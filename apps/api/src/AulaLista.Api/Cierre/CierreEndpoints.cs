using AulaLista.Api.Comun;
using AulaLista.Application.Cierre;
using FluentValidation;
using static AulaLista.Api.Comun.Respuestas;

namespace AulaLista.Api.Cierre;

/// <summary>SPEC-04: cierre del periodo, observaciones con IA y estilo del docente.</summary>
internal static class CierreEndpoints
{
    public static RouteGroupBuilder MapCierre(this RouteGroupBuilder api)
    {
        var periodos = api.MapGroup("/periodos").WithTags("Cierre");
        periodos.MapPost("/{id:guid}/cierre", Iniciar).WithName("IniciarCierre")
            .Produces<CierreDto>().ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        periodos.MapGet("/{id:guid}/cierre", Obtener).WithName("ObtenerCierre")
            .Produces<CierreDto>().Produces(StatusCodes.Status404NotFound);
        periodos.MapPost("/{id:guid}/observaciones/generar", Generar).WithName("GenerarObservaciones")
            .Produces<CierreDto>().ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status429TooManyRequests);
        periodos.MapGet("/{id:guid}/observaciones", Listar).WithName("ListarObservaciones")
            .Produces<IReadOnlyList<ObservacionDto>>().Produces(StatusCodes.Status404NotFound);

        var observaciones = api.MapGroup("/observaciones").WithTags("Cierre");
        observaciones.MapPut("/{id:guid}", Editar).WithName("EditarObservacion")
            .Produces<ObservacionDto>().ProducesProblem(StatusCodes.Status409Conflict);
        observaciones.MapPost("/{id:guid}/regenerar", Regenerar).WithName("RegenerarObservacion")
            .Produces<ObservacionDto>().ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status429TooManyRequests);
        observaciones.MapPost("/{id:guid}/aprobar", Aprobar).WithName("AprobarObservacion")
            .Produces<ObservacionDto>().ProducesProblem(StatusCodes.Status409Conflict);
        observaciones.MapPost("/{id:guid}/reabrir", Reabrir).WithName("ReabrirObservacion")
            .Produces<ObservacionDto>().ProducesProblem(StatusCodes.Status409Conflict);

        var estilo = api.MapGroup("/docente").WithTags("Cierre");
        estilo.MapGet("/estilo-observaciones", ObtenerEstilo).WithName("ObtenerEstiloObservaciones").Produces<EstiloDto>();
        estilo.MapPut("/estilo-observaciones", GuardarEstilo).WithName("GuardarEstiloObservaciones")
            .Produces<EstiloDto>().ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        return api;
    }

    private static async Task<IResult> Iniciar(Guid id, IniciarCierreCasoUso caso, CancellationToken ct) =>
        Respuesta(await caso.EjecutarAsync(id, ct));

    private static async Task<IResult> Obtener(Guid id, ObtenerCierreCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, ct) is { } cierre ? TypedResults.Ok(cierre) : TypedResults.NotFound();

    private static async Task<IResult> Generar(Guid id, GenerarObservaciones? cuerpo, GenerarObservacionesCasoUso caso, CancellationToken ct) =>
        Respuesta(await caso.EjecutarAsync(id, cuerpo ?? new GenerarObservaciones(null), ct));

    private static async Task<IResult> Listar(Guid id, ListarObservacionesCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, ct) is { } lista ? TypedResults.Ok(lista) : TypedResults.NotFound();

    private static async Task<IResult> Editar(
        Guid id, EditarObservacion cuerpo, IValidator<EditarObservacion> validador, EditarObservacionCasoUso caso, CancellationToken ct) =>
        await validador.ValidarAsync(cuerpo, ct) ?? Respuesta(await caso.EjecutarAsync(id, cuerpo, ct));

    private static async Task<IResult> Regenerar(
        Guid id, RegenerarObservacion? cuerpo, IValidator<RegenerarObservacion> validador, RegenerarObservacionCasoUso caso, CancellationToken ct)
    {
        cuerpo ??= new RegenerarObservacion(null);
        return await validador.ValidarAsync(cuerpo, ct) ?? Respuesta(await caso.EjecutarAsync(id, cuerpo, ct));
    }

    private static async Task<IResult> Aprobar(
        Guid id, AprobarObservacion? cuerpo, IValidator<AprobarObservacion> validador, AprobarObservacionCasoUso caso, CancellationToken ct)
    {
        cuerpo ??= new AprobarObservacion(null);
        return await validador.ValidarAsync(cuerpo, ct) ?? Respuesta(await caso.EjecutarAsync(id, cuerpo, ct));
    }

    private static async Task<IResult> Reabrir(Guid id, ReabrirObservacionCasoUso caso, CancellationToken ct) =>
        Respuesta(await caso.EjecutarAsync(id, ct));

    private static Task<EstiloDto> ObtenerEstilo(ObtenerEstiloCasoUso caso, CancellationToken ct) => caso.EjecutarAsync(ct);

    private static async Task<IResult> GuardarEstilo(
        GuardarEstilo cuerpo, IValidator<GuardarEstilo> validador, GuardarEstiloCasoUso caso, CancellationToken ct) =>
        await validador.ValidarAsync(cuerpo, ct) ?? Respuesta(await caso.EjecutarAsync(cuerpo, ct));
}
