using AulaLista.Application.Evaluacion;
using AulaLista.Application.Grupos;
using AulaLista.Domain.Evaluacion;
using AulaLista.Domain.Grupos;
using AulaLista.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AulaLista.Infrastructure.Evaluacion;

/// <summary>Todas las consultas pasan por los filtros globales por docente y por RLS; el catálogo de tipos es global.</summary>
internal sealed class RepositorioEvaluacion(AulaListaDbContext db) : IRepositorioEvaluacion, IDatosDeEvaluacion
{
    private List<TipoRubro>? _tipos;

    public async Task<IReadOnlyList<TipoRubro>> TiposRubroAsync(CancellationToken cancellationToken) =>
        _tipos ??= await db.TiposRubro.AsNoTracking().OrderBy(t => t.Orden).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Rubro>> RubrosAsync(Guid periodoId, CancellationToken cancellationToken) =>
        await db.Rubros.Include(r => r.Criterios).Where(r => r.PeriodoId == periodoId).OrderBy(r => r.Orden).ToListAsync(cancellationToken);

    public Task<Rubro?> RubroAsync(Guid id, CancellationToken cancellationToken) =>
        db.Rubros.Include(r => r.Criterios).SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Rubro>> RubrosPorIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await db.Rubros.Include(r => r.Criterios).Where(r => ids.Contains(r.Id)).ToListAsync(cancellationToken);

    public void Agregar(Rubro rubro) => db.Rubros.Add(rubro);

    public void Borrar(Rubro rubro) => db.Rubros.Remove(rubro);

    public async Task<IReadOnlyList<Actividad>> ActividadesAsync(IReadOnlyCollection<Guid> rubroIds, CancellationToken cancellationToken) =>
        await db.Actividades.Where(a => rubroIds.Contains(a.RubroId)).OrderBy(a => a.Orden).ToListAsync(cancellationToken);

    public Task<Actividad?> ActividadAsync(Guid id, CancellationToken cancellationToken) =>
        db.Actividades.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Actividad>> ActividadesPorIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await db.Actividades.Where(a => ids.Contains(a.Id)).ToListAsync(cancellationToken);

    public void Agregar(Actividad actividad) => db.Actividades.Add(actividad);

    public void Borrar(Actividad actividad) => db.Actividades.Remove(actividad);

    public async Task<IReadOnlyList<Calificacion>> CalificacionesAsync(IReadOnlyCollection<Guid> actividadIds, CancellationToken cancellationToken) =>
        actividadIds.Count == 0
            ? []
            : await db.Calificaciones.Include(c => c.Criterios).Where(c => actividadIds.Contains(c.ActividadId)).AsSplitQuery()
                .ToListAsync(cancellationToken);

    public void Agregar(Calificacion calificacion) => db.Calificaciones.Add(calificacion);

    public async Task<IReadOnlySet<Guid>> RubrosConCalificacionesAsync(Guid periodoId, CancellationToken cancellationToken) =>
        (await CeldasCapturadas()
            .Join(db.Actividades, c => c.ActividadId, a => a.Id, (c, a) => a.RubroId)
            .Join(db.Rubros.Where(r => r.PeriodoId == periodoId), id => id, r => r.Id, (id, _) => id)
            .Distinct()
            .ToListAsync(cancellationToken))
        .ToHashSet();

    public async Task<IReadOnlySet<Guid>> CriteriosConCalificacionesAsync(Guid periodoId, CancellationToken cancellationToken) =>
        (await db.CalificacionesCriterio
            .Join(db.Criterios, d => d.CriterioId, c => c.Id, (d, c) => c)
            .Join(db.Rubros.Where(r => r.PeriodoId == periodoId), c => c.RubroId, r => r.Id, (c, _) => c.Id)
            .Distinct()
            .ToListAsync(cancellationToken))
        .ToHashSet();

    public async Task<IReadOnlySet<Guid>> CambiosAplicadosAsync(IReadOnlyCollection<Guid> claves, CancellationToken cancellationToken) =>
        (await db.CambiosSincronizados.Where(c => claves.Contains(c.Id)).Select(c => c.Id).ToListAsync(cancellationToken)).ToHashSet();

    public void Agregar(CambioSincronizado cambio) => db.CambiosSincronizados.Add(cambio);

    public async Task<IReadOnlyList<Equipo>> EquiposAsync(Guid grupoId, CancellationToken cancellationToken) =>
        await db.Equipos.Include(e => e.Integrantes).Where(e => e.GrupoId == grupoId).OrderBy(e => e.Orden).ThenBy(e => e.Nombre)
            .ToListAsync(cancellationToken);

    public void Agregar(Equipo equipo) => db.Equipos.Add(equipo);

    public void Borrar(Equipo equipo) => db.Equipos.Remove(equipo);

    public async Task<IReadOnlyList<ActividadEquipo>> ComposicionesAsync(IReadOnlyCollection<Guid> actividadIds, CancellationToken cancellationToken) =>
        actividadIds.Count == 0
            ? []
            : await db.ActividadesEquipo.Where(a => actividadIds.Contains(a.ActividadId)).ToListAsync(cancellationToken);

    public void Agregar(ActividadEquipo integrante) => db.ActividadesEquipo.Add(integrante);

    public async Task<IReadOnlyList<Nota>> NotasAsync(Guid alumnoId, CancellationToken cancellationToken) =>
        await db.Notas.Where(n => n.AlumnoId == alumnoId).OrderByDescending(n => n.Fecha).ThenByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<Nota?> NotaAsync(Guid id, CancellationToken cancellationToken) => db.Notas.SingleOrDefaultAsync(n => n.Id == id, cancellationToken);

    public void Agregar(Nota nota) => db.Notas.Add(nota);

    public async Task<IReadOnlyList<TomaAsistencia>> TomasAsync(Guid grupoId, DateOnly desde, DateOnly hasta, CancellationToken cancellationToken) =>
        await db.TomasAsistencia.Include(t => t.Alumnos).Where(t => t.GrupoId == grupoId && t.Fecha >= desde && t.Fecha <= hasta)
            .OrderBy(t => t.Fecha).AsSplitQuery().ToListAsync(cancellationToken);

    public Task<TomaAsistencia?> TomaAsync(Guid grupoId, DateOnly fecha, CancellationToken cancellationToken) =>
        db.TomasAsistencia.Include(t => t.Alumnos).SingleOrDefaultAsync(t => t.GrupoId == grupoId && t.Fecha == fecha, cancellationToken);

    public void Agregar(TomaAsistencia toma) => db.TomasAsistencia.Add(toma);

    public void Borrar(TomaAsistencia toma) => db.TomasAsistencia.Remove(toma);

    public async Task<IReadOnlyList<DateOnly>> FechasDeTomasAsync(Guid cicloId, CancellationToken cancellationToken) =>
        await db.TomasAsistencia
            .Join(db.Grupos.Where(g => g.CicloEscolarId == cicloId), t => t.GrupoId, g => g.Id, (t, _) => t.Fecha)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlySet<Guid>> UnidadesConDatosAsync(Guid cicloId, CancellationToken cancellationToken) =>
        (await db.Rubros
            .Join(db.Periodos, r => r.PeriodoId, p => p.Id, (r, p) => p.UnidadId)
            .Join(db.Unidades.Where(u => u.CicloEscolarId == cicloId), id => id, u => u.Id, (id, _) => id)
            .Distinct()
            .ToListAsync(cancellationToken))
        .ToHashSet();

    public Task<bool> GrupoTieneCalificacionesAsync(Guid grupoId, CancellationToken cancellationToken) =>
        CeldasCapturadas()
            .Join(db.Alumnos.Where(a => a.GrupoId == grupoId), c => c.AlumnoId, a => a.Id, (c, _) => c.Id)
            .AnyAsync(cancellationToken);

    /// <summary>Celdas con algo capturado: valor, marca o detalle por criterio.</summary>
    private IQueryable<Calificacion> CeldasCapturadas() =>
        db.Calificaciones.Where(c => c.Valor != null || c.Marca != MarcaCalificacion.Ninguna || db.CalificacionesCriterio.Any(d => d.CalificacionId == c.Id));
}
