using AulaLista.Api.Comun;
using AulaLista.Application.Grupos;
using AulaLista.Domain.Comun;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using static AulaLista.Api.Comun.Respuestas;

namespace AulaLista.Api.Grupos;

/// <summary>SPEC-02: ciclo escolar y unidades, grupos, periodos, alumnos, tutores e importación.</summary>
internal static class GruposEndpoints
{
    public static RouteGroupBuilder MapGrupos(this RouteGroupBuilder api)
    {
        var ciclos = api.MapGroup("/ciclos").WithTags("Ciclos");
        ciclos.MapGet("/", (ListarCiclosCasoUso caso, CancellationToken ct) => caso.EjecutarAsync(ct)).WithName("ListarCiclos");
        ciclos.MapPost("/", CrearCiclo).WithName("CrearCiclo")
            .Produces<CicloDto>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        ciclos.MapGet("/{id:guid}", ObtenerCiclo).WithName("ObtenerCiclo").Produces<CicloDto>().Produces(StatusCodes.Status404NotFound);
        ciclos.MapPut("/{id:guid}", EditarCiclo).WithName("EditarCiclo").Produces<CicloDto>().ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        ciclos.MapPut("/{id:guid}/unidades", ReemplazarUnidades).WithName("ReemplazarUnidades")
            .Produces<CicloDto>().ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        var grupos = api.MapGroup("/grupos").WithTags("Grupos");
        grupos.MapGet("/", (bool? archivados, ListarGruposCasoUso caso, CancellationToken ct) => caso.EjecutarAsync(archivados ?? false, ct))
            .WithName("ListarGrupos");
        grupos.MapPost("/", CrearGrupo).WithName("CrearGrupo")
            .Produces<GrupoDto>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        grupos.MapPost("/lote", CrearGruposLote).WithName("CrearGruposLote")
            .Produces<IReadOnlyList<GrupoDto>>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        grupos.MapGet("/{id:guid}", ObtenerGrupo).WithName("ObtenerGrupo").Produces<GrupoDto>().Produces(StatusCodes.Status404NotFound);
        grupos.MapPut("/{id:guid}", EditarGrupo).WithName("EditarGrupo").Produces<GrupoDto>().ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        grupos.MapDelete("/{id:guid}", BorrarGrupo).WithName("BorrarGrupo")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status409Conflict);
        grupos.MapPost("/{id:guid}/archivar", ArchivarGrupo).WithName("ArchivarGrupo").Produces(StatusCodes.Status204NoContent);
        grupos.MapGet("/{id:guid}/periodos", Periodos).WithName("ListarPeriodos").Produces<IReadOnlyList<PeriodoDto>>();
        grupos.MapGet("/{id:guid}/alumnos", ListarAlumnos).WithName("ListarAlumnos").Produces<IReadOnlyList<AlumnoDto>>();
        grupos.MapPost("/{id:guid}/alumnos", AgregarAlumno).WithName("AgregarAlumno")
            .Produces<AlumnoDto>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        grupos.MapPost("/{id:guid}/importaciones", CrearImportacion).WithName("CrearImportacion").DisableAntiforgery()
            .Accepts<FormularioImportacion>("multipart/form-data")
            .Produces<VistaPreviaImportacion>().ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        var alumnos = api.MapGroup("/alumnos").WithTags("Alumnos");
        alumnos.MapGet("/{id:guid}", ObtenerAlumno).WithName("ObtenerAlumno").Produces<AlumnoDto>().Produces(StatusCodes.Status404NotFound);
        alumnos.MapPut("/{id:guid}", EditarAlumno).WithName("EditarAlumno").Produces<AlumnoDto>().ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        alumnos.MapDelete("/{id:guid}", DarDeBaja).WithName("DarDeBajaAlumno").Produces(StatusCodes.Status204NoContent);
        alumnos.MapGet("/{id:guid}/tutores", ListarTutores).WithName("ListarTutores").Produces<IReadOnlyList<TutorDto>>();
        alumnos.MapPost("/{id:guid}/tutores", VincularTutor).WithName("VincularTutor")
            .Produces<TutorVinculado>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        alumnos.MapPut("/{id:guid}/tutores/{tutorId:guid}", EditarTutor).WithName("EditarTutor").Produces<TutorDto>();
        alumnos.MapDelete("/{id:guid}/tutores/{tutorId:guid}", DesvincularTutor).WithName("DesvincularTutor").Produces(StatusCodes.Status204NoContent);

        var importaciones = api.MapGroup("/importaciones").WithTags("Importaciones");
        importaciones.MapPost("/{id:guid}/confirmar", ConfirmarImportacion).WithName("ConfirmarImportacion")
            .Produces<ResultadoImportacion>().ProducesProblem(StatusCodes.Status409Conflict);
        importaciones.MapGet("/plantilla", (IHojasDeCalculo hojas) => TypedResults.File(
                hojas.Plantilla(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "plantilla-alumnos.xlsx"))
            .WithName("DescargarPlantilla")
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream");
        return api;
    }

    private static async Task<IResult> CrearCiclo(CrearCiclo cuerpo, IValidator<CrearCiclo> validador, CrearCicloCasoUso caso, CancellationToken ct) =>
        await validador.ValidarAsync(cuerpo, ct) ?? Creado(await caso.EjecutarAsync(cuerpo, ct), c => $"/api/v1/ciclos/{c.Id}");

    private static async Task<IResult> ObtenerCiclo(Guid id, IRepositorioGrupos repositorio, CancellationToken ct) =>
        await repositorio.CicloAsync(id, ct) is { } ciclo ? TypedResults.Ok(CicloDto.De(ciclo)) : TypedResults.NotFound();

    private static async Task<IResult> EditarCiclo(
        Guid id, EditarCiclo cuerpo, IValidator<EditarCiclo> validador, EditarCicloCasoUso caso, CancellationToken ct) =>
        await validador.ValidarAsync(cuerpo, ct) ?? Respuesta(await caso.EjecutarAsync(id, cuerpo, ct));

    private static async Task<IResult> ReemplazarUnidades(
        Guid id, ReemplazarUnidades cuerpo, IValidator<ReemplazarUnidades> validador, ReemplazarUnidadesCasoUso caso, CancellationToken ct) =>
        await validador.ValidarAsync(cuerpo, ct) ?? Respuesta(await caso.EjecutarAsync(id, cuerpo, ct));

    private static async Task<IResult> CrearGrupo(CrearGrupo cuerpo, IValidator<CrearGrupo> validador, CrearGruposCasoUso caso, CancellationToken ct)
    {
        if (await validador.ValidarAsync(cuerpo, ct) is { } invalido)
        {
            return invalido;
        }

        var resultado = await caso.EjecutarAsync(cuerpo, ct);
        return resultado.EsExito
            ? TypedResults.Created($"/api/v1/grupos/{resultado.Valor[0].Id}", resultado.Valor[0])
            : resultado.Error.AProblemaHttp();
    }

    private static async Task<IResult> CrearGruposLote(
        CrearGruposLote cuerpo, IValidator<CrearGruposLote> validador, CrearGruposCasoUso caso, CancellationToken ct) =>
        await validador.ValidarAsync(cuerpo, ct) ?? Creado(await caso.EjecutarAsync(cuerpo, ct), _ => "/api/v1/grupos");

    private static async Task<IResult> ObtenerGrupo(Guid id, ObtenerGrupoCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, ct) is { } grupo ? TypedResults.Ok(grupo) : TypedResults.NotFound();

    private static async Task<IResult> EditarGrupo(
        Guid id, EditarGrupo cuerpo, IValidator<EditarGrupo> validador, EditarGrupoCasoUso caso, ObtenerGrupoCasoUso obtener, CancellationToken ct)
    {
        if (await validador.ValidarAsync(cuerpo, ct) is { } invalido)
        {
            return invalido;
        }

        return await caso.EjecutarAsync(id, cuerpo, ct) switch
        {
            null => TypedResults.NotFound(),
            { EsFallo: true } fallo => fallo.Error.AProblemaHttp(),
            _ => TypedResults.Ok(await obtener.EjecutarAsync(id, ct)),
        };
    }

    private static async Task<IResult> BorrarGrupo(Guid id, BorrarGrupoCasoUso caso, CancellationToken ct) =>
        SinContenido(await caso.EjecutarAsync(id, ct));

    private static async Task<IResult> ArchivarGrupo(Guid id, ArchivarGrupoCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, ct) ? TypedResults.NoContent() : TypedResults.NotFound();

    private static async Task<IResult> Periodos(Guid id, PeriodosDeGrupoCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, ct) is { } periodos ? TypedResults.Ok(periodos) : TypedResults.NotFound();

    private static async Task<IResult> ListarAlumnos(Guid id, ListarAlumnosCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, ct) is { } alumnos ? TypedResults.Ok(alumnos) : TypedResults.NotFound();

    private static async Task<IResult> AgregarAlumno(
        Guid id, AgregarAlumno cuerpo, IValidator<AgregarAlumno> validador, AgregarAlumnoCasoUso caso, CancellationToken ct) =>
        await validador.ValidarAsync(cuerpo, ct) ?? Creado(await caso.EjecutarAsync(id, cuerpo, ct), a => $"/api/v1/alumnos/{a.Id}");

    private static async Task<IResult> ObtenerAlumno(Guid id, ObtenerAlumnoCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, ct) is { } alumno ? TypedResults.Ok(alumno) : TypedResults.NotFound();

    private static async Task<IResult> EditarAlumno(
        Guid id, EditarAlumno cuerpo, IValidator<EditarAlumno> validador, EditarAlumnoCasoUso caso, CancellationToken ct) =>
        await validador.ValidarAsync(cuerpo, ct) ?? Respuesta(await caso.EjecutarAsync(id, cuerpo, ct));

    private static async Task<IResult> DarDeBaja(Guid id, DarDeBajaAlumnoCasoUso caso, CancellationToken ct) =>
        SinContenido(await caso.EjecutarAsync(id, ct));

    private static async Task<IResult> ListarTutores(Guid id, ListarTutoresCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, ct) is { } tutores ? TypedResults.Ok(tutores) : TypedResults.NotFound();

    private static async Task<IResult> VincularTutor(
        Guid id, VincularTutor cuerpo, IValidator<VincularTutor> validador, VincularTutorCasoUso caso, CancellationToken ct) =>
        await validador.ValidarAsync(cuerpo, ct)
            ?? Creado(await caso.EjecutarAsync(id, cuerpo, ct), t => $"/api/v1/alumnos/{id}/tutores/{t.Tutor.Id}");

    private static async Task<IResult> EditarTutor(
        Guid id, Guid tutorId, EditarTutor cuerpo, IValidator<EditarTutor> validador, EditarTutorCasoUso caso, CancellationToken ct) =>
        await validador.ValidarAsync(cuerpo, ct) ?? Respuesta(await caso.EjecutarAsync(id, tutorId, cuerpo, ct));

    private static async Task<IResult> DesvincularTutor(Guid id, Guid tutorId, DesvincularTutorCasoUso caso, CancellationToken ct) =>
        SinContenido(await caso.EjecutarAsync(id, tutorId, ct));

    private static async Task<IResult> CrearImportacion(
        Guid id, [FromForm] FormularioImportacion formulario, CrearImportacionCasoUso caso, CancellationToken ct)
    {
        var archivo = formulario.Archivo;
        await using var flujo = archivo?.OpenReadStream();
        var fuente = new FuenteImportacion(flujo, archivo?.FileName, archivo?.Length ?? 0, formulario.Texto);
        return Respuesta(await caso.EjecutarAsync(id, fuente, ct));
    }

    private static async Task<IResult> ConfirmarImportacion(
        Guid id, ConfirmarImportacion cuerpo, IValidator<ConfirmarImportacion> validador, ConfirmarImportacionCasoUso caso, CancellationToken ct) =>
        await validador.ValidarAsync(cuerpo, ct) ?? Respuesta(await caso.EjecutarAsync(id, cuerpo, ct));
}

/// <summary>Un archivo .xlsx o .csv (RF-013), o el texto pegado con un alumno por línea (RF-014).</summary>
public sealed class FormularioImportacion
{
    public IFormFile? Archivo { get; init; }

    public string? Texto { get; init; }
}
