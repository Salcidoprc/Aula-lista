namespace AulaLista.Application.Comun;

/// <summary>Quién hace la solicitud. Alimenta los filtros por dueño y las variables de RLS.</summary>
public interface IContextoUsuario
{
    Guid? DocenteId { get; }

    Guid? TutorId { get; }

    /// <summary>IP de origen, solo para la bitácora de auditoría.</summary>
    string? DireccionIp { get; }

    /// <summary>Id de la familia de sesión del token de acceso (claim <c>sid</c>).</summary>
    Guid? FamiliaSesionId { get; }

    /// <summary>
    /// Actúa como un docente ya autenticado por otro medio (contraseña, Google, token de correo o un job).
    /// Debe llamarse antes de abrir la transacción para que RLS lo vea.
    /// </summary>
    void AsumirDocente(Guid docenteId);
}
