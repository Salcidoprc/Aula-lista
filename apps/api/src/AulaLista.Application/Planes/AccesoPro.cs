using AulaLista.Application.Cuentas;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Grupos;
using AulaLista.Domain.Planes;
using Microsoft.Extensions.Options;

namespace AulaLista.Application.Planes;

/// <summary>Qué plan tiene un grupo hoy, para mostrarlo y para decidir los candados.</summary>
public static class PlanVigente
{
    public const string Gratis = "gratis";
    public const string Pro = "pro";

    /// <summary>RN-081: el grupo de la prueba Pro mientras está vigente.</summary>
    public const string Prueba = "prueba";
}

/// <summary>
/// El plan de un grupo. <see cref="ProHasta"/>: Pro pagado que ya tiene fecha de fin (grupo quitado, gracia o fin de un
/// prepago). <see cref="DiasPrueba"/> y <see cref="PruebaHastaPublicar"/> solo en el grupo de la prueba.
/// <see cref="PruebaDisponible"/>: el docente aún no usa su prueba y este grupo podría usarla.
/// </summary>
public sealed record PlanGrupoDto(string Plan, DateTimeOffset? ProHasta, int? DiasPrueba, bool PruebaHastaPublicar, bool PruebaDisponible)
{
    public bool TienePro => Plan != PlanVigente.Gratis;
}

/// <summary>
/// SPEC-08: decide si un grupo tiene Pro (pagado o en prueba) y lleva la prueba. Lee la suscripción del docente una vez
/// por solicitud.
/// </summary>
public sealed class AccesoPro(
    IRepositorioPlanes planes, IRepositorioDocentes docentes, IOptions<OpcionesPlanes> opciones, IOptions<OpcionesPagos> pagos, TimeProvider reloj)
{
    private Suscripcion? _suscripcion;
    private bool _cargada;

    public OpcionesPlanes Opciones => opciones.Value;

    public async Task<Suscripcion?> SuscripcionAsync(CancellationToken ct)
    {
        if (!_cargada)
        {
            _suscripcion = await planes.SuscripcionAsync(ct);
            _cargada = true;
        }

        return _suscripcion;
    }

    /// <summary>La suscripción del docente; si no existe se crea vacía (Gratis, RN-080).</summary>
    public async Task<Suscripcion> SuscripcionOCrearAsync(Guid docenteId, CancellationToken ct)
    {
        if (await SuscripcionAsync(ct) is { } existente)
        {
            return existente;
        }

        _suscripcion = new Suscripcion(docenteId, pagos.Value.Proveedor, reloj.GetUtcNow());
        planes.Agregar(_suscripcion);
        return _suscripcion;
    }

    public async Task<PlanGrupoDto> PlanAsync(Grupo grupo, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var suscripcion = await SuscripcionAsync(ct);
        var prueba = suscripcion?.Prueba(Opciones.DiasPrueba, Opciones.DiasMaximoPrueba);
        if (grupo.TieneProPagado(ahora))
        {
            return new PlanGrupoDto(PlanVigente.Pro, grupo.ProHasta, null, false, false);
        }

        if (prueba is not null && prueba.GrupoId == grupo.Id && prueba.Vigente(ahora))
        {
            return new PlanGrupoDto(PlanVigente.Prueba, null, prueba.DiasRestantes(ahora), prueba.TerminaAlPublicar(ahora), false);
        }

        return new PlanGrupoDto(PlanVigente.Gratis, null, null, false, suscripcion?.PruebaUsada != true && !grupo.EstaArchivado);
    }

    public async Task<bool> TieneProAsync(Grupo grupo, CancellationToken ct) => (await PlanAsync(grupo, ct)).TienePro;

    /// <summary>RN-081: activa la prueba en ese grupo y periodo; una sola vez por docente.</summary>
    public async Task<Resultado> IniciarPruebaAsync(Grupo grupo, Guid periodoId, CancellationToken ct)
    {
        var suscripcion = await SuscripcionOCrearAsync(grupo.DocenteId, ct);
        var ahora = reloj.GetUtcNow();
        var resultado = suscripcion.IniciarPrueba(grupo.Id, periodoId, ahora);
        if (resultado.EsExito && await docentes.PorIdAsync(grupo.DocenteId, ct) is { } docente)
        {
            docente.MarcarPruebaUsada(ahora);
        }

        return resultado;
    }

    /// <summary>RN-081: al cerrarse el periodo de la prueba (boletas publicadas) empieza la cuenta de su fin.</summary>
    public async Task PeriodoCerradoAsync(Guid periodoId, CancellationToken ct)
    {
        if (await SuscripcionAsync(ct) is { } suscripcion)
        {
            suscripcion.RegistrarPeriodoCerrado(periodoId, reloj.GetUtcNow());
        }
    }
}
