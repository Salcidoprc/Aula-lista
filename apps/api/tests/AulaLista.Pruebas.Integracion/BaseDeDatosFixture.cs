using AulaLista.Infrastructure.Persistencia;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;

namespace AulaLista.Pruebas.Integracion;

/// <summary>
/// PostgreSQL 18 real con los mismos roles que staging: migra con app_migraciones y la API usa app_runtime.
/// </summary>
public sealed class BaseDeDatosFixture : IAsyncLifetime
{
    private const string Base = "aula_lista";
    private const string ClaveMigraciones = "migraciones-pruebas";
    private const string ClaveRuntime = "runtime-pruebas";

    private readonly PostgreSqlContainer _contenedor = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase(Base)
        .Build();

    public string ConexionRuntime => ConexionComo("app_runtime", ClaveRuntime);

    public string ConexionMigraciones => ConexionComo("app_migraciones", ClaveMigraciones);

    public string ConexionAdmin => _contenedor.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _contenedor.StartAsync();

        var rutaRoles = Path.Combine(RaizRepositorio(), "infra", "postgres", "roles.sql");
        await _contenedor.CopyAsync(await File.ReadAllBytesAsync(rutaRoles), "/tmp/roles.sql");
        var resultado = await _contenedor.ExecAsync(
        [
            "psql", "-v", "ON_ERROR_STOP=1", "-U", "postgres", "-d", Base,
            "-v", $"base={Base}",
            "-v", $"clave_migraciones={ClaveMigraciones}",
            "-v", $"clave_runtime={ClaveRuntime}",
            "-f", "/tmp/roles.sql",
        ]);
        if (resultado.ExitCode != 0)
        {
            throw new InvalidOperationException($"No se pudieron crear los roles: {resultado.Stderr}");
        }

        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{Migrador.CadenaConexion}"] = ConexionMigraciones,
            })
            .Build();
        await Migrador.MigrarAsync(configuracion);
    }

    public Task DisposeAsync() => _contenedor.DisposeAsync().AsTask();

    private string ConexionComo(string usuario, string clave) =>
        new NpgsqlConnectionStringBuilder(_contenedor.GetConnectionString())
        {
            Username = usuario,
            Password = clave,
        }.ConnectionString;

    private static string RaizRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "infra")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }
}

[CollectionDefinition(Nombre)]
public sealed class ColeccionBaseDeDatos : ICollectionFixture<BaseDeDatosFixture>
{
    public const string Nombre = "Base de datos";
}
