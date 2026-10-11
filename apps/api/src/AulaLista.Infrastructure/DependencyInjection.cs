using AulaLista.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AulaLista.Infrastructure;

public static class DependencyInjection
{
    public const string CadenaConexion = "AulaLista";

    public static IServiceCollection AddInfraestructura(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<VariablesRlsInterceptor>();
        services.AddDbContext<AulaListaDbContext>((sp, options) => options
            .UseNpgsql(configuration.GetConnectionString(CadenaConexion))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<VariablesRlsInterceptor>()));

        services.AddHealthChecks().AddDbContextCheck<AulaListaDbContext>("base_de_datos", tags: ["lista"]);
        return services;
    }
}
