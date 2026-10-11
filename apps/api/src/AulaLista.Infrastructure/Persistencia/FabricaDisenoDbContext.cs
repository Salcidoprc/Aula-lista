using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AulaLista.Infrastructure.Persistencia;

/// <summary>Usada solo por <c>dotnet ef</c> para generar migraciones; no se conecta a la base.</summary>
internal sealed class FabricaDisenoDbContext : IDesignTimeDbContextFactory<AulaListaDbContext>
{
    public AulaListaDbContext CreateDbContext(string[] args)
    {
        var conexion = Environment.GetEnvironmentVariable("ConnectionStrings__Migraciones")
            ?? "Host=localhost;Database=aula_lista;Username=app_migraciones";
        var opciones = new DbContextOptionsBuilder<AulaListaDbContext>()
            .UseNpgsql(conexion)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AulaListaDbContext(opciones);
    }
}
