using System.Security.Claims;
using AulaLista.Application.Comun;

namespace AulaLista.Api.Comun;

/// <summary>Lee el dueño de la solicitud desde los claims del token, o el que asumió un caso de uso o un job.</summary>
internal sealed class ContextoUsuarioHttp(IHttpContextAccessor accesor) : IContextoUsuario
{
    public const string ClaimDocente = "docente_id";
    public const string ClaimTutor = "tutor_id";
    public const string ClaimFamiliaSesion = "sid";

    private Guid? _docenteAsumido;

    public Guid? DocenteId => _docenteAsumido ?? Leer(ClaimDocente);

    public Guid? TutorId => _docenteAsumido is null ? Leer(ClaimTutor) : null;

    public Guid? FamiliaSesionId => Leer(ClaimFamiliaSesion);

    public string? DireccionIp => accesor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public void AsumirDocente(Guid docenteId) => _docenteAsumido = docenteId;

    private Guid? Leer(string claim)
    {
        var valor = accesor.HttpContext?.User.FindFirstValue(claim);
        return Guid.TryParse(valor, out var id) ? id : null;
    }
}
