using AulaLista.Application.Comun;
using AulaLista.Application.Grupos;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Planes;

namespace AulaLista.Application.Planes;

public static class EstadoSesionSimulada
{
    public const string Abierta = "abierta";
    public const string Pagada = "pagada";
    public const string Rechazada = "rechazada";
}

/// <summary>
/// Un Checkout de la pasarela simulada. <see cref="Tipo"/> es <c>suscripcion</c> o el <see cref="TipoPago"/> del pago
/// único. Vive en memoria: solo existe para probar sin Stripe.
/// </summary>
public sealed record SesionSimulada(
    string Id, Guid DocenteId, string Tipo, IReadOnlyList<Guid> Grupos, int Meses, decimal Monto, bool Oxxo, string Referencia,
    DateTimeOffset? VenceEn, string Estado);

/// <summary>Lo que solo la pasarela simulada sabe hacer: mostrar su sesión y cerrarla.</summary>
public interface IPasarelaSimulada
{
    public const string TipoSuscripcion = "suscripcion";

    SesionSimulada? Sesion(string id);

    void Terminar(string id, string estado);
}

public sealed record GrupoSimuladoDto(Guid Id, string Nombre, int Grado);

public sealed record SesionSimuladaDto(
    string Id, string Tipo, decimal Monto, int Meses, bool Oxxo, string Referencia, DateTimeOffset? VenceEn, string Estado,
    IReadOnlyList<GrupoSimuladoDto> Grupos);

/// <summary>Una renovación mensual simulada con tarjeta: pagada o rechazada (para probar la gracia, RN-085).</summary>
public sealed record RenovacionSimulada(bool Pagada);

/// <summary>
/// La página "Pago de prueba" (solo sin Stripe): confirma o rechaza la sesión como lo haría el proveedor, con los mismos
/// eventos, aplicados en la solicitud. Una sesión ajena no existe (404).
/// </summary>
public sealed class PagoSimuladoCasoUso(
    IPasarelaPagos pasarela, ProcesadorEventosPago procesador, IRepositorioGrupos grupos, AccesoPro acceso, IContextoUsuario usuario,
    IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<SesionSimuladaDto?> ObtenerAsync(string id, CancellationToken ct) =>
        SesionPropia(id) is { } sesion ? await DtoAsync(sesion, ct) : null;

    public async Task<Resultado<SesionSimuladaDto>?> TerminarAsync(string id, bool pagar, CancellationToken ct)
    {
        if (SesionPropia(id) is not { } sesion)
        {
            return null;
        }

        if (sesion.Estado != EstadoSesionSimulada.Abierta)
        {
            return Resultado.Fallo<SesionSimuladaDto>(ErroresPlanes.PagoSimuladoTerminado);
        }

        var ahora = reloj.GetUtcNow();
        var docente = sesion.DocenteId;
        EventoPagoRecibido? evento = (sesion.Tipo, pagar) switch
        {
            (IPasarelaSimulada.TipoSuscripcion, true) => new EventoPagoRecibido(
                NuevoId(), TipoEventoPago.SuscripcionActualizada, docente, Cliente: $"cus_sim_{docente:N}", Suscripcion: $"sub_sim_{Guid.NewGuid():N}",
                Grupos: sesion.Grupos, PeriodoFin: ahora.AddMonths(1), Activa: true),

            // Una tarjeta rechazada en el Checkout no crea nada.
            (IPasarelaSimulada.TipoSuscripcion, false) => null,
            (_, true) => new EventoPagoRecibido(NuevoId(), TipoEventoPago.PagoUnicoPagado, docente, Sesion: sesion.Id),
            _ => new EventoPagoRecibido(NuevoId(), TipoEventoPago.PagoUnicoFallido, docente, Sesion: sesion.Id),
        };

        if (evento is not null)
        {
            await procesador.AplicarAsync(evento, ct);
            await unidad.GuardarAsync(ct);
        }

        Simulada!.Terminar(id, pagar ? EstadoSesionSimulada.Pagada : EstadoSesionSimulada.Rechazada);
        return Resultado.Exito((await DtoAsync(Simulada.Sesion(id)!, ct))!);
    }

    /// <summary>RN-085: simula el cobro mensual siguiente de la suscripción con tarjeta.</summary>
    public async Task<Resultado<bool>?> RenovarAsync(RenovacionSimulada comando, CancellationToken ct)
    {
        if (Simulada is null)
        {
            return null;
        }

        if (await acceso.SuscripcionAsync(ct) is not { EsTarjetaActiva: true } s)
        {
            return Resultado.Fallo<bool>(ErroresPlanes.SinTarjeta);
        }

        var evento = comando.Pagada
            ? new EventoPagoRecibido(NuevoId(), TipoEventoPago.CobroPagado, s.DocenteId, PeriodoFin: (s.PeriodoActualFin ?? reloj.GetUtcNow()).AddMonths(1))
            : new EventoPagoRecibido(NuevoId(), TipoEventoPago.CobroFallido, s.DocenteId);
        await procesador.AplicarAsync(evento, ct);
        await unidad.GuardarAsync(ct);
        return Resultado.Exito(true);
    }

    private IPasarelaSimulada? Simulada => pasarela as IPasarelaSimulada;

    private SesionSimulada? SesionPropia(string id) =>
        Simulada?.Sesion(id) is { } sesion && sesion.DocenteId == usuario.DocenteId ? sesion : null;

    private static string NuevoId() => $"evt_sim_{Guid.NewGuid():N}";

    private async Task<SesionSimuladaDto?> DtoAsync(SesionSimulada sesion, CancellationToken ct)
    {
        var lista = new List<GrupoSimuladoDto>();
        foreach (var id in sesion.Grupos)
        {
            if (await grupos.GrupoAsync(id, ct) is { } grupo)
            {
                lista.Add(new GrupoSimuladoDto(grupo.Id, grupo.Nombre, grupo.Grado));
            }
        }

        return new SesionSimuladaDto(
            sesion.Id, sesion.Tipo, sesion.Monto, sesion.Meses, sesion.Oxxo, sesion.Referencia, sesion.VenceEn, sesion.Estado, lista);
    }
}
