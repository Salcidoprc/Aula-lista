using System.ComponentModel.DataAnnotations;
using AulaLista.Application.Comun;
using Microsoft.Extensions.Options;

namespace AulaLista.Infrastructure.Comun;

public sealed class OpcionesAlmacen
{
    public const string Seccion = "Almacen";

    /// <summary>Carpeta raíz de los archivos (un volumen en Coolify).</summary>
    [Required]
    public string Ruta { get; init; } = Path.Combine(Path.GetTempPath(), "aula-lista-archivos");
}

/// <summary>Almacén en disco del servidor. Se cambia por almacenamiento de objetos al migrar (ADR-0007).</summary>
internal sealed class AlmacenArchivosLocal(IOptions<OpcionesAlmacen> opciones) : IAlmacenArchivos
{
    public async Task<string> GuardarAsync(string carpeta, string nombre, byte[] contenido, CancellationToken cancellationToken)
    {
        var relativa = Path.Combine(carpeta, nombre);
        var completa = Resolver(relativa);
        Directory.CreateDirectory(Path.GetDirectoryName(completa)!);
        await File.WriteAllBytesAsync(completa, contenido, cancellationToken);
        return relativa;
    }

    public Task<Stream?> AbrirAsync(string ruta, CancellationToken cancellationToken)
    {
        var completa = Resolver(ruta);
        return Task.FromResult<Stream?>(File.Exists(completa) ? File.OpenRead(completa) : null);
    }

    public Task BorrarAsync(string ruta, CancellationToken cancellationToken)
    {
        var completa = Resolver(ruta);
        if (File.Exists(completa))
        {
            File.Delete(completa);
        }

        return Task.CompletedTask;
    }

    public Task<int> BorrarAnterioresAsync(string carpeta, DateTimeOffset antesDe, CancellationToken cancellationToken)
    {
        var raiz = Resolver(carpeta);
        if (!Directory.Exists(raiz))
        {
            return Task.FromResult(0);
        }

        var borrados = 0;
        foreach (var archivo in Directory.EnumerateFiles(raiz, "*", SearchOption.AllDirectories))
        {
            if (File.GetCreationTimeUtc(archivo) < antesDe.UtcDateTime)
            {
                File.Delete(archivo);
                borrados++;
            }
        }

        return Task.FromResult(borrados);
    }

    private string Resolver(string relativa)
    {
        var raiz = Path.GetFullPath(opciones.Value.Ruta);
        var completa = Path.GetFullPath(Path.Combine(raiz, relativa));
        if (!completa.StartsWith(raiz + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Ruta fuera del almacén.");
        }

        return completa;
    }
}
