using System.Security.Claims;
using System.Text.Encodings.Web;
using AulaLista.Api.Comun;
using AulaLista.Application.Tutores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AulaLista.Api.Tutores;

/// <summary>
/// RN-062: el tutor entra con una cookie <c>HttpOnly</c> de sesión, no con JWT. Cada solicitud la valida contra la
/// base (una sesión revocada al regenerar el enlace deja de servir al instante) y la renueva con el uso.
/// </summary>
internal sealed class AutenticacionTutor(
    IOptionsMonitor<AuthenticationSchemeOptions> opciones, ILoggerFactory logger, UrlEncoder codificador, ValidacionSesionTutor validacion)
    : AuthenticationHandler<AuthenticationSchemeOptions>(opciones, logger, codificador)
{
    public const string Esquema = "Tutor";
    public const string Politica = "tutor";
    public const string PoliticaDocenteOTutor = "docente_o_tutor";
    public const string Cookie = "aula_lista_tutor";
    public const string RutaCookie = "/api/v1";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(Cookie, out var token) || string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.NoResult();
        }

        if (await validacion.ValidarAsync(token, Context.RequestAborted) is not { } sesion)
        {
            return AuthenticateResult.Fail("Sesión de tutor inválida.");
        }

        if (sesion.RenovadaHasta is { } hasta)
        {
            EscribirCookie(Context, token, hasta);
        }

        var identidad = new ClaimsIdentity([new Claim(ContextoUsuarioHttp.ClaimTutor, sesion.TutorId.ToString())], Esquema);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidad), Esquema));
    }

    public static void EscribirCookie(HttpContext http, string token, DateTimeOffset expira) =>
        http.Response.Cookies.Append(Cookie, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RutaCookie,
            Expires = expira,
            IsEssential = true,
        });
}
