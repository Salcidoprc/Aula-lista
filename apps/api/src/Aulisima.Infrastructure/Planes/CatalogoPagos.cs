using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Aulisima.Application.Planes;
using Microsoft.Extensions.Options;

namespace Aulisima.Infrastructure.Planes;

/// <summary>
/// El catálogo en Stripe (ADR-0017): productos y precios mensuales recurrentes. Las altas llevan
/// <c>Idempotency-Key</c>, así un reintento devuelve el mismo objeto. Un precio no se edita: se archiva
/// (<c>active=false</c>) y quien ya lo tiene lo sigue pagando.
/// </summary>
internal sealed class CatalogoStripe(HttpClient http, IOptions<OpcionesPagos> opciones) : ICatalogoPagos
{
    public async Task<string> CrearProductoAsync(Guid productoId, string nombre, string descripcion, CancellationToken cancellationToken)
    {
        var campos = new List<KeyValuePair<string, string>> { new("name", nombre), new("metadata[producto_id]", productoId.ToString()) };
        if (!string.IsNullOrWhiteSpace(descripcion))
        {
            campos.Add(new("description", descripcion));
        }

        using var json = await PostAsync("v1/products", campos, $"producto-{productoId}", cancellationToken);
        return json.RootElement.GetProperty("id").GetString()!;
    }

    public async Task ActualizarProductoAsync(string idExterno, string nombre, string descripcion, CancellationToken cancellationToken)
    {
        // Stripe borra la descripción con un valor vacío.
        using var _ = await PostAsync(
            $"v1/products/{Uri.EscapeDataString(idExterno)}", [new("name", nombre), new("description", descripcion)], null, cancellationToken);
    }

    public async Task ArchivarProductoAsync(string idExterno, CancellationToken cancellationToken)
    {
        using var _ = await PostAsync($"v1/products/{Uri.EscapeDataString(idExterno)}", [new("active", "false")], null, cancellationToken);
    }

    public async Task<string> CrearPrecioAsync(
        string producto, decimal monto, string moneda, string apodo, string claveIdempotencia, CancellationToken cancellationToken)
    {
        using var json = await PostAsync(
            "v1/prices",
            [
                new("product", producto),
                new("currency", moneda.ToLowerInvariant()),
                new("unit_amount", Centavos(monto)),
                new("recurring[interval]", "month"),
                new("nickname", apodo),
                new("tax_behavior", "inclusive"),
            ],
            claveIdempotencia,
            cancellationToken);
        return json.RootElement.GetProperty("id").GetString()!;
    }

    public async Task ArchivarPrecioAsync(string idExterno, CancellationToken cancellationToken)
    {
        using var _ = await PostAsync($"v1/prices/{Uri.EscapeDataString(idExterno)}", [new("active", "false")], null, cancellationToken);
    }

    public async Task<string> ProductoDePrecioAsync(string precio, CancellationToken cancellationToken)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, $"v1/prices/{Uri.EscapeDataString(precio)}");
        using var json = await EnviarAsync(solicitud, cancellationToken);
        var producto = json.RootElement.GetProperty("product");
        return producto.ValueKind == JsonValueKind.String ? producto.GetString()! : producto.GetProperty("id").GetString()!;
    }

    private static string Centavos(decimal monto) =>
        decimal.ToInt64(decimal.Round(monto * 100m, 0, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);

    private async Task<JsonDocument> PostAsync(string ruta, IEnumerable<KeyValuePair<string, string>> campos, string? clave, CancellationToken ct)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, ruta) { Content = new FormUrlEncodedContent(campos) };
        if (clave is not null)
        {
            solicitud.Headers.Add("Idempotency-Key", clave);
        }

        return await EnviarAsync(solicitud, ct);
    }

    private async Task<JsonDocument> EnviarAsync(HttpRequestMessage solicitud, CancellationToken ct)
    {
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opciones.Value.ClaveSecreta);
        HttpResponseMessage respuesta;
        try
        {
            respuesta = await http.SendAsync(solicitud, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new HttpRequestException("Stripe no respondió a tiempo.", ex);
        }

        using (respuesta)
        {
            await using var cuerpo = await respuesta.Content.ReadAsStreamAsync(ct);
            var json = await JsonDocument.ParseAsync(cuerpo, cancellationToken: ct);
            if (!respuesta.IsSuccessStatusCode)
            {
                var error = json.RootElement.TryGetProperty("error", out var e)
                    ? $"{Texto(e, "type")}/{Texto(e, "code")}: {Texto(e, "message")}"
                    : "desconocido";
                json.Dispose();
                throw new HttpRequestException($"Stripe respondió {(int)respuesta.StatusCode} ({error}).");
            }

            return json;
        }
    }

    private static string? Texto(JsonElement objeto, string propiedad) =>
        objeto.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;
}

/// <summary>Sin Stripe: ids inventados y estables por clave de idempotencia, para probar el panel completo.</summary>
internal sealed class CatalogoSimulado : ICatalogoPagos
{
    public Task<string> CrearProductoAsync(Guid productoId, string nombre, string descripcion, CancellationToken cancellationToken) =>
        Task.FromResult($"prod_sim_{productoId:N}");

    public Task ActualizarProductoAsync(string idExterno, string nombre, string descripcion, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task ArchivarProductoAsync(string idExterno, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<string> CrearPrecioAsync(
        string producto, decimal monto, string moneda, string apodo, string claveIdempotencia, CancellationToken cancellationToken) =>
        Task.FromResult($"price_sim_{claveIdempotencia.Replace("precio-", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal)}");

    public Task ArchivarPrecioAsync(string idExterno, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<string> ProductoDePrecioAsync(string precio, CancellationToken cancellationToken) => Task.FromResult($"prod_sim_{precio}");
}
