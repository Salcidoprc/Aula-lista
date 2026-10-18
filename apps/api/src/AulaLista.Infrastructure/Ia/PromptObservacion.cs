using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AulaLista.Application.Cierre;

namespace AulaLista.Infrastructure.Ia;

/// <summary>
/// El prompt de las observaciones (SPEC-04, Contrato con la IA). El de sistema vive versionado en
/// <c>Ia/Prompts/</c>; cambiarlo pide correr la evaluación de calidad (reglas/05-ia) y subir <see cref="Version"/>.
/// </summary>
internal static class PromptObservacion
{
    public const string Version = "observacion-v1";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Sistema { get; } = Leer($"{Version}.md");

    /// <summary>Los ejemplos del docente van en su propio bloque para cachearlos aparte del prompt fijo.</summary>
    public static string? Ejemplos(EstiloParaIa estilo)
    {
        if (estilo.Ejemplos.Count == 0)
        {
            return null;
        }

        var texto = new StringBuilder("Ejemplos de observaciones escritas por este docente, para imitar su tono:\n");
        for (var i = 0; i < estilo.Ejemplos.Count; i++)
        {
            texto.Append(CultureInfo.InvariantCulture, $"\n<ejemplo_{i + 1}>\n{estilo.Ejemplos[i]}\n</ejemplo_{i + 1}>\n");
        }

        return texto.ToString();
    }

    /// <summary>Los datos del alumno en el JSON del contrato. Los ejemplos ya van en el sistema.</summary>
    public static string Usuario(EntradaObservacion entrada) => JsonSerializer.Serialize(
        new
        {
            entrada.Alumno,
            entrada.Trato,
            entrada.Nivel,
            entrada.Grado,
            entrada.Materia,
            entrada.Periodo,
            entrada.CalificacionPeriodo,
            entrada.MinimaAprobatoria,
            Rubros = entrada.Rubros.Select(r => new { r.Nombre, r.Peso, r.Promedio, r.NoEntrego }),
            Notas = entrada.Notas.Select(n => new { Fecha = n.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), n.Etiqueta, n.Texto }),
            Estilo = new { entrada.Estilo.Longitud, entrada.Estilo.Persona, Ejemplos = entrada.Estilo.Ejemplos.Count },
            entrada.InstruccionExtra,
        },
        Json);

    private static string Leer(string archivo)
    {
        var ensamblado = typeof(PromptObservacion).Assembly;
        var recurso = ensamblado.GetManifestResourceNames().Single(n => n.EndsWith(archivo, StringComparison.Ordinal));
        using var lector = new StreamReader(ensamblado.GetManifestResourceStream(recurso)!);
        return lector.ReadToEnd();
    }
}
