using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

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
        await using (var db = new AulaListaDbContext(opciones))
        {
            await db.Database.MigrateAsync(cancellationToken);
        }

        await PrepararTrabajosAsync(conexion, cancellationToken);
    }

    /// <summary>
    /// Hangfire crea su esquema aquí, con el rol de migraciones; en ejecución la API corre sin DDL
    /// (PrepareSchemaIfNecessary = false) y solo con permisos de datos.
    /// </summary>
    private static async Task PrepararTrabajosAsync(string conexion, CancellationToken cancellationToken)
    {
        await using var npgsql = new NpgsqlConnection(conexion);
        await npgsql.OpenAsync(cancellationToken);
        PostgreSqlObjectsInstaller.Install(npgsql, DependencyInjection.EsquemaTrabajos);
        await using var permisos = new NpgsqlCommand(
            $"""
            GRANT USAGE ON SCHEMA {DependencyInjection.EsquemaTrabajos} TO app_runtime;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {DependencyInjection.EsquemaTrabajos} TO app_runtime;
            GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA {DependencyInjection.EsquemaTrabajos} TO app_runtime;
            """,
            npgsql);
        await permisos.ExecuteNonQueryAsync(cancellationToken);
    }
}
