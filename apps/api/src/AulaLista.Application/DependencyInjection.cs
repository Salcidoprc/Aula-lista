using AulaLista.Application.Cuentas;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AulaLista.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAplicacion(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        var casosDeUso = typeof(DependencyInjection).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true }
                && (t.Name.EndsWith("CasoUso", StringComparison.Ordinal) || t.Name.EndsWith("Trabajo", StringComparison.Ordinal)));
        foreach (var tipo in casosDeUso)
        {
            services.AddScoped(tipo);
        }

        services.AddScoped<EmisorSesiones>();
        return services;
    }
}
