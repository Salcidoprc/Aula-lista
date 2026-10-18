using AulaLista.Application.Avisos;
using AulaLista.Domain.Avisos;
using AulaLista.Domain.Grupos;
using AulaLista.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AulaLista.Infrastructure.Avisos;

/// <summary>Avisos del docente con RLS por docente; tutores y suscripciones se ven por el vínculo con él.</summary>
internal sealed class RepositorioAvisos(AulaListaDbContext db) : IRepositorioAvisos
{
    public async Task<IReadOnlyList<AvisoConLecturas>> AvisosAsync(Guid grupoId, CancellationToken cancellationToken) =>
        await db.Avisos
            .Where(a => a.GrupoId == grupoId && a.BorradoEn == null)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AvisoConLecturas(
                a,
                db.EntregasAviso.Count(e => e.AvisoId == a.Id),
                db.EntregasAviso.Count(e => e.AvisoId == a.Id && e.LeidoEn != null)))
            .ToListAsync(cancellationToken);

    public Task<Aviso?> AvisoAsync(Guid id, CancellationToken cancellationToken) =>
        db.Avisos.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<EntregaAviso>> EntregasAsync(Guid avisoId, CancellationToken cancellationToken) =>
        await db.EntregasAviso.Where(e => e.AvisoId == avisoId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, ContactoPropio>> ContactosAsync(
        IReadOnlyCollection<Guid> tutorIds, CancellationToken cancellationToken)
    {
        if (tutorIds.Count == 0)
        {
            return new Dictionary<Guid, ContactoPropio>();
        }

        var correos = await db.Set<Tutor>().Where(t => tutorIds.Contains(t.Id)).Select(t => new { t.Id, t.Correo }).ToListAsync(cancellationToken);
        var conPush = (await db.SuscripcionesPush.Where(s => tutorIds.Contains(s.TutorId)).Select(s => s.TutorId).Distinct()
            .ToListAsync(cancellationToken)).ToHashSet();
        return correos.ToDictionary(t => t.Id, t => new ContactoPropio(t.Correo, conPush.Contains(t.Id)));
    }

    public async Task<IReadOnlyList<SuscripcionPush>> SuscripcionesAsync(
        IReadOnlyCollection<Guid> tutorIds, CancellationToken cancellationToken) =>
        tutorIds.Count == 0 ? [] : await db.SuscripcionesPush.Where(s => tutorIds.Contains(s.TutorId)).ToListAsync(cancellationToken);

    /// <summary>Los fallidos no cuentan: Meta no los cobra.</summary>
    public async Task<IReadOnlyDictionary<Guid, int>> MensajesPorTutorAsync(
        Guid grupoId, string plantilla, DateTimeOffset? desde, Guid? referenciaId, CancellationToken cancellationToken)
    {
        var consulta = db.MensajesWhatsapp.Where(m => m.GrupoId == grupoId && m.Plantilla == plantilla && m.Estado != EstadoMensaje.Fallido);
        if (desde is { } inicio)
        {
            consulta = consulta.Where(m => m.CreatedAt >= inicio);
        }

        if (referenciaId is { } referencia)
        {
            consulta = consulta.Where(m => m.ReferenciaId == referencia);
        }

        // Los apartados en esta misma solicitud todavía no están en la base.
        var nuevos = db.ChangeTracker.Entries<MensajeWhatsapp>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .Where(m => m.GrupoId == grupoId && m.Plantilla == plantilla && (desde is null || m.CreatedAt >= desde)
                && (referenciaId is null || m.ReferenciaId == referenciaId))
            .ToList();
        var cuenta = await consulta.GroupBy(m => m.TutorId).Select(g => new { g.Key, Total = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Total, cancellationToken);
        foreach (var m in nuevos)
        {
            cuenta[m.TutorId] = cuenta.GetValueOrDefault(m.TutorId) + 1;
        }

        return cuenta;
    }

    public async Task<IReadOnlyList<MensajeWhatsapp>> MensajesAsync(Guid referenciaId, string plantilla, CancellationToken cancellationToken) =>
        await db.MensajesWhatsapp.Where(m => m.ReferenciaId == referenciaId && m.Plantilla == plantilla).ToListAsync(cancellationToken);

    public void Agregar(Aviso aviso) => db.Avisos.Add(aviso);

    public void Agregar(IEnumerable<EntregaAviso> entregas) => db.EntregasAviso.AddRange(entregas);

    public void Agregar(MensajeWhatsapp mensaje) => db.MensajesWhatsapp.Add(mensaje);

    public void Borrar(SuscripcionPush suscripcion) => db.SuscripcionesPush.Remove(suscripcion);

    public Task<bool> ActualizarEstadoWhatsappAsync(string idMeta, string estado, CancellationToken cancellationToken) =>
        db.Database.SqlQuery<bool>($"SELECT actualizar_mensaje_whatsapp({idMeta}, {estado}) AS \"Value\"").SingleAsync(cancellationToken);
}

/// <summary>
/// Lo del tutor en el portal. Los filtros globales son por docente, así que se ignoran y se filtra por el tutor;
/// RLS con <c>app.tutor_id</c> solo deja ver sus entregas, sus avisos sin borrar y sus suscripciones.
/// </summary>
internal sealed class AvisosDelTutor(AulaListaDbContext db) : IAvisosDelTutor
{
    public async Task<IReadOnlyList<AvisoDelTutor>> AvisosAsync(Guid tutorId, CancellationToken cancellationToken) =>
        await (
            from e in db.EntregasAviso.IgnoreQueryFilters()
            where e.TutorId == tutorId
            join a in db.Avisos.IgnoreQueryFilters() on e.AvisoId equals a.Id
            where a.BorradoEn == null
            join d in db.Docentes.IgnoreQueryFilters() on a.DocenteId equals d.Id into docentes
            from d in docentes.DefaultIfEmpty()
            orderby a.CreatedAt descending
            select new AvisoDelTutor(a, e, d.Nombre))
            .ToListAsync(cancellationToken);

    public Task<string?> CorreoAsync(Guid tutorId, CancellationToken cancellationToken) =>
        db.Set<Tutor>().Where(t => t.Id == tutorId).Select(t => t.Correo).SingleOrDefaultAsync(cancellationToken);

    public async Task FijarCorreoAsync(string? correo, CancellationToken cancellationToken) =>
        await db.Database.ExecuteSqlAsync($"SELECT fijar_correo_tutor({correo}::text)", cancellationToken);

    public Task<SuscripcionPush?> SuscripcionAsync(Guid tutorId, string endpoint, CancellationToken cancellationToken) =>
        db.SuscripcionesPush.SingleOrDefaultAsync(s => s.TutorId == tutorId && s.Endpoint == endpoint, cancellationToken);

    public void Agregar(SuscripcionPush suscripcion) => db.SuscripcionesPush.Add(suscripcion);

    public void Borrar(SuscripcionPush suscripcion) => db.SuscripcionesPush.Remove(suscripcion);
}
