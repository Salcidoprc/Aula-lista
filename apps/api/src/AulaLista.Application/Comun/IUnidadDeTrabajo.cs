namespace AulaLista.Application.Comun;

public interface IUnidadDeTrabajo
{
    Task GuardarAsync(CancellationToken cancellationToken);
}
