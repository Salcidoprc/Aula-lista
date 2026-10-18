using AulaLista.Api.Comun;
using AulaLista.Application.Comun;
using AulaLista.Application.Tutores;
using static AulaLista.Api.Comun.Respuestas;

namespace AulaLista.Api.Tutores;

/// <summary>SPEC-06: canje del enlace, lo publicado de los hijos del tutor y el envío del enlace desde la ficha.</summary>
internal static class TutoresEndpoints
{
    public const string PoliticaCanje = "canje_enlace";

    public static RouteGroupBuilder MapTutores(this RouteGroupBuilder api)
    {
        var tutor = api.MapGroup("/tutor").WithTags("Portal del tutor");
        tutor.MapPost("/sesion", Canjear).AllowAnonymous().RequireRateLimiting(PoliticaCanje).WithName("CanjearEnlaceTutor")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status401Unauthorized);

        var portal = tutor.MapGroup(string.Empty).RequireAuthorization(AutenticacionTutor.Politica);
        portal.MapGet("/alumnos", MisHijos).WithName("ListarHijosTutor").Produces<MisHijosDto>();
        portal.MapGet("/alumnos/{id:guid}", Detalle).WithName("ObtenerHijoTutor")
            .Produces<DetalleHijoDto>().Produces(StatusCodes.Status404NotFound);

        api.MapGroup("/alumnos").WithTags("Alumnos")
            .MapPost("/{id:guid}/tutores/{tutorId:guid}/enlace", Enviar).WithName("EnviarEnlaceTutor")
            .Produces<EnlaceDto>().Produces(StatusCodes.Status404NotFound);
        return api;
    }

    private static async Task<IResult> Canjear(SolicitudCanje cuerpo, CanjearEnlaceCasoUso caso, HttpContext http, CancellationToken ct)
    {
        http.Request.Cookies.TryGetValue(AutenticacionTutor.Cookie, out var actual);
        var resultado = await caso.EjecutarAsync(cuerpo.Token, actual, ct);
        if (resultado.EsFallo)
        {
            return resultado.Error.AProblemaHttp();
        }

        AutenticacionTutor.EscribirCookie(http, resultado.Valor.Token, resultado.Valor.ExpiraEn);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> MisHijos(MisHijosCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(ct) is { } hijos ? TypedResults.Ok(hijos) : TypedResults.NotFound();

    private static async Task<IResult> Detalle(Guid id, DetalleHijoCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, ct) is { } hijo ? TypedResults.Ok(hijo) : TypedResults.NotFound();

    private static async Task<IResult> Enviar(Guid id, Guid tutorId, EnviarEnlace? cuerpo, EnviarEnlaceCasoUso caso, CancellationToken ct) =>
        Respuesta(await caso.EjecutarAsync(id, tutorId, cuerpo ?? new EnviarEnlace(false), ct));
}

public sealed record SolicitudCanje(string Token);
