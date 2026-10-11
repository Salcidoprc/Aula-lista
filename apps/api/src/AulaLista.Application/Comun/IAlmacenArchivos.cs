namespace AulaLista.Application.Comun;

/// <summary>Archivos generados (exportaciones, después PDFs de boletas).</summary>
public interface IAlmacenArchivos
{
    Task<string> GuardarAsync(string carpeta, string nombre, byte[] contenido, CancellationToken cancellationToken);

    Task<Stream?> AbrirAsync(string ruta, CancellationToken cancellationToken);

    Task BorrarAsync(string ruta, CancellationToken cancellationToken);

    /// <summary>Borra los archivos de una carpeta creados antes de una fecha.</summary>
    Task<int> BorrarAnterioresAsync(string carpeta, DateTimeOffset antesDe, CancellationToken cancellationToken);
}
