using AulaLista.Api.Comun;
using AulaLista.Application.Evaluacion;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using static AulaLista.Api.Comun.Respuestas;

namespace AulaLista.Api.Evaluacion;

/// <summary>SPEC-03: tipos de rubro, rubros y criterios, actividades, calificaciones y la cola sin conexión.</summary>
internal static class EvaluacionEndpoints
{
    public static RouteGroupBuilder MapEvaluacion(this RouteGroupBuilder api)
    {
        api.MapGet("/tipos-rubro", (ListarTiposRubroCasoUso caso, CancellationToken ct) => caso.EjecutarAsync(ct))
            .WithTags("Rubros").WithName("ListarTiposRubro");

        var periodos = api.MapGroup("/periodos").WithTags("Rubros");
        periodos.MapGet("/{id:guid}/rubros", ObtenerRubros).WithName("ObtenerRubros")
            .Produces<RubrosDelPeriodoDto>().Produces(StatusCodes.Status404NotFound);
        periodos.MapPut("/{id:guid}/rubros", ReemplazarRubros).WithName("ReemplazarRubros")
            .Produces<RubrosDelPeriodoDto>().ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        periodos.MapPost("/{id:guid}/rubros/copiar", CopiarRubros).WithName("CopiarRubros")
            .Produces<RubrosDelPeriodoDto>().ProducesProblem(StatusCodes.Status409Conflict);
        periodos.MapGet("/{id:guid}/calificaciones", Matriz).WithTags("Calificaciones").WithName("ObtenerMatrizCalificaciones")
            .Produces<MatrizCalificacionesDto>().Produces(StatusCodes.Status404NotFound);

        var rubros = api.MapGroup("/rubros").WithTags("Actividades");
        rubros.MapGet("/{id:guid}/actividades", ListarActividades).WithName("ListarActividades")
            .Produces<IReadOnlyList<ActividadDto>>().Produces(StatusCodes.Status404NotFound);
        rubros.MapPost("/{id:guid}/actividades", CrearActividad).WithName("CrearActividad")
            .Produces<ActividadDto>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        var actividades = api.MapGroup("/actividades").WithTags("Actividades");
        actividades.MapPut("/{id:guid}", EditarActividad).WithName("EditarActividad")
            .Produces<ActividadDto>().ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        actividades.MapDelete("/{id:guid}", BorrarActividad).WithName("BorrarActividad")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status409Conflict);

        api.MapPut("/calificaciones", GuardarCalificaciones).WithTags("Calificaciones").WithName("GuardarCalificaciones")
            .Produces<ResultadoCalificaciones>().ProducesValidationProblem();
        api.MapPost("/sincronizacion", Sincronizar).WithTags("Calificaciones").WithName("Sincronizar")
            .Produces<ResultadoCalificaciones>().ProducesValidationProblem();
        return api;
    }

    private static async Task<IResult> ObtenerRubros(Guid id, ObtenerRubrosCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, ct) is { } rubros ? TypedResults.Ok(rubros) : TypedResults.NotFound();

    private static async Task<IResult> ReemplazarRubros(
        Guid id, ReemplazarRubros cuerpo, IValidator<ReemplazarRubros> validador, ReemplazarRubrosCasoUso caso, CancellationToken ct) =>
        await validador.ValidarAsync(cuerpo, ct) ?? Respuesta(await caso.EjecutarAsync(id, cuerpo, ct));

    private static async Task<IResult> CopiarRubros(Guid id, CopiarRubros cuerpo, CopiarRubrosCasoUso caso, CancellationToken ct) =>
        Respuesta(await caso.EjecutarAsync(id, cuerpo, ct));

    private static async Task<IResult> Matriz(Guid id, MatrizCalificacionesCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, ct) is { } matriz ? TypedResults.Ok(matriz) : TypedResults.NotFound();

    private static async Task<IResult> ListarActividades(Guid id, ListarActividadesCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, ct) is { } lista ? TypedResults.Ok(lista) : TypedResults.NotFound();

    private static async Task<IResult> CrearActividad(
        Guid id, DatosActividad cuerpo, IValidator<DatosActividad> validador, CrearActividadCasoUso caso, CancellationToken ct) =>
        await validador.ValidarAsync(cuerpo, ct) ?? Creado(await caso.EjecutarAsync(id, cuerpo, ct), a => $"/api/v1/actividades/{a.Id}");

    private static async Task<IResult> EditarActividad(
        Guid id, DatosActividad cuerpo, IValidator<DatosActividad> validador, EditarActividadCasoUso caso, CancellationToken ct) =>
        await validador.ValidarAsync(cuerpo, ct) ?? Respuesta(await caso.EjecutarAsync(id, cuerpo, ct));

    private static async Task<IResult> BorrarActividad(Guid id, bool? confirmar, BorrarActividadCasoUso caso, CancellationToken ct) =>
        SinContenido(await caso.EjecutarAsync(id, confirmar ?? false, ct));

    private static async Task<IResult> GuardarCalificaciones(
        GuardarCalificaciones cuerpo,
        [FromHeader(Name = "Idempotency-Key")] Guid? claveIdempotencia,
        IValidator<GuardarCalificaciones> validador,
        GuardarCalificacionesCasoUso caso,
        CancellationToken ct) =>
        await validador.ValidarAsync(cuerpo, ct) ?? TypedResults.Ok(await caso.EjecutarAsync(cuerpo, claveIdempotencia, ct));

    private static async Task<IResult> Sincronizar(
        SincronizarCambios cuerpo, IValidator<SincronizarCambios> validador, GuardarCalificacionesCasoUso caso, CancellationToken ct) =>
        await validador.ValidarAsync(cuerpo, ct) ?? TypedResults.Ok(await caso.EjecutarAsync(cuerpo, ct));
}
