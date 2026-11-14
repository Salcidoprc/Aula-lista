using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Aulisima.Infrastructure.Persistencia;

/// <summary>Usada solo por <c>dotnet ef</c> para generar migraciones; no se conecta a la base.</summary>
internal sealed class FabricaDisenoDbContext : IDesignTimeDbContextFactory<AulisimaDbContext>
{
    public AulisimaDbContext CreateDbContext(string[] args)
    {
        var conexion = Environment.GetEnvironmentVariable("ConnectionStrings__Migraciones")
            ?? "Host=localhost;Database=aulisima;Username=app_migraciones";
        var opciones = new DbContextOptionsBuilder<AulisimaDbContext>()
            .UseNpgsql(conexion)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AulisimaDbContext(opciones);
    }
}
