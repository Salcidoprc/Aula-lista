using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AulaLista.Infrastructure.Persistencia;

/// <summary>Aplica las migraciones con el rol <c>app_migraciones</c>. Se ejecuta antes de arrancar cada versión.</summary>
public static class Migrador
{
    public const string CadenaConexion = "Migraciones";

    public static async Task MigrarAsync(IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var conexion = configuration.GetConnectionString(CadenaConexion)
            ?? throw new InvalidOperationException("Falta ConnectionStrings__Migraciones.");
        var opciones = new DbContextOptionsBuilder<AulaListaDbContext>()
            .UseNpgsql(conexion)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var db = new AulaListaDbContext(opciones);
        await db.Database.MigrateAsync(cancellationToken);
    }
}
