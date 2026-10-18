using System.Globalization;
using AulaLista.Application.Cierre;
using AulaLista.Domain.Cierre;

namespace AulaLista.Infrastructure.Ia;

/// <summary>
/// Redactor sin red ni costo para local, pruebas y demostraciones (<c>Ia:Proveedor=simulado</c>). Arma un texto
/// plausible con los rubros y las etiquetas de las notas, en la longitud y la persona del estilo.
/// </summary>
internal sealed class RedactorSimulado : IRedactorObservaciones
{
    public const string Modelo = "simulado";

    private static readonly string[] Relleno =
    [
        "Participar en clase y preguntar cuando algo no queda claro ayuda mucho.",
        "Repasar en casa unos minutos cada día dará buenos resultados.",
        "Organizar los materiales permite entregar a tiempo.",
        "El esfuerzo de este periodo vale la pena mantenerlo.",
        "Compartir lo aprendido con el equipo refuerza el avance.",
    ];

    public async Task<ObservacionRedactada> RedactarAsync(EntradaObservacion entrada, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
        var segunda = entrada.Estilo.Persona == PersonaObservacion.Segunda;
        var frases = new List<string>();
        var mejor = entrada.Rubros.Where(r => r.Promedio is not null).MaxBy(r => r.Promedio);
        var peor = entrada.Rubros.Where(r => r.Promedio is not null).MinBy(r => r.Promedio);
        frases.Add(mejor is null
            ? (segunda ? "Este periodo trabajaste con constancia." : "Este periodo trabajó con constancia.")
            : segunda ? $"Este periodo destacaste en {mejor.Nombre.ToLower(CultureInfo.GetCultureInfo("es-MX"))}." : $"Este periodo destacó en {mejor.Nombre.ToLower(CultureInfo.GetCultureInfo("es-MX"))}.");
        if (entrada.Notas.Any(n => n.Etiqueta == "fortaleza"))
        {
            frases.Add(segunda ? "Mostraste disposición para apoyar a tu equipo." : "Mostró disposición para apoyar a su equipo.");
        }

        if (peor is not null && peor != mejor)
        {
            frases.Add(segunda
                ? $"Para el siguiente periodo, enfócate en {peor.Nombre.ToLower(CultureInfo.GetCultureInfo("es-MX"))}."
                : $"Para el siguiente periodo, conviene reforzar {peor.Nombre.ToLower(CultureInfo.GetCultureInfo("es-MX"))}.");
        }

        if (entrada.Rubros.Sum(r => r.NoEntrego) > 0)
        {
            frases.Add(segunda ? "Entregar todas tus actividades hará una gran diferencia." : "Entregar todas sus actividades hará una gran diferencia.");
        }

        var (minimo, maximo) = LongitudObservacion.Palabras(entrada.Estilo.Longitud);
        if (entrada.InstruccionExtra?.Contains("breve", StringComparison.OrdinalIgnoreCase) == true)
        {
            maximo = minimo + ((maximo - minimo) / 3);
        }

        var i = 0;
        while (LongitudObservacion.ContarPalabras(string.Join(' ', frases)) < minimo + ((maximo - minimo) / 3))
        {
            frases.Add(Relleno[i++ % Relleno.Length]);
        }

        var texto = string.Join(' ', frases);
        return new ObservacionRedactada(texto, null, Modelo, PromptObservacion.Version, 900, LongitudObservacion.ContarPalabras(texto) * 2, 0, 150);
    }
}
