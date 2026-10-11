using AulaLista.Application.Comun;
using AulaLista.Domain.Cuentas;
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
    }
}
