using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace AulaLista.Api.Operacion;

internal static class SaludEndpoints
{
    /// <summary><c>/salud</c> indica que el proceso vive; <c>/salud/lista</c> además revisa la base.</summary>
    public static IEndpointRouteBuilder MapSalud(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/salud", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/salud/lista", new HealthCheckOptions { Predicate = c => c.Tags.Contains("lista") })
            .AllowAnonymous();
        return app;
    }
}
