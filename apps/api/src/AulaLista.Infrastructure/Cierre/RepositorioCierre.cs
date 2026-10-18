using AulaLista.Application.Cierre;
using AulaLista.Domain.Cierre;
using AulaLista.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AulaLista.Infrastructure.Cierre;

/// <summary>Observaciones, estilo y llamadas a la IA, con los filtros globales por docente y RLS.</summary>
internal sealed class RepositorioCierre(AulaListaDbContext db) : IRepositorioCierre
{
    public async Task<IReadOnlyList<Observacion>> ObservacionesAsync(Guid periodoId, CancellationToken cancellationToken) =>
        await db.Observaciones.Where(o => o.PeriodoId == periodoId).ToListAsync(cancellationToken);

    public Task<Observacion?> ObservacionAsync(Guid id, CancellationToken cancellationToken) =>
        db.Observaciones.SingleOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Observacion>> ObservacionesPorIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await db.Observaciones.Where(o => ids.Contains(o.Id)).ToListAsync(cancellationToken);

    public void Agregar(Observacion observacion) => db.Observaciones.Add(observacion);

    public async Task MarcarDatosCambiaronAsync(
        Guid periodoId, IReadOnlyCollection<Guid>? alumnoIds, DateTimeOffset ahora, CancellationToken cancellationToken)
    {
        var aprobadas = db.Observaciones.Where(o => o.PeriodoId == periodoId && o.Estado == EstadoObservacion.Aprobada);
        if (alumnoIds is not null)
        {
            aprobadas = aprobadas.Where(o => alumnoIds.Contains(o.AlumnoId));
        }

        await aprobadas.ExecuteUpdateAsync(
            s => s.SetProperty(o => o.Estado, EstadoObservacion.Borrador)
                .SetProperty(o => o.DatosCambiaron, true)
                .SetProperty(o => o.AprobadaEn, (DateTimeOffset?)null)
                .SetProperty(o => o.UpdatedAt, ahora),
            cancellationToken);
    }

    public Task<EstiloObservaciones?> EstiloAsync(Guid docenteId, CancellationToken cancellationToken) =>
        db.EstilosObservaciones.SingleOrDefaultAsync(e => e.DocenteId == docenteId, cancellationToken);

    public void Agregar(EstiloObservaciones estilo) => db.EstilosObservaciones.Add(estilo);

    public void Agregar(LlamadaIa llamada) => db.LlamadasIa.Add(llamada);

    public async Task<int> SolicitudesIaDesdeAsync(DateTimeOffset desde, DateTimeOffset ahora, CancellationToken cancellationToken)
    {
        var llamadas = await db.LlamadasIa.CountAsync(l => l.CreatedAt >= desde, cancellationToken);
        var perdida = ahora - Observacion.GeneracionPerdida;
        var esperando = await db.Observaciones.CountAsync(o => o.Estado == EstadoObservacion.Generando && o.UpdatedAt >= perdida, cancellationToken);
        return llamadas + esperando;
    }
}
