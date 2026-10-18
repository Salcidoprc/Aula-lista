using AulaLista.Api.Comun;
using AulaLista.Api.Tutores;
using AulaLista.Application.Boletas;
using AulaLista.Application.Comun;
using AulaLista.Application.Tutores;
using static AulaLista.Api.Comun.Respuestas;

namespace AulaLista.Api.Boletas;

/// <summary>SPEC-05: boletas, sus versiones, el PDF del grupo y la exportación a Excel.</summary>
internal static class BoletasEndpoints
{
    private const string Pdf = "application/pdf";
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static RouteGroupBuilder MapBoletas(this RouteGroupBuilder api)
    {
        var periodos = api.MapGroup("/periodos").WithTags("Boletas");
        periodos.MapGet("/{id:guid}/boletas", Listar).WithName("ListarBoletas")
            .Produces<BoletasDto>().Produces(StatusCodes.Status404NotFound);
        periodos.MapPost("/{id:guid}/boletas/generar", Generar).WithName("GenerarBoletas")
            .Produces<BoletasDto>().ProducesProblem(StatusCodes.Status409Conflict);
        periodos.MapPost("/{id:guid}/boletas/publicar", Publicar).WithName("PublicarBoletas")
            .Produces<BoletasDto>().ProducesProblem(StatusCodes.Status409Conflict);
        periodos.MapGet("/{id:guid}/boletas/pdf", PdfDelGrupo).WithName("DescargarBoletasDelGrupo")
            .Produces(StatusCodes.Status200OK, contentType: Pdf).ProducesProblem(StatusCodes.Status409Conflict);
        periodos.MapGet("/{id:guid}/exportacion.xlsx", Exportar).WithName("ExportarPeriodo")
            .Produces(StatusCodes.Status200OK, contentType: Xlsx).Produces(StatusCodes.Status404NotFound);

        var boletas = api.MapGroup("/boletas").WithTags("Boletas");
        boletas.MapGet("/{id:guid}/pdf", PdfDeBoleta).WithName("DescargarBoleta")
            .RequireAuthorization(AutenticacionTutor.PoliticaDocenteOTutor)
            .Produces(StatusCodes.Status200OK, contentType: Pdf).ProducesProblem(StatusCodes.Status409Conflict);
        boletas.MapPost("/{id:guid}/corregir", Corregir).WithName("CorregirBoleta")
            .Produces<BoletasDto>().ProducesProblem(StatusCodes.Status409Conflict);
        return api;
    }

    private static async Task<IResult> Listar(Guid id, ObtenerBoletasCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, ct) is { } boletas ? TypedResults.Ok(boletas) : TypedResults.NotFound();

    private static async Task<IResult> Generar(Guid id, SeleccionAlumnos? cuerpo, GenerarBoletasCasoUso caso, CancellationToken ct) =>
        Respuesta(await caso.EjecutarAsync(id, cuerpo ?? new SeleccionAlumnos(null), ct));

    private static async Task<IResult> Publicar(Guid id, SeleccionAlumnos? cuerpo, PublicarBoletasCasoUso caso, CancellationToken ct) =>
        Respuesta(await caso.EjecutarAsync(id, cuerpo ?? new SeleccionAlumnos(null), ct));

    private static async Task<IResult> Corregir(Guid id, CorregirBoletaCasoUso caso, CancellationToken ct) =>
        Respuesta(await caso.EjecutarAsync(id, ct));

    private static async Task<IResult> PdfDelGrupo(Guid id, PdfDelGrupoCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, ct) switch
        {
            null => TypedResults.NotFound(),
            { EsExito: true } r => TypedResults.File(r.Valor.Contenido!, Pdf, r.Valor.Nombre),
            var r => r.Error.AProblemaHttp(),
        };

    /// <summary>El docente baja cualquier boleta suya; el tutor, solo la publicada de un hijo suyo (SPEC-06).</summary>
    private static async Task<IResult> PdfDeBoleta(
        Guid id, IContextoUsuario usuario, ObtenerPdfBoletaCasoUso caso, PdfBoletaTutorCasoUso delTutor, IAlmacenArchivos almacen,
        CancellationToken ct)
    {
        if (usuario.DocenteId is null)
        {
            return await delTutor.EjecutarAsync(id, ct) is { Ruta: { } ruta } archivo
                && await almacen.AbrirAsync(ruta, ct) is { } contenido
                ? TypedResults.File(contenido, Pdf, archivo.Nombre)
                : TypedResults.NotFound();
        }

        var resultado = await caso.EjecutarAsync(id, ct);
        if (resultado is null)
        {
            return TypedResults.NotFound();
        }

        if (resultado.EsFallo)
        {
            return resultado.Error.AProblemaHttp();
        }

        var flujo = await almacen.AbrirAsync(resultado.Valor.Ruta!, ct);
        return flujo is null ? TypedResults.NotFound() : TypedResults.File(flujo, Pdf, resultado.Valor.Nombre);
    }

    private static async Task<IResult> Exportar(Guid id, ExportarPeriodoCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, ct) is { } archivo ? TypedResults.File(archivo.Contenido!, Xlsx, archivo.Nombre) : TypedResults.NotFound();
}
