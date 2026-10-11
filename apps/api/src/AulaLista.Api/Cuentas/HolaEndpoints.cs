using AulaLista.Application.Comun;

namespace AulaLista.Api.Cuentas;

/// <summary>Endpoint mínimo autenticado del hito H0. Se reemplaza por el perfil del docente en SPEC-01.</summary>
internal static class HolaEndpoints
{
    public static RouteGroupBuilder MapHola(this RouteGroupBuilder api)
    {
        api.MapGet("/hola", (IContextoUsuario usuario) =>
                TypedResults.Ok(new RespuestaHola("Hola desde Aula Lista", usuario.DocenteId)))
            .WithName("ObtenerHola")
            .WithTags("Cuentas");
        return api;
    }
}

public sealed record RespuestaHola(string Mensaje, Guid? DocenteId);
