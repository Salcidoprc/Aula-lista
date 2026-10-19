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
        services.AddScoped<Grupos.VerificacionCorreo>();
        services.AddScoped<Evaluacion.ConsultaRubros>();
        services.AddScoped<Evaluacion.ContextoRubros>();
        services.AddScoped<Cierre.ConsultaCierre>();
        services.AddScoped<Cierre.AccionesObservacion>();
        services.AddScoped<Cierre.AvisoDatosCambiaron>();
        services.AddScoped<Boletas.ArmadoBoletas>();
        services.AddScoped<Boletas.ConsultaBoletas>();
        services.AddScoped<Tutores.ValidacionSesionTutor>();
        services.AddScoped<Tutores.EstadoEnlaces>();
        services.AddScoped<Tutores.EmisionEnlaces>();
        services.AddScoped<Avisos.PlanEntregas>();
        services.AddScoped<Avisos.EnviosATutores>();
        services.AddScoped<Avisos.AvisoBoletas>();
        services.AddScoped<Planes.AccesoPro>();
        services.AddScoped<Planes.ProcesadorEventosPago>();
        services.AddScoped<Operacion.AccesoAdmin>();
        services.AddScoped<Operacion.Costeo>();
        return services;
    }
}
