using AulaLista.Infrastructure.Comun;

namespace AulaLista.Api.Operacion;

/// <summary>
/// Buzón de correos para local y end-to-end. Solo existe con <c>Correo:Proveedor = buzon</c>
/// y nunca en el entorno de producción.
/// </summary>
internal static class DesarrolloEndpoints
{
    public static RouteGroupBuilder MapDesarrollo(this RouteGroupBuilder api, IWebHostEnvironment entorno, IServiceProvider servicios)
    {
        if (entorno.IsProduction() || servicios.GetService<BuzonCorreo>() is null)
        {
            return api;
        }

        api.MapGet("/desarrollo/correos", (string para, BuzonCorreo buzon) =>
                TypedResults.Ok(buzon.Correos
                    .Where(c => string.Equals(c.Para, para, StringComparison.OrdinalIgnoreCase))
                    .Select(c => new CorreoDeDesarrollo(c.Asunto, c.Texto))
                    .Reverse()
                    .ToList()))
            .AllowAnonymous()
            .ExcludeFromDescription();
        return api;
    }
}

public sealed record CorreoDeDesarrollo(string Asunto, string Texto);
