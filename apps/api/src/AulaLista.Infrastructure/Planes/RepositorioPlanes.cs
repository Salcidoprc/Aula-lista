using AulaLista.Application.Planes;
using AulaLista.Domain.Planes;
using AulaLista.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AulaLista.Infrastructure.Planes;

/// <summary>Suscripción, pagos y eventos del docente con RLS por docente.</summary>
internal sealed class RepositorioPlanes(AulaListaDbContext db) : IRepositorioPlanes
{
    public Task<Suscripcion?> SuscripcionAsync(CancellationToken cancellationToken) =>
        db.Suscripciones.SingleOrDefaultAsync(cancellationToken);

    public void Agregar(Suscripcion suscripcion) => db.Suscripciones.Add(suscripcion);

    public async Task<IReadOnlyList<Pago>> PagosAsync(CancellationToken cancellationToken) =>
        await db.Pagos.OrderByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);

    public Task<Pago?> PagoPorSesionAsync(string sesion, CancellationToken cancellationToken) =>
        db.Pagos.SingleOrDefaultAsync(p => p.SesionExterna == sesion, cancellationToken);

    public void Agregar(Pago pago) => db.Pagos.Add(pago);

    public async Task<bool> EventoAplicadoAsync(string idExterno, CancellationToken cancellationToken) =>
        db.EventosPago.Local.Any(e => e.IdExterno == idExterno)
        || await db.EventosPago.AnyAsync(e => e.IdExterno == idExterno, cancellationToken);

    public void Agregar(EventoPago evento) => db.EventosPago.Add(evento);

    /// <summary>Usa la función <c>docentes_con_plan</c>, que corre con permisos del dueño del esquema.</summary>
    public async Task<IReadOnlyList<Guid>> DocentesConPlanAsync(CancellationToken cancellationToken) =>
        await db.Database.SqlQuery<Guid>($"SELECT docente_id AS \"Value\" FROM docentes_con_plan()").ToListAsync(cancellationToken);
}
