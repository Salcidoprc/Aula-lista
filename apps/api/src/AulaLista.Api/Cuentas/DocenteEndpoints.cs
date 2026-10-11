using AulaLista.Api.Comun;
using AulaLista.Application.Comun;
using AulaLista.Application.Cuentas;
using AulaLista.Domain.Cuentas;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AulaLista.Api.Cuentas;

/// <summary>SPEC-01: perfil, aceptaciones legales, exportación y eliminación de la cuenta.</summary>
internal static class DocenteEndpoints
{
    public static RouteGroupBuilder MapDocente(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/docente").WithTags("Docente");

        g.MapGet("/perfil", ObtenerPerfil).WithName("ObtenerPerfil");
        g.MapPut("/perfil", ActualizarPerfil).WithName("ActualizarPerfil");
        g.MapPost("/aceptaciones", RegistrarAceptacion).WithName("RegistrarAceptacion");
        g.MapPost("/exportacion", SolicitarExportacion).WithName("SolicitarExportacion");
        g.MapGet("/exportaciones/{id:guid}", ObtenerExportacion).WithName("ObtenerExportacion");
        g.MapGet("/exportaciones/{id:guid}/{formato}", DescargarExportacion).WithName("DescargarExportacion")
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream");
        g.MapDelete("/", SolicitarEliminacion).WithName("SolicitarEliminacion");
        return api;
    }

    private static async Task<Results<Ok<PerfilDocente>, NotFound>> ObtenerPerfil(ObtenerPerfilCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(ct) is { } perfil ? TypedResults.Ok(perfil) : TypedResults.NotFound();

    private static async Task<IResult> ActualizarPerfil(
        ActualizarPerfil cuerpo, IValidator<ActualizarPerfil> validador, ActualizarPerfilCasoUso caso, CancellationToken ct)
    {
        if (await validador.ValidarAsync(cuerpo, ct) is { } invalido)
        {
            return invalido;
        }

        var resultado = await caso.EjecutarAsync(cuerpo, ct);
        return resultado.EsExito ? TypedResults.NoContent() : resultado.Error.AProblemaHttp();
    }

    private static async Task<IResult> RegistrarAceptacion(RegistrarAceptacion cuerpo, RegistrarAceptacionCasoUso caso, CancellationToken ct)
    {
        var resultado = await caso.EjecutarAsync(cuerpo, ct);
        return resultado.EsExito ? TypedResults.NoContent() : resultado.Error.AProblemaHttp();
    }

    private static async Task<Accepted<RespuestaExportacion>> SolicitarExportacion(SolicitarExportacionCasoUso caso, CancellationToken ct)
    {
        var id = await caso.EjecutarAsync(ct);
        return TypedResults.Accepted((string?)null, new RespuestaExportacion(id, EstadoExportacion.Pendiente, null));
    }

    private static async Task<Results<Ok<RespuestaExportacion>, NotFound>> ObtenerExportacion(
        Guid id, IRepositorioDocentes docentes, CancellationToken ct) =>
        await docentes.ExportacionPorIdAsync(id, ct) is { } e
            ? TypedResults.Ok(new RespuestaExportacion(e.Id, e.Estado, e.ExpiraEn))
            : TypedResults.NotFound();

    private static async Task<IResult> DescargarExportacion(
        Guid id, string formato, ObtenerArchivoExportacionCasoUso caso, IAlmacenArchivos almacen, CancellationToken ct)
    {
        var resultado = await caso.EjecutarAsync(id, formato, ct);
        if (resultado.EsFallo)
        {
            return resultado.Error.AProblemaHttp();
        }

        var archivo = resultado.Valor;
        var flujo = await almacen.AbrirAsync(archivo.Ruta, ct);
        return flujo is null
            ? ErroresCuentas.ExportacionNoDisponible.AProblemaHttp()
            : TypedResults.File(flujo, archivo.TipoContenido, archivo.Nombre);
    }

    private static async Task<IResult> SolicitarEliminacion(
        [Microsoft.AspNetCore.Mvc.FromBody] SolicitarEliminacion cuerpo, SolicitarEliminacionCasoUso caso, HttpContext http, CancellationToken ct)
    {
        var resultado = await caso.EjecutarAsync(cuerpo, ct);
        if (resultado.EsFallo)
        {
            return resultado.Error.AProblemaHttp();
        }

        http.Response.Cookies.Delete(CuentasEndpoints.CookieSesion, new CookieOptions { Path = CuentasEndpoints.RutaCookie, Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict });
        return TypedResults.NoContent();
    }
}

public sealed record RespuestaExportacion(Guid Id, string Estado, DateTimeOffset? ExpiraEn);
