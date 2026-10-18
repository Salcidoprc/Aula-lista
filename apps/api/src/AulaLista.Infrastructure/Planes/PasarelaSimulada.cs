using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using AulaLista.Application.Cuentas;
using AulaLista.Application.Planes;
using Microsoft.Extensions.Options;

namespace AulaLista.Infrastructure.Planes;

/// <summary>Los Checkout simulados abiertos, en memoria del proceso.</summary>
public sealed class SesionesPagoSimuladas
{
    private readonly ConcurrentDictionary<string, SesionSimulada> _sesiones = new(StringComparer.Ordinal);

    public void Guardar(SesionSimulada sesion) => _sesiones[sesion.Id] = sesion;

    public SesionSimulada? Leer(string id) => _sesiones.GetValueOrDefault(id);
}

/// <summary>
/// Sin Stripe (local y pruebas): el Checkout es la página "Pago de prueba" de la app, que confirma con los mismos
/// eventos que mandaría Stripe. Los webhooks se firman igual que los de Stripe con <c>Pagos__SecretoWebhook</c>.
/// </summary>
internal sealed class PasarelaSimulada(
    SesionesPagoSimuladas sesiones,
    IRepositorioPlanes planes,
    ProcesadorEventosPago procesador,
    IOptions<OpcionesPagos> pagos,
    IOptions<OpcionesPlanes> opcionesPlanes,
    IOptions<OpcionesCuentas> cuentas,
    TimeProvider reloj) : IPasarelaPagos, IPasarelaSimulada
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Proveedor => OpcionesPagos.ProveedorSimulado;

    public Task<SesionPago> CheckoutSuscripcionAsync(NuevaSuscripcion datos, CancellationToken cancellationToken)
    {
        var id = $"cs_sim_{Guid.NewGuid():N}";
        sesiones.Guardar(new SesionSimulada(
            id, datos.DocenteId, IPasarelaSimulada.TipoSuscripcion, datos.Grupos, 0, opcionesPlanes.Value.Mensual(datos.Grupos.Count), false,
            string.Empty, null, EstadoSesionSimulada.Abierta));
        return Task.FromResult(new SesionPago(id, Url(id), null));
    }

    public Task<SesionPago> CheckoutPagoUnicoAsync(PagoUnico datos, CancellationToken cancellationToken)
    {
        var id = $"cs_sim_{Guid.NewGuid():N}";
        DateTimeOffset? vence = datos.SoloOxxo ? reloj.GetUtcNow().AddDays(datos.DiasFicha) : null;
        var referencia = datos.SoloOxxo ? string.Concat(Enumerable.Range(0, 14).Select(_ => RandomNumberGenerator.GetInt32(10))) : string.Empty;
        sesiones.Guardar(new SesionSimulada(
            id, datos.DocenteId, datos.Tipo, datos.Grupos, datos.Meses, datos.Monto, datos.SoloOxxo, referencia, vence, EstadoSesionSimulada.Abierta));
        var url = Url(id);
        return Task.FromResult(new SesionPago(id, url, vence, datos.SoloOxxo ? url : null));
    }

    /// <summary>El proveedor confirmaría el cambio con su evento; aquí se aplica de una vez.</summary>
    public async Task ActualizarGruposAsync(
        Guid docenteId, string suscripcion, IReadOnlyList<Guid> grupos, bool prorratear, CancellationToken cancellationToken)
    {
        var actual = await planes.SuscripcionAsync(cancellationToken);
        await procesador.AplicarAsync(
            new EventoPagoRecibido(
                $"evt_sim_{Guid.NewGuid():N}", TipoEventoPago.SuscripcionActualizada, docenteId, Cliente: actual?.ClienteExterno,
                Suscripcion: suscripcion, Grupos: grupos, PeriodoFin: actual?.PeriodoActualFin, Activa: true),
            cancellationToken);
    }

    public Task<string> PortalAsync(string cliente, string urlRegreso, CancellationToken cancellationToken) =>
        Task.FromResult($"{urlRegreso}?portal=simulado");

    public EventoLeido? LeerEvento(byte[] cuerpo, string? firma)
    {
        var opciones = pagos.Value;
        if (!FirmaPagos.Valida(cuerpo, firma, opciones.SecretoWebhook, reloj.GetUtcNow(), opciones.ToleranciaFirmaSegundos))
        {
            return null;
        }

        try
        {
            var evento = JsonSerializer.Deserialize<EventoPagoRecibido>(cuerpo, Json);
            return new EventoLeido(evento is { Id.Length: > 0, Tipo.Length: > 0 } && evento.DocenteId != Guid.Empty ? evento : null);
        }
        catch (JsonException)
        {
            return new EventoLeido(null);
        }
    }

    public SesionSimulada? Sesion(string id) => sesiones.Leer(id);

    public void Terminar(string id, string estado)
    {
        if (sesiones.Leer(id) is { } sesion)
        {
            sesiones.Guardar(sesion with { Estado = estado });
        }
    }

    private string Url(string id) => $"{cuentas.Value.UrlWeb.TrimEnd('/')}/plan/pago-simulado/{id}";
}
