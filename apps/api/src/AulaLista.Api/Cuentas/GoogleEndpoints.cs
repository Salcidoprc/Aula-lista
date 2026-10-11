using System.Security.Claims;
using AulaLista.Application.Cuentas;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AulaLista.Api.Cuentas;

/// <summary>RF-002: inicio de sesión con Google (OpenID Connect). Solo se publica si hay credenciales configuradas.</summary>
internal static class GoogleEndpoints
{
    public const string EsquemaExterno = "Externa";
    public const string EsquemaGoogle = "Google";
    public const string RutaRetornoGoogle = "/api/v1/cuentas/google/callback";
    private const string PropiedadVolver = "volver";

    /// <summary>Sin credenciales de Google, el botón regresa a la pantalla de entrada con un aviso.</summary>
    public static RouteGroupBuilder MapGoogleNoDisponible(this RouteGroupBuilder api)
    {
        api.MapGet("/cuentas/google/inicio", (IOptions<OpcionesCuentas> opciones) =>
                TypedResults.Redirect($"{opciones.Value.UrlWeb.TrimEnd('/')}/cuenta/entrar?error=google_no_disponible"))
            .AllowAnonymous().WithTags("Cuentas").WithName("IniciarConGoogle");
        return api;
    }

    /// <summary>Solo rutas de la propia app web, para no abrir una redirección a otro sitio.</summary>
    private static string RutaLocal(string? volver) =>
        volver is { Length: > 1 } && volver[0] == '/' && volver[1] != '/' && volver[1] != '\\' ? volver : "/";

    public static RouteGroupBuilder MapGoogle(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/cuentas/google").WithTags("Cuentas").AllowAnonymous();

        g.MapGet("/inicio", (string? volver) =>
            {
                var propiedades = new AuthenticationProperties { RedirectUri = "/api/v1/cuentas/google/completar" };
                propiedades.Items[PropiedadVolver] = RutaLocal(volver);
                return TypedResults.Challenge(propiedades, [EsquemaGoogle]);
            })
            .WithName("IniciarConGoogle");

        g.MapGet("/completar", Completar).WithName("CompletarConGoogle").ExcludeFromDescription();
        return api;
    }

    private static async Task<IResult> Completar(
        HttpContext http, IniciarConGoogleCasoUso caso, IOptions<OpcionesCuentas> opciones, CancellationToken ct)
    {
        var web = opciones.Value.UrlWeb.TrimEnd('/');
        var externa = await http.AuthenticateAsync(EsquemaExterno);
        var volver = RutaLocal(externa.Properties?.Items.TryGetValue(PropiedadVolver, out var v) == true ? v : null);
        await http.SignOutAsync(EsquemaExterno);
        var usuario = externa.Principal;
        var sub = usuario?.FindFirstValue(ClaimTypes.NameIdentifier);
        var correo = usuario?.FindFirstValue(ClaimTypes.Email);
        if (!externa.Succeeded || sub is null || correo is null)
        {
            return TypedResults.Redirect($"{web}/cuenta/entrar?error=google");
        }

        var verificado = string.Equals(usuario!.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);
        var resultado = await caso.EjecutarAsync(new DatosGoogle(sub, correo, verificado), ct);
        if (resultado.EsFallo)
        {
            return TypedResults.Redirect($"{web}/cuenta/entrar?error={resultado.Error.Codigo}");
        }

        CuentasEndpoints.EscribirCookie(http, resultado.Valor);
        return TypedResults.Redirect($"{web}{volver}");
    }
}
