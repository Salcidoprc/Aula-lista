using AulaLista.Api.Comun;
using AulaLista.Application.Operacion;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using static AulaLista.Api.Comun.Respuestas;

namespace AulaLista.Api.Operacion;

/// <summary>
/// SPEC-09: panel de administración. Toda ruta exige la política <see cref="PoliticaAdmin"/>; para quien no es
/// administrador responde 404, como si no existiera (02-arquitectura, API).
/// </summary>
internal static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdmin(this RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin").WithTags("Administración").RequireAuthorization(PoliticaAdmin.Nombre)
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapGet("/resumen", async (ResumenCasoUso caso, CancellationToken ct) => TypedResults.Ok(await caso.EjecutarAsync(ct)))
            .WithName("ResumenAdmin").Produces<ResumenDto>();
        admin.MapGet("/metricas", async (MetricasCasoUso caso, CancellationToken ct) => TypedResults.Ok(await caso.EjecutarAsync(ct)))
            .WithName("MetricasAdmin").Produces<MetricasDto>();
        admin.MapGet("/docentes", async (DocentesAdminCasoUso caso, CancellationToken ct) => TypedResults.Ok(await caso.EjecutarAsync(ct)))
            .WithName("DocentesAdmin").Produces<DocentesAdminDto>();
        admin.MapGet("/costos", async (CostosCasoUso caso, CancellationToken ct) => TypedResults.Ok(await caso.EjecutarAsync(ct)))
            .WithName("CostosAdmin").Produces<CostosDto>();
        admin.MapGet("/errores", async (ErroresAdminCasoUso caso, CancellationToken ct) => TypedResults.Ok(await caso.EjecutarAsync(ct)))
            .WithName("ErroresAdmin").Produces<IReadOnlyList<ErrorTrabajoDto>>();
        admin.MapGet("/auditoria", async (DateTimeOffset? antesDe, AuditoriaAdminCasoUso caso, CancellationToken ct) =>
                TypedResults.Ok(await caso.EjecutarAsync(antesDe, ct)))
            .WithName("AuditoriaAdmin").Produces<PaginaAuditoriaDto>();
        admin.MapGet("/comportamientos", () => TypedResults.Ok(ComportamientosCasoUso.Ejecutar()))
            .WithName("ComportamientosAdmin").Produces<IReadOnlyList<string>>();

        var tipos = admin.MapGroup("/tipos-rubro");
        tipos.MapGet("/", async (ListarTiposRubroAdminCasoUso caso, CancellationToken ct) => TypedResults.Ok(await caso.EjecutarAsync(ct)))
            .WithName("TiposRubroAdmin").Produces<IReadOnlyList<TipoRubroAdminDto>>();
        tipos.MapPost("/", async (TipoRubroEntrada cuerpo, IValidator<TipoRubroEntrada> validador, CrearTipoRubroCasoUso caso, CancellationToken ct) =>
                await validador.ValidarAsync(cuerpo, ct)
                ?? Creado(await caso.EjecutarAsync(cuerpo, ct), t => $"/api/v1/admin/tipos-rubro/{t.Id}"))
            .WithName("CrearTipoRubro").Produces<TipoRubroAdminDto>(StatusCodes.Status201Created).ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        tipos.MapPut("/{id:guid}", async (
                Guid id, TipoRubroEntrada cuerpo, IValidator<TipoRubroEntrada> validador, EditarTipoRubroCasoUso caso, CancellationToken ct) =>
                await validador.ValidarAsync(cuerpo, ct) ?? Respuesta(await caso.EjecutarAsync(id, cuerpo, ct)))
            .WithName("EditarTipoRubro").Produces<TipoRubroAdminDto>().ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        return api;
    }
}

/// <summary>
/// La política <c>admin</c> corre antes de leer el cuerpo, así que nadie distingue una ruta del panel por sus errores de
/// validación. <see cref="AccesoAdmin"/> además asume al administrador para <c>app.admin_id</c>.
/// </summary>
internal static class PoliticaAdmin
{
    public const string Nombre = "admin";

    public static AuthorizationPolicy Crear() => new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddRequirements(new Requisito())
        .Build();

    internal sealed class Requisito : IAuthorizationRequirement;

    internal sealed class Manejador(IHttpContextAccessor accesor) : AuthorizationHandler<Requisito>
    {
        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, Requisito requirement)
        {
            if (accesor.HttpContext is { } http
                && await http.RequestServices.GetRequiredService<AccesoAdmin>().ComprobarAsync(http.RequestAborted))
            {
                context.Succeed(requirement);
            }
        }
    }

    /// <summary>Un rechazo de la política <c>admin</c> responde 404 en lugar de 403 (SPEC-09).</summary>
    internal sealed class Resultado : IAuthorizationMiddlewareResultHandler
    {
        private readonly AuthorizationMiddlewareResultHandler _porDefecto = new();

        public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
        {
            if (authorizeResult.Forbidden && policy.Requirements.OfType<Requisito>().Any())
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }

            return _porDefecto.HandleAsync(next, context, policy, authorizeResult);
        }
    }
}
