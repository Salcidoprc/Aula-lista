namespace AulaLista.Application.Comun;

/// <summary>Quién hace la solicitud. Alimenta los filtros por dueño y las variables de RLS.</summary>
public interface IContextoUsuario
{
    Guid? DocenteId { get; }

    Guid? TutorId { get; }
}
