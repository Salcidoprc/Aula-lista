using AulaLista.Application.Tutores;
using AulaLista.Domain.Boletas;
using AulaLista.Domain.Grupos;
using AulaLista.Domain.Tutores;
using AulaLista.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AulaLista.Infrastructure.Tutores;

/// <summary>Enlaces y sesiones del tutor, con RLS por tutor y por docente vinculado.</summary>
internal sealed class RepositorioEnlaces(AulaListaDbContext db) : IRepositorioEnlaces
{
    public Task<Guid?> TutorDelEnlaceAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.Database.SqlQuery<Guid?>($"SELECT tutor_del_enlace({tokenHash}) AS \"Value\"").SingleAsync(cancellationToken);

    public Task<Guid?> TutorDeLaSesionAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.Database.SqlQuery<Guid?>($"SELECT tutor_de_la_sesion({tokenHash}) AS \"Value\"").SingleAsync(cancellationToken);

    public Task<EnlaceTutor?> EnlacePorHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.EnlacesTutor.SingleOrDefaultAsync(e => e.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<EnlaceTutor>> EnlacesAsync(Guid tutorId, CancellationToken cancellationToken) =>
        await db.EnlacesTutor.Where(e => e.TutorId == tutorId).ToListAsync(cancellationToken);

    public async Task<SesionConEnlace?> SesionPorHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        await db.SesionesTutor
            .Where(s => s.TokenHash == tokenHash)
            .Join(db.EnlacesTutor, s => s.EnlaceId, e => e.Id, (s, e) => new SesionConEnlace(s, e))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<SesionTutor>> SesionesAsync(Guid tutorId, CancellationToken cancellationToken) =>
        await db.SesionesTutor.Where(s => s.TutorId == tutorId).ToListAsync(cancellationToken);

    public Task<int> SesionesDesdeAsync(Guid enlaceId, DateTimeOffset desde, CancellationToken cancellationToken) =>
        db.SesionesTutor.CountAsync(s => s.EnlaceId == enlaceId && s.CreatedAt >= desde, cancellationToken);

    public void Agregar(EnlaceTutor enlace) => db.EnlacesTutor.Add(enlace);

    public void Agregar(SesionTutor sesion) => db.SesionesTutor.Add(sesion);
}

/// <summary>
/// Lecturas del portal. Los filtros globales son por docente, así que aquí se ignoran y se filtra por el tutor de
/// forma explícita; RLS con <c>app.tutor_id</c> es la segunda barrera (solo vínculos suyos y boletas publicadas).
/// </summary>
internal sealed class ConsultasTutor(AulaListaDbContext db) : IConsultasTutor
{
    public Task<string?> NombreAsync(Guid tutorId, CancellationToken cancellationToken) =>
        db.Set<Tutor>().Where(t => t.Id == tutorId).Select(t => t.Nombre).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<HijoTutor>> HijosAsync(Guid tutorId, CancellationToken cancellationToken) =>
        await (
            from a in db.Alumnos.IgnoreQueryFilters()
            where AlumnosDe(tutorId).Contains(a.Id)
            join g in db.Grupos.IgnoreQueryFilters() on a.GrupoId equals g.Id
            join d in db.Docentes.IgnoreQueryFilters() on a.DocenteId equals d.Id into docentes
            from d in docentes.DefaultIfEmpty()
            select new HijoTutor(a.Id, a.GrupoId, a.Nombres, a.Apellidos, a.BajaEn, g.Nivel, g.Grado, g.Nombre, g.Materia, d.Nombre))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BoletaDeHijo>> BoletasAsync(
        Guid tutorId, IReadOnlyCollection<Guid> alumnoIds, CancellationToken cancellationToken) =>
        await (
            from b in Publicadas(tutorId)
            where alumnoIds.Contains(b.AlumnoId)
            join p in db.Periodos.IgnoreQueryFilters() on b.PeriodoId equals p.Id
            join u in db.Unidades.IgnoreQueryFilters() on p.UnidadId equals u.Id
            select new BoletaDeHijo(b, u.Inicio))
            .ToListAsync(cancellationToken);

    public Task<Boleta?> BoletaAsync(Guid tutorId, Guid boletaId, CancellationToken cancellationToken) =>
        Publicadas(tutorId).SingleOrDefaultAsync(b => b.Id == boletaId, cancellationToken);

    private IQueryable<Guid> AlumnosDe(Guid tutorId) =>
        db.VinculosTutor.IgnoreQueryFilters().Where(v => v.TutorId == tutorId).Select(v => v.AlumnoId);

    /// <summary>RN-064: solo lo publicado, y de cada boleta corregida solo la versión vigente (RN-052).</summary>
    private IQueryable<Boleta> Publicadas(Guid tutorId) =>
        db.Boletas.IgnoreQueryFilters()
            .Where(b => b.PublicadaEn != null && b.ReemplazadaPor == null && AlumnosDe(tutorId).Contains(b.AlumnoId));
}
