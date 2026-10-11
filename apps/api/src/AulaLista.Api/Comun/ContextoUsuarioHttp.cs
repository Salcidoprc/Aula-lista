using System.Security.Claims;
using AulaLista.Application.Comun;

namespace AulaLista.Api.Comun;

/// <summary>Lee el dueño de la solicitud desde los claims del token.</summary>
internal sealed class ContextoUsuarioHttp(IHttpContextAccessor accesor) : IContextoUsuario
{
    public const string ClaimDocente = "docente_id";
    public const string ClaimTutor = "tutor_id";

    public Guid? DocenteId => Leer(ClaimDocente);

    public Guid? TutorId => Leer(ClaimTutor);

    private Guid? Leer(string claim)
    {
        var valor = accesor.HttpContext?.User.FindFirstValue(claim);
        return Guid.TryParse(valor, out var id) ? id : null;
    }
}
