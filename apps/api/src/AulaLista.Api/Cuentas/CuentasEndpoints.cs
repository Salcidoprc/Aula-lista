using AulaLista.Api.Comun;
using AulaLista.Application.Cuentas;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace AulaLista.Api.Cuentas;

/// <summary>SPEC-01: registro, verificación, sesión y recuperación de contraseña.</summary>
internal static class CuentasEndpoints
{
    public const string CookieSesion = "aula_lista_sesion";
    public const string RutaCookie = "/api/v1/cuentas";
    public const string PoliticaInicioSesion = "inicio_sesion";

    public static RouteGroupBuilder MapCuentas(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/cuentas").WithTags("Cuentas");

        g.MapPost("/registro", Registrar).AllowAnonymous().WithName("Registrar")
            .Produces<RespuestaSesion>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status409Conflict);
        g.MapPost("/verificar-correo", VerificarCorreo).AllowAnonymous().WithName("VerificarCorreo");
        g.MapPost("/verificar-correo/reenviar", ReenviarVerificacion).WithName("ReenviarVerificacion");
        g.MapPost("/sesion", IniciarSesion).AllowAnonymous().RequireRateLimiting(PoliticaInicioSesion).WithName("IniciarSesion")
            .Produces<RespuestaSesion>().ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status429TooManyRequests);
        g.MapPost("/sesion/renovar", Renovar).AllowAnonymous().WithName("RenovarSesion")
            .Produces<RespuestaSesion>().ProducesProblem(StatusCodes.Status401Unauthorized);
        g.MapDelete("/sesion", CerrarSesion).AllowAnonymous().WithName("CerrarSesion");
        g.MapDelete("/sesiones", CerrarTodas).WithName("CerrarTodasLasSesiones");
        g.MapPost("/recuperar", Recuperar).AllowAnonymous().WithName("SolicitarRecuperacion");
        g.MapPost("/restablecer", Restablecer).AllowAnonymous().WithName("RestablecerContrasena");
        g.MapPut("/contrasena", CambiarContrasena).WithName("CambiarContrasena");
        return api;
    }

    private static async Task<IResult> Registrar(
        RegistrarCuenta cuerpo, IValidator<RegistrarCuenta> validador, RegistrarCuentaCasoUso caso,
        HttpContext http, CancellationToken ct)
    {
        if (await validador.ValidarAsync(cuerpo, ct) is { } invalido)
        {
            return invalido;
        }

        var resultado = await caso.EjecutarAsync(cuerpo, ct);
        return resultado.EsExito
            ? Sesion(http, resultado.Valor, StatusCodes.Status201Created)
            : resultado.Error.AProblemaHttp();
    }

    private static async Task<IResult> VerificarCorreo(SolicitudToken cuerpo, VerificarCorreoCasoUso caso, CancellationToken ct)
    {
        var resultado = await caso.EjecutarAsync(cuerpo.Token, ct);
        return resultado.EsExito ? TypedResults.NoContent() : resultado.Error.AProblemaHttp();
    }

    private static async Task<IResult> ReenviarVerificacion(ReenviarVerificacionCasoUso caso, CancellationToken ct)
    {
        var resultado = await caso.EjecutarAsync(ct);
        return resultado.EsExito ? TypedResults.Accepted((string?)null) : resultado.Error.AProblemaHttp();
    }

    private static async Task<IResult> IniciarSesion(
        IniciarSesion cuerpo, IValidator<IniciarSesion> validador, IniciarSesionCasoUso caso,
        HttpContext http, CancellationToken ct)
    {
        if (await validador.ValidarAsync(cuerpo, ct) is { } invalido)
        {
            return invalido;
        }

        var resultado = await caso.EjecutarAsync(cuerpo, ct);
        return resultado.EsExito ? Sesion(http, resultado.Valor) : resultado.Error.AProblemaHttp();
    }

    private static async Task<IResult> Renovar(RenovarSesionCasoUso caso, HttpContext http, CancellationToken ct)
    {
        var resultado = await caso.EjecutarAsync(http.Request.Cookies[CookieSesion], ct);
        if (resultado.EsFallo)
        {
            BorrarCookie(http);
            return resultado.Error.AProblemaHttp();
        }

        return Sesion(http, resultado.Valor);
    }

    private static async Task<NoContent> CerrarSesion(CerrarSesionCasoUso caso, HttpContext http, CancellationToken ct)
    {
        await caso.EjecutarAsync(http.Request.Cookies[CookieSesion], ct);
        BorrarCookie(http);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> CerrarTodas(CerrarTodasLasSesionesCasoUso caso, HttpContext http, CancellationToken ct)
    {
        await caso.EjecutarAsync(ct);
        BorrarCookie(http);
        return TypedResults.NoContent();
    }

    private static async Task<Accepted> Recuperar(SolicitudCorreo cuerpo, SolicitarRecuperacionCasoUso caso, CancellationToken ct)
    {
        await caso.EjecutarAsync(cuerpo.Correo, ct);
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<IResult> Restablecer(
        RestablecerContrasena cuerpo, IValidator<RestablecerContrasena> validador, RestablecerContrasenaCasoUso caso,
        CancellationToken ct)
    {
        if (await validador.ValidarAsync(cuerpo, ct) is { } invalido)
        {
            return invalido;
        }

        var resultado = await caso.EjecutarAsync(cuerpo, ct);
        return resultado.EsExito ? TypedResults.NoContent() : resultado.Error.AProblemaHttp();
    }

    private static async Task<IResult> CambiarContrasena(
        CambiarContrasena cuerpo, IValidator<CambiarContrasena> validador, CambiarContrasenaCasoUso caso,
        CancellationToken ct)
    {
        if (await validador.ValidarAsync(cuerpo, ct) is { } invalido)
        {
            return invalido;
        }

        var resultado = await caso.EjecutarAsync(cuerpo, ct);
        return resultado.EsExito ? TypedResults.NoContent() : resultado.Error.AProblemaHttp();
    }

    /// <summary>
    /// Devuelve el token de acceso en el cuerpo y deja el de actualización en una cookie
    /// <c>HttpOnly</c>, <c>Secure</c>, <c>SameSite=Strict</c> limitada a /api/v1/cuentas.
    /// </summary>
    internal static IResult Sesion(HttpContext http, SesionEmitida sesion, int estado = StatusCodes.Status200OK)
    {
        EscribirCookie(http, sesion);
        var cuerpo = new RespuestaSesion(sesion.DocenteId, sesion.TokenAcceso, sesion.AccesoExpiraEn);
        return estado == StatusCodes.Status201Created
            ? TypedResults.Created((string?)null, cuerpo)
            : TypedResults.Ok(cuerpo);
    }

    internal static void EscribirCookie(HttpContext http, SesionEmitida sesion) =>
        http.Response.Cookies.Append(CookieSesion, sesion.TokenActualizacion, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RutaCookie,
            Expires = sesion.ActualizacionExpiraEn,
            IsEssential = true,
        });

    private static void BorrarCookie(HttpContext http) =>
        http.Response.Cookies.Delete(CookieSesion, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RutaCookie,
        });
}

public sealed record RespuestaSesion(Guid DocenteId, string TokenAcceso, DateTimeOffset ExpiraEn);

public sealed record SolicitudToken(string Token);

public sealed record SolicitudCorreo(string Correo);
