using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using AulaLista.Application.Planes;
using AulaLista.Domain.Planes;
using Microsoft.Extensions.Options;

namespace AulaLista.Infrastructure.Planes;

/// <summary>
/// Stripe por su API REST (ADR-0009): Checkout alojado (la app nunca ve datos de tarjeta), Billing para la suscripción
/// mensual, OXXO como pago único y el portal de clientes. Los grupos viajan como metadatos de la sesión y de la
/// suscripción, y vuelven en los webhooks.
/// </summary>
internal sealed class PasarelaStripe(HttpClient http, IOptions<OpcionesPagos> opciones, TimeProvider reloj) : IPasarelaPagos
{
    /// <summary>Stripe acepta 500 caracteres por valor de metadatos: 13 ids por clave.</summary>
    private const int GruposPorClave = 13;
    private const int ClavesDeGrupos = 10;
    private const string Locale = "es-419";

    public string Proveedor => OpcionesPagos.ProveedorStripe;

    public async Task<SesionPago> CheckoutSuscripcionAsync(NuevaSuscripcion datos, CancellationToken cancellationToken)
    {
        var o = opciones.Value;
        var campos = new List<KeyValuePair<string, string>>
        {
            new("mode", "subscription"),
            new("locale", Locale),
            new("success_url", datos.UrlExito),
            new("cancel_url", datos.UrlCancelar),
            new("client_reference_id", datos.DocenteId.ToString()),
            new("metadata[docente_id]", datos.DocenteId.ToString()),
            new("subscription_data[metadata][docente_id]", datos.DocenteId.ToString()),
            new("line_items[0][price]", o.PrecioPrimerGrupoId!),
            new("line_items[0][quantity]", "1"),
        };
        if (datos.Grupos.Count > 1)
        {
            campos.Add(new("line_items[1][price]", o.PrecioGrupoAdicionalId!));
            campos.Add(new("line_items[1][quantity]", (datos.Grupos.Count - 1).ToString(CultureInfo.InvariantCulture)));
        }

        Cliente(campos, datos.Cliente, datos.Correo);
        campos.AddRange(Grupos("subscription_data[metadata]", datos.Grupos));
        using var json = await PostAsync("v1/checkout/sessions", campos, cancellationToken);
        return Sesion(json.RootElement, null);
    }

    public async Task<SesionPago> CheckoutPagoUnicoAsync(PagoUnico datos, CancellationToken cancellationToken)
    {
        var campos = new List<KeyValuePair<string, string>>
        {
            new("mode", "payment"),
            new("locale", Locale),
            new("success_url", datos.UrlExito),
            new("cancel_url", datos.UrlCancelar),
            new("client_reference_id", datos.DocenteId.ToString()),
            new("payment_method_types[0]", datos.SoloOxxo ? "oxxo" : "card"),
            new("line_items[0][quantity]", "1"),
            new("line_items[0][price_data][currency]", "mxn"),
            new("line_items[0][price_data][unit_amount]", Centavos(datos.Monto)),
            new("line_items[0][price_data][product_data][name]", datos.Concepto),
            new("metadata[docente_id]", datos.DocenteId.ToString()),
            new("metadata[tipo]", datos.Tipo),
            new("metadata[meses]", datos.Meses.ToString(CultureInfo.InvariantCulture)),
        };
        if (datos.SoloOxxo)
        {
            campos.Add(new("payment_method_options[oxxo][expires_after_days]", datos.DiasFicha.ToString(CultureInfo.InvariantCulture)));
        }

        Cliente(campos, datos.Cliente, datos.Correo);
        campos.AddRange(Grupos("metadata", datos.Grupos));
        using var json = await PostAsync("v1/checkout/sessions", campos, cancellationToken);

        // La ficha la muestra el Checkout y Stripe la manda por correo; su vencimiento llega con el webhook.
        return Sesion(json.RootElement, datos.SoloOxxo ? reloj.GetUtcNow().AddDays(datos.DiasFicha) : null);
    }

    /// <summary>RN-083: cantidades nuevas; con prorrateo al agregar y sin él al quitar (aplica al siguiente corte).</summary>
    public async Task ActualizarGruposAsync(
        Guid docenteId, string suscripcion, IReadOnlyList<Guid> grupos, bool prorratear, CancellationToken cancellationToken)
    {
        var o = opciones.Value;
        using var actual = await GetAsync($"v1/subscriptions/{Uri.EscapeDataString(suscripcion)}", cancellationToken);
        var items = actual.RootElement.GetProperty("items").GetProperty("data").EnumerateArray()
            .Select(i => (Id: i.GetProperty("id").GetString()!, Precio: i.GetProperty("price").GetProperty("id").GetString()))
            .ToList();
        var campos = new List<KeyValuePair<string, string>>
        {
            new("proration_behavior", prorratear ? "create_prorations" : "none"),
        };
        var n = 0;
        void Item(string? id, string precio, int cantidad)
        {
            var prefijo = $"items[{n++}]";
            if (id is not null)
            {
                campos.Add(new($"{prefijo}[id]", id));
            }
            else
            {
                campos.Add(new($"{prefijo}[price]", precio));
            }

            if (cantidad == 0)
            {
                campos.Add(new($"{prefijo}[deleted]", "true"));
            }
            else
            {
                campos.Add(new($"{prefijo}[quantity]", cantidad.ToString(CultureInfo.InvariantCulture)));
            }
        }

        Item(items.FirstOrDefault(i => i.Precio == o.PrecioPrimerGrupoId).Id, o.PrecioPrimerGrupoId!, 1);
        var adicional = items.FirstOrDefault(i => i.Precio == o.PrecioGrupoAdicionalId).Id;
        if (adicional is not null || grupos.Count > 1)
        {
            Item(adicional, o.PrecioGrupoAdicionalId!, grupos.Count - 1);
        }

        campos.AddRange(Grupos("metadata", grupos, borrarSobrantes: true));
        using var _ = await PostAsync($"v1/subscriptions/{Uri.EscapeDataString(suscripcion)}", campos, cancellationToken);
    }

    public async Task<string> PortalAsync(string cliente, string urlRegreso, CancellationToken cancellationToken)
    {
        using var json = await PostAsync(
            "v1/billing_portal/sessions", [new("customer", cliente), new("return_url", urlRegreso), new("locale", Locale)], cancellationToken);
        return json.RootElement.GetProperty("url").GetString()!;
    }

    public EventoLeido? LeerEvento(byte[] cuerpo, string? firma)
    {
        var o = opciones.Value;
        if (!FirmaPagos.Valida(cuerpo, firma, o.SecretoWebhook, reloj.GetUtcNow(), o.ToleranciaFirmaSegundos))
        {
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(cuerpo);
            return new EventoLeido(Traducir(json.RootElement));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return new EventoLeido(null);
        }
    }

    /// <summary>Del evento de Stripe al de la app; nulo si no nos interesa o no trae docente.</summary>
    internal static EventoPagoRecibido? Traducir(JsonElement evento)
    {
        var id = evento.GetProperty("id").GetString()!;
        var tipo = evento.GetProperty("type").GetString();
        var objeto = evento.GetProperty("data").GetProperty("object");
        switch (tipo)
        {
            case "customer.subscription.created" or "customer.subscription.updated" or "customer.subscription.deleted":
                {
                    if (Docente(Metadatos(objeto)) is not { } docente)
                    {
                        return null;
                    }

                    var estado = Texto(objeto, "status");
                    if (tipo == "customer.subscription.deleted" || estado is "canceled" or "unpaid" or "incomplete_expired")
                    {
                        return new EventoPagoRecibido(id, TipoEventoPago.SuscripcionCancelada, docente);
                    }

                    if (estado is not ("active" or "trialing" or "past_due"))
                    {
                        return null;
                    }

                    return new EventoPagoRecibido(
                        id, TipoEventoPago.SuscripcionActualizada, docente, Cliente: Texto(objeto, "customer"), Suscripcion: Texto(objeto, "id"),
                        Grupos: GruposDe(Metadatos(objeto)), PeriodoFin: FinDePeriodo(objeto), Activa: true);
                }

            case "invoice.paid" or "invoice.payment_failed":
                {
                    if (Docente(MetadatosFactura(objeto)) is not { } docente)
                    {
                        return null;
                    }

                    if (tipo == "invoice.payment_failed")
                    {
                        return new EventoPagoRecibido(id, TipoEventoPago.CobroFallido, docente);
                    }

                    DateTimeOffset? fin = objeto.TryGetProperty("lines", out var lineas) && lineas.TryGetProperty("data", out var datos)
                        ? datos.EnumerateArray().Select(l => Fecha(l.GetProperty("period"), "end")).Max()
                        : null;
                    return new EventoPagoRecibido(id, TipoEventoPago.CobroPagado, docente, PeriodoFin: fin);
                }

            case "checkout.session.completed" or "checkout.session.async_payment_succeeded"
                or "checkout.session.async_payment_failed" or "checkout.session.expired":
                {
                    if (Texto(objeto, "mode") != "payment" || Docente(Metadatos(objeto)) is not { } docente)
                    {
                        return null;
                    }

                    var sesion = Texto(objeto, "id");
                    return tipo switch
                    {
                        "checkout.session.completed" when Texto(objeto, "payment_status") == "paid"
                            => new EventoPagoRecibido(id, TipoEventoPago.PagoUnicoPagado, docente, Sesion: sesion),
                        "checkout.session.completed" => new EventoPagoRecibido(id, TipoEventoPago.PagoUnicoPendiente, docente, Sesion: sesion),
                        "checkout.session.async_payment_succeeded" => new EventoPagoRecibido(id, TipoEventoPago.PagoUnicoPagado, docente, Sesion: sesion),
                        _ => new EventoPagoRecibido(id, TipoEventoPago.PagoUnicoFallido, docente, Sesion: sesion),
                    };
                }

            default:
                return null;
        }
    }

    private static void Cliente(List<KeyValuePair<string, string>> campos, string? cliente, string correo) =>
        campos.Add(cliente is null ? new("customer_email", correo) : new("customer", cliente));

    private static IEnumerable<KeyValuePair<string, string>> Grupos(string prefijo, IReadOnlyList<Guid> grupos, bool borrarSobrantes = false)
    {
        var partes = grupos.Chunk(GruposPorClave).ToList();
        if (partes.Count > ClavesDeGrupos)
        {
            throw new InvalidOperationException("Demasiados grupos para los metadatos de Stripe.");
        }

        for (var i = 0; i < ClavesDeGrupos; i++)
        {
            if (i < partes.Count)
            {
                yield return new($"{prefijo}[grupos_{i}]", string.Join(',', partes[i]));
            }
            else if (borrarSobrantes)
            {
                yield return new($"{prefijo}[grupos_{i}]", string.Empty);
            }
        }
    }

    private static List<Guid> GruposDe(JsonElement? metadatos)
    {
        var lista = new List<Guid>();
        if (metadatos is not { ValueKind: JsonValueKind.Object } m)
        {
            return lista;
        }

        for (var i = 0; i < ClavesDeGrupos; i++)
        {
            if (m.TryGetProperty($"grupos_{i}", out var valor) && valor.GetString() is { Length: > 0 } texto)
            {
                lista.AddRange(texto.Split(',').Select(t => Guid.TryParse(t, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty));
            }
        }

        return lista;
    }

    private static JsonElement? Metadatos(JsonElement objeto) =>
        objeto.TryGetProperty("metadata", out var m) && m.ValueKind == JsonValueKind.Object ? m : null;

    /// <summary>La factura trae los metadatos de su suscripción en <c>parent</c> (API nueva) o en <c>subscription_details</c>.</summary>
    private static JsonElement? MetadatosFactura(JsonElement factura)
    {
        if (factura.TryGetProperty("parent", out var padre) && padre.ValueKind == JsonValueKind.Object
            && padre.TryGetProperty("subscription_details", out var detalle) && detalle.ValueKind == JsonValueKind.Object)
        {
            return Metadatos(detalle);
        }

        return factura.TryGetProperty("subscription_details", out var viejo) && viejo.ValueKind == JsonValueKind.Object ? Metadatos(viejo) : null;
    }

    private static Guid? Docente(JsonElement? metadatos) =>
        metadatos is { } m && m.TryGetProperty("docente_id", out var valor) && Guid.TryParse(valor.GetString(), out var id) ? id : null;

    /// <summary><c>current_period_end</c> está en la suscripción (API vieja) o en cada concepto (API nueva).</summary>
    private static DateTimeOffset? FinDePeriodo(JsonElement suscripcion)
    {
        if (Fecha(suscripcion, "current_period_end") is { } fin)
        {
            return fin;
        }

        return suscripcion.TryGetProperty("items", out var items) && items.TryGetProperty("data", out var datos)
            ? datos.EnumerateArray().Select(i => Fecha(i, "current_period_end")).Max()
            : null;
    }

    private static DateTimeOffset? Fecha(JsonElement objeto, string propiedad) =>
        objeto.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeSeconds(valor.GetInt64())
            : null;

    private static string? Texto(JsonElement objeto, string propiedad) =>
        objeto.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;

    private static string Centavos(decimal monto) =>
        decimal.ToInt64(decimal.Round(monto * 100m, 0, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);

    private static SesionPago Sesion(JsonElement sesion, DateTimeOffset? vence) =>
        new(sesion.GetProperty("id").GetString()!, sesion.GetProperty("url").GetString()!, vence);

    private async Task<JsonDocument> PostAsync(string ruta, IEnumerable<KeyValuePair<string, string>> campos, CancellationToken ct)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, ruta) { Content = new FormUrlEncodedContent(campos) };
        return await EnviarAsync(solicitud, ct);
    }

    private async Task<JsonDocument> GetAsync(string ruta, CancellationToken ct)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, ruta);
        return await EnviarAsync(solicitud, ct);
    }

    private async Task<JsonDocument> EnviarAsync(HttpRequestMessage solicitud, CancellationToken ct)
    {
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opciones.Value.ClaveSecreta);
        using var respuesta = await http.SendAsync(solicitud, ct);
        await using var cuerpo = await respuesta.Content.ReadAsStreamAsync(ct);
        var json = await JsonDocument.ParseAsync(cuerpo, cancellationToken: ct);
        if (!respuesta.IsSuccessStatusCode)
        {
            // Solo el tipo y el código de Stripe: nunca datos del docente.
            var error = json.RootElement.TryGetProperty("error", out var e) ? $"{Texto(e, "type")}/{Texto(e, "code")}" : "desconocido";
            json.Dispose();
            throw new HttpRequestException($"Stripe respondió {(int)respuesta.StatusCode} ({error}).");
        }

        return json;
    }
}
