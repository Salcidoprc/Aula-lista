namespace AulaLista.Application.Comun;

/// <summary>Quién hace la solicitud. Alimenta los filtros por dueño y las variables de RLS.</summary>
public interface IContextoUsuario
{
    Guid? DocenteId { get; }

    Guid? TutorId { get; }

    /// <summary>
    /// SPEC-09: el administrador ya comprobado de una ruta <c>/admin</c>. Alimenta <c>app.admin_id</c>, que deja escribir
    /// el catálogo de tipos de rubro y la auditoría del panel.
    /// </summary>
    Guid? AdminId { get; }

    /// <summary>IP de origen, solo para la bitácora de auditoría.</summary>
    string? DireccionIp { get; }

    /// <summary>Id de la familia de sesión del token de acceso (claim <c>sid</c>).</summary>
    Guid? FamiliaSesionId { get; }

    /// <summary>
    /// Actúa como un docente ya autenticado por otro medio (contraseña, Google, token de correo o un job).
    /// Debe llamarse antes de abrir la transacción para que RLS lo vea.
    /// </summary>
    void AsumirDocente(Guid docenteId);

    /// <summary>
    /// Actúa como el tutor dueño de un enlace o una sesión ya probados por su hash (SPEC-06). Debe llamarse antes
    /// de <see cref="IUnidadDeTrabajo.EnTransaccionAsync{T}"/> para que RLS lo vea.
    /// </summary>
    void AsumirTutor(Guid tutorId);

    /// <summary>Actúa como administrador comprobado (SPEC-09). Debe llamarse antes de la transacción del caso de uso.</summary>
    void AsumirAdmin(Guid adminId);
}
