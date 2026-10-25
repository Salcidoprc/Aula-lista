namespace AulaLista.Infrastructure.Ia;

/// <summary>
/// El prompt de la lectura de fotos de lista (SPEC-02, RF-015). Vive versionado en <c>Ia/Prompts/</c> como el de las
/// observaciones; cambiarlo pide subir <see cref="Version"/>.
/// </summary>
internal static class PromptLista
{
    public const string Version = "lista-v1";

    /// <summary>La respuesta cuando la foto no trae una lista legible.</summary>
    public const string SinLista = "SIN_LISTA";

    public const string Usuario = "Copia los nombres de los alumnos de esta lista.";

    public static string Sistema { get; } = Leer($"{Version}.md");

    /// <summary>Un nombre por línea; descarta lo que no parece un nombre (vacío, el marcador o demasiado largo).</summary>
    public static IReadOnlyList<string> Nombres(string texto) =>
        texto.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length is > 0 and <= 160 && l != SinLista)
            .ToList();

    private static string Leer(string archivo)
    {
        var ensamblado = typeof(PromptLista).Assembly;
        var recurso = ensamblado.GetManifestResourceNames().Single(n => n.EndsWith(archivo, StringComparison.Ordinal));
        using var lector = new StreamReader(ensamblado.GetManifestResourceStream(recurso)!);
        return lector.ReadToEnd();
    }
}
