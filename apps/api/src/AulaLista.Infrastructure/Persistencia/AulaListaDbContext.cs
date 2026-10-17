using AulaLista.Application.Comun;
using AulaLista.Domain.Cuentas;
using AulaLista.Domain.Evaluacion;
using AulaLista.Domain.Grupos;
using AulaLista.Domain.Operacion;
using Microsoft.EntityFrameworkCore;

namespace AulaLista.Infrastructure.Persistencia;

public sealed class AulaListaDbContext : DbContext
{
    private readonly IContextoUsuario? _contexto;

    public AulaListaDbContext(DbContextOptions<AulaListaDbContext> options, IContextoUsuario contexto)
        : base(options) => _contexto = contexto;

    /// <summary>Para migraciones y herramientas, sin docente: los filtros por dueño no devuelven filas.</summary>
    public AulaListaDbContext(DbContextOptions<AulaListaDbContext> options)
        : base(options)
    {
    }

    public DbSet<CicloEscolar> CiclosEscolares => Set<CicloEscolar>();

    public DbSet<Unidad> Unidades => Set<Unidad>();

    public DbSet<Grupo> Grupos => Set<Grupo>();

    public DbSet<Periodo> Periodos => Set<Periodo>();

    public DbSet<Alumno> Alumnos => Set<Alumno>();

    public DbSet<VinculoTutor> VinculosTutor => Set<VinculoTutor>();

    public DbSet<Importacion> Importaciones => Set<Importacion>();

    public DbSet<TipoRubro> TiposRubro => Set<TipoRubro>();

    public DbSet<Rubro> Rubros => Set<Rubro>();

    public DbSet<Criterio> Criterios => Set<Criterio>();

    public DbSet<Actividad> Actividades => Set<Actividad>();

    public DbSet<Calificacion> Calificaciones => Set<Calificacion>();

    public DbSet<CalificacionCriterio> CalificacionesCriterio => Set<CalificacionCriterio>();

    public DbSet<CambioSincronizado> CambiosSincronizados => Set<CambioSincronizado>();

    public DbSet<Cuenta> Cuentas => Set<Cuenta>();

    public DbSet<TokenCuenta> TokensCuenta => Set<TokenCuenta>();

    public DbSet<Sesion> Sesiones => Set<Sesion>();

    public DbSet<Docente> Docentes => Set<Docente>();

    public DbSet<AceptacionLegal> AceptacionesLegales => Set<AceptacionLegal>();

    public DbSet<Exportacion> Exportaciones => Set<Exportacion>();

    public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();

    /// <summary>Docente de la solicitud para los filtros globales por dueño (además de RLS).</summary>
    private Guid? DocenteActual => _contexto?.DocenteId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AulaListaDbContext).Assembly);

        modelBuilder.Entity<Docente>().HasQueryFilter(d => d.Id == DocenteActual);
        modelBuilder.Entity<AceptacionLegal>().HasQueryFilter(a => a.DocenteId == DocenteActual);
        modelBuilder.Entity<Exportacion>().HasQueryFilter(e => e.DocenteId == DocenteActual);
        modelBuilder.Entity<CicloEscolar>().HasQueryFilter(c => c.DocenteId == DocenteActual);
        modelBuilder.Entity<Unidad>().HasQueryFilter(u => u.DocenteId == DocenteActual);
        modelBuilder.Entity<Grupo>().HasQueryFilter(g => g.DocenteId == DocenteActual);
        modelBuilder.Entity<Periodo>().HasQueryFilter(p => p.DocenteId == DocenteActual);
        modelBuilder.Entity<Alumno>().HasQueryFilter(a => a.DocenteId == DocenteActual);
        modelBuilder.Entity<VinculoTutor>().HasQueryFilter(v => v.DocenteId == DocenteActual);
        modelBuilder.Entity<Importacion>().HasQueryFilter(i => i.DocenteId == DocenteActual);
        modelBuilder.Entity<Rubro>().HasQueryFilter(r => r.DocenteId == DocenteActual);
        modelBuilder.Entity<Criterio>().HasQueryFilter(c => c.DocenteId == DocenteActual);
        modelBuilder.Entity<Actividad>().HasQueryFilter(a => a.DocenteId == DocenteActual);
        modelBuilder.Entity<Calificacion>().HasQueryFilter(c => c.DocenteId == DocenteActual);
        modelBuilder.Entity<CalificacionCriterio>().HasQueryFilter(c => c.DocenteId == DocenteActual);
        modelBuilder.Entity<CambioSincronizado>().HasQueryFilter(c => c.DocenteId == DocenteActual);
    }
}
