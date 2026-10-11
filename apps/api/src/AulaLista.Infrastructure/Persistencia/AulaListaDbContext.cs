using AulaLista.Domain.Grupos;
using Microsoft.EntityFrameworkCore;

namespace AulaLista.Infrastructure.Persistencia;

public sealed class AulaListaDbContext(DbContextOptions<AulaListaDbContext> options) : DbContext(options)
{
    public DbSet<CicloEscolar> CiclosEscolares => Set<CicloEscolar>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AulaListaDbContext).Assembly);
    }
}
