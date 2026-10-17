using AulaLista.Application.Grupos;
using AulaLista.Domain.Grupos;
using AulaLista.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AulaLista.Infrastructure.Grupos;

/// <summary>Todas las consultas pasan por los filtros globales por docente y por RLS.</summary>
internal sealed class RepositorioGrupos(AulaListaDbContext db) : IRepositorioGrupos
{
    public async Task<IReadOnlyList<CicloEscolar>> CiclosAsync(CancellationToken cancellationToken) =>
        await db.CiclosEscolares.Include(c => c.Unidades).AsSplitQuery().ToListAsync(cancellationToken);

    public Task<CicloEscolar?> CicloAsync(Guid id, CancellationToken cancellationToken) =>
        db.CiclosEscolares.Include(c => c.Unidades).SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<bool> ExisteCicloAsync(string nombre, Guid? excepto, CancellationToken cancellationToken) =>
        (await db.CiclosEscolares.Where(c => c.Id != excepto).Select(c => c.Nombre).ToListAsync(cancellationToken))
        .Any(n => string.Equals(n, nombre, StringComparison.OrdinalIgnoreCase));

    public void Agregar(CicloEscolar ciclo) => db.CiclosEscolares.Add(ciclo);

    public async Task<IReadOnlySet<Guid>> UnidadesCerradasAsync(Guid cicloId, CancellationToken cancellationToken) =>
        (await db.Periodos
            .Where(p => p.Estado == EstadoPeriodo.Cerrado && db.Unidades.Any(u => u.Id == p.UnidadId && u.CicloEscolarId == cicloId))
            .Select(p => p.UnidadId)
            .Distinct()
            .ToListAsync(cancellationToken))
        .ToHashSet();

    public async Task<IReadOnlyList<GrupoConConteo>> GruposAsync(bool archivados, CancellationToken cancellationToken)
    {
        var filas = await db.Grupos
            .Where(g => archivados || g.ArchivadoEn == null)
            .Join(db.CiclosEscolares, g => g.CicloEscolarId, c => c.Id, (g, c) => new
            {
                Grupo = g,
                Ciclo = c.Nombre,
                CicloInicio = c.Inicio,
                Activos = db.Alumnos.Count(a => a.GrupoId == g.Id && a.BajaEn == null),
            })
            .OrderBy(x => x.Grupo.ArchivadoEn != null)
            .ThenByDescending(x => x.CicloInicio)
            .ThenBy(x => x.Grupo.Grado)
            .ThenBy(x => x.Grupo.Nombre)
            .ThenBy(x => x.Grupo.Materia)
            .ToListAsync(cancellationToken);
        return filas.Select(x => new GrupoConConteo(x.Grupo, x.Ciclo, x.Activos)).ToList();
    }

    public Task<Grupo?> GrupoAsync(Guid id, CancellationToken cancellationToken) =>
        db.Grupos.SingleOrDefaultAsync(g => g.Id == id, cancellationToken);

    public async Task<Grupo?> GrupoParaInscribirAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Grupos
            .FromSql($"SELECT * FROM grupo WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Grupo>> GruposDelCicloAsync(Guid cicloId, CancellationToken cancellationToken) =>
        await db.Grupos.Where(g => g.CicloEscolarId == cicloId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<(int Grado, string Nombre, string? Materia)>> NombresDeGruposAsync(
        Guid cicloId, Guid? excepto, CancellationToken cancellationToken) =>
        (await db.Grupos
            .Where(g => g.CicloEscolarId == cicloId && g.Id != excepto)
            .Select(g => new { g.Grado, g.Nombre, g.Materia })
            .ToListAsync(cancellationToken))
        .Select(g => (g.Grado, g.Nombre, g.Materia))
        .ToList();

    public void Agregar(Grupo grupo) => db.Grupos.Add(grupo);

    public void Borrar(Grupo grupo) => db.Grupos.Remove(grupo);

    public void Agregar(IEnumerable<Periodo> periodos) => db.Periodos.AddRange(periodos);

    public async Task<PeriodoCompleto?> PeriodoAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Periodos
            .Where(p => p.Id == id)
            .Join(db.Unidades, p => p.UnidadId, u => u.Id, (p, u) => new { p, u })
            .Join(db.Grupos, x => x.p.GrupoId, g => g.Id, (x, g) => new { x.p, x.u, g })
            .Select(x => new PeriodoCompleto(x.p, x.u, x.g))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PeriodoDeGrupo>> PeriodosAsync(Guid grupoId, CancellationToken cancellationToken) =>
        (await db.Periodos
            .Where(p => p.GrupoId == grupoId)
            .Join(db.Unidades, p => p.UnidadId, u => u.Id, (p, u) => new { p, u })
            .ToListAsync(cancellationToken))
        .Select(x => new PeriodoDeGrupo(x.p, x.u))
        .ToList();

    public async Task<IReadOnlyList<Alumno>> AlumnosAsync(Guid grupoId, CancellationToken cancellationToken) =>
        await db.Alumnos.Where(a => a.GrupoId == grupoId).ToListAsync(cancellationToken);

    public Task<Alumno?> AlumnoAsync(Guid id, CancellationToken cancellationToken) =>
        db.Alumnos.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public void Agregar(IEnumerable<Alumno> alumnos) => db.Alumnos.AddRange(alumnos);

    public async Task<IReadOnlyList<VinculoTutor>> VinculosAsync(IReadOnlyCollection<Guid> alumnoIds, CancellationToken cancellationToken) =>
        await db.VinculosTutor.Where(v => alumnoIds.Contains(v.AlumnoId)).ToListAsync(cancellationToken);

    public Task<bool> TieneVinculoConAsync(Guid tutorId, CancellationToken cancellationToken) =>
        db.VinculosTutor.AnyAsync(v => v.TutorId == tutorId, cancellationToken);

    public void Agregar(VinculoTutor vinculo) => db.VinculosTutor.Add(vinculo);

    public void Borrar(VinculoTutor vinculo) => db.VinculosTutor.Remove(vinculo);

    public void Agregar(Importacion importacion) => db.Importaciones.Add(importacion);

    public Task<Importacion?> ImportacionAsync(Guid id, CancellationToken cancellationToken) =>
        db.Importaciones.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
}

/// <summary>Usa <c>vincular_tutor</c>, que corre como dueño del esquema (ADR-0014).</summary>
internal sealed class Tutores(AulaListaDbContext db) : ITutores
{
    public async Task<(Guid TutorId, bool YaExistia)> VincularAsync(
        string telefonoE164, string nombre, string? correo, CancellationToken cancellationToken)
    {
        var fila = await db.Database
            .SqlQuery<FilaTutor>($"SELECT tutor_id, ya_existia FROM vincular_tutor({telefonoE164}, {nombre}, {correo})")
            .SingleAsync(cancellationToken);
        return (fila.TutorId, fila.YaExistia);
    }

    private sealed class FilaTutor
    {
        public Guid TutorId { get; init; }

        public bool YaExistia { get; init; }
    }
}
