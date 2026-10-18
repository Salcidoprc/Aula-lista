namespace AulaLista.Application.Comun;

public interface IUnidadDeTrabajo
{
    Task GuardarAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Corre el trabajo en una transacción con las variables de RLS del usuario de ese momento (también las de un
    /// usuario asumido) y guarda los cambios al final. Para lecturas que necesitan RLS fuera de una solicitud autenticada.
    /// </summary>
    Task<T> EnTransaccionAsync<T>(Func<Task<T>> trabajo, CancellationToken cancellationToken);
}
