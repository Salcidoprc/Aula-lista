using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AulaLista.Domain.Grupos;

namespace AulaLista.Application.Cierre;

/// <summary>
/// RN-019 y RNF-007: lo que el docente escribió (notas, ejemplos de estilo, instrucciones) puede traer nombres,
/// teléfonos o correos. Antes de enviarlo a la IA se cambian: el alumno por su seudónimo, sus compañeros por
/// "otro alumno", sus tutores por "su familia", y teléfonos y correos por una marca. Compara sin acentos ni
/// mayúsculas y por palabra completa.
/// </summary>
public sealed partial class Seudonimizador
{
    public const string OtroAlumno = "otro alumno";
    public const string Familia = "su familia";
    public const string Telefono = "[teléfono]";
    public const string Correo = "[correo]";

    /// <summary>Partículas de los nombres que solas no identifican a nadie.</summary>
    private static readonly HashSet<string> Particulas = new(StringComparer.Ordinal)
    {
        "de", "del", "la", "las", "los", "y", "san", "santa", "da", "di", "van", "von", "mc",
    };

    private readonly List<(Regex Patron, string Reemplazo)> _reemplazos = [];

    /// <param name="alumno">El alumno de la observación: sus nombres pasan a <paramref name="seudonimo"/>.</param>
    /// <param name="companeros">Todos los alumnos del grupo (también bajas).</param>
    /// <param name="tutores">Nombres de los tutores del grupo; sus teléfonos y correos los atrapan los patrones.</param>
    public Seudonimizador(
        Alumno alumno, string seudonimo, IEnumerable<Alumno> companeros, IEnumerable<string> tutores)
    {
        var frases = new List<(string Frase, string Reemplazo)>();
        Agregar(frases, alumno.Nombres, alumno.Apellidos, seudonimo);
        foreach (var otro in companeros.Where(c => c.Id != alumno.Id))
        {
            Agregar(frases, otro.Nombres, otro.Apellidos, OtroAlumno);
        }

        foreach (var tutor in tutores)
        {
            Agregar(frases, tutor, string.Empty, Familia);
        }

        // Primero las frases largas ("Ana Sofía López") para que no queden pedazos sueltos; ante el mismo texto,
        // gana el alumno, que se agregó primero.
        foreach (var (frase, reemplazo) in frases.DistinctBy(f => f.Frase).OrderByDescending(f => f.Frase.Length))
        {
            _reemplazos.Add((Patron(frase), reemplazo));
        }
    }

    public string Limpiar(string texto)
    {
        var limpio = CorreoPatron().Replace(texto, Correo);
        limpio = TelefonoPatron().Replace(limpio, Telefono);
        var plano = SinAcentos(limpio);
        foreach (var (patron, reemplazo) in _reemplazos)
        {
            // Se busca sobre la versión sin acentos (misma longitud) y se reemplaza en el texto original.
            var coincidencias = patron.Matches(plano);
            for (var i = coincidencias.Count - 1; i >= 0; i--)
            {
                var c = coincidencias[i];
                limpio = string.Concat(limpio.AsSpan(0, c.Index), reemplazo, limpio.AsSpan(c.Index + c.Length));
                plano = string.Concat(plano.AsSpan(0, c.Index), SinAcentos(reemplazo), plano.AsSpan(c.Index + c.Length));
            }
        }

        // "Mateo Cruz" son dos palabras del mismo alumno: queda un solo "otro alumno".
        foreach (var reemplazo in _reemplazos.Select(r => r.Reemplazo).Distinct())
        {
            var repetido = Regex.Escape(reemplazo);
            limpio = Regex.Replace(limpio, $@"{repetido}(?:\s+{repetido})+", reemplazo, RegexOptions.None, TimeSpan.FromMilliseconds(200));
        }

        return limpio;
    }

    /// <summary>Quita acentos y diéresis carácter por carácter, sin cambiar la longitud.</summary>
    internal static string SinAcentos(string texto)
    {
        var resultado = new StringBuilder(texto.Length);
        foreach (var caracter in texto)
        {
            if (char.IsSurrogate(caracter) || caracter < 'À')
            {
                resultado.Append(caracter);
                continue;
            }

            var descompuesto = caracter.ToString().Normalize(NormalizationForm.FormD);
            resultado.Append(descompuesto.Length > 1 && char.IsLetter(descompuesto[0])
                && CharUnicodeInfo.GetUnicodeCategory(descompuesto[1]) == UnicodeCategory.NonSpacingMark
                ? descompuesto[0]
                : caracter);
        }

        return resultado.ToString();
    }

    private static void Agregar(List<(string, string)> frases, string nombres, string apellidos, string reemplazo)
    {
        var completo = $"{nombres} {apellidos}".Trim();
        if (completo.Length == 0)
        {
            return;
        }

        frases.Add((completo, reemplazo));
        if (apellidos.Length > 0)
        {
            frases.Add(($"{apellidos} {nombres}".Trim(), reemplazo));
            frases.Add((nombres.Trim(), reemplazo));
            frases.Add((apellidos.Trim(), reemplazo));
        }

        foreach (var palabra in completo.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (palabra.Length >= 3 && !Particulas.Contains(SinAcentos(palabra).ToLowerInvariant()))
            {
                frases.Add((palabra, reemplazo));
            }
        }
    }

    private static Regex Patron(string frase)
    {
        var partes = SinAcentos(frase).Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape);
        return new Regex(
            $@"(?<![\p{{L}}\p{{N}}]){string.Join(@"\s+", partes)}(?![\p{{L}}\p{{N}}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(200));
    }

    [GeneratedRegex(@"[\w.+\-]+@[\w\-]+(\.[\w\-]+)+", RegexOptions.CultureInvariant)]
    private static partial Regex CorreoPatron();

    /// <summary>Diez dígitos o más (los teléfonos de México), con espacios, guiones, puntos o paréntesis entre ellos, y un + opcional.</summary>
    [GeneratedRegex(@"\+?\d(?:[\s\-.()]*\d){9,}", RegexOptions.CultureInvariant)]
    private static partial Regex TelefonoPatron();
}
