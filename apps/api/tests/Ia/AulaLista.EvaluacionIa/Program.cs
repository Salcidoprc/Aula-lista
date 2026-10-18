using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Anthropic;
using AulaLista.Application.Cierre;
using AulaLista.Domain.Cierre;
using AulaLista.Infrastructure.Ia;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

// Evaluación de calidad de las observaciones (reglas/05-ia, SPEC-04): 30 casos ficticios contra el modelo real.
// Se corre a mano antes de cambiar el prompt o el modelo, porque cuesta dinero:
//   Ia__ApiKey=... Ia__Modelo=claude-haiku-5-5 dotnet run --project tests/Ia/AulaLista.EvaluacionIa [reporte.md]
// Con --simulado prueba el arnés con el redactor local, sin red ni costo.
// Termina con código 1 si algún caso no cumple los criterios automáticos. El reporte trae los textos para la
// revisión humana de una muestra de 10.

var simulado = args.Contains("--simulado");
args = [.. args.Where(a => a != "--simulado")];
var modelo = simulado ? "simulado" : Environment.GetEnvironmentVariable("Ia__Modelo");
var clave = simulado ? "simulado" : Environment.GetEnvironmentVariable("Ia__ApiKey");
if (string.IsNullOrWhiteSpace(modelo) || string.IsNullOrWhiteSpace(clave))
{
    Console.Error.WriteLine("Falta Ia__Modelo o Ia__ApiKey en las variables de entorno.");
    return 2;
}

var opciones = Environment.GetEnvironmentVariable("Ia__Esfuerzo") is { Length: > 0 } esfuerzo
    ? new OpcionesIa { Proveedor = OpcionesIa.ProveedorClaude, Modelo = modelo, ApiKey = clave, Esfuerzo = esfuerzo }
    : new OpcionesIa { Proveedor = OpcionesIa.ProveedorClaude, Modelo = modelo, ApiKey = clave };

var casos = JsonSerializer.Deserialize<List<Caso>>(
    await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "casos", "casos.json")), Criterios.Json)!;

IRedactorObservaciones redactor = simulado
    ? new RedactorSimulado()
    : new RedactorClaude(
        new AnthropicClient { ApiKey = clave, MaxRetries = opciones.Reintentos },
        Options.Create(opciones),
        NullLogger<RedactorClaude>.Instance);

var reporte = new StringBuilder();
reporte.AppendLine(CultureInfo.InvariantCulture, $"# Evaluación de observaciones · {modelo} · {PromptObservacion.Version}");
reporte.AppendLine();
var fallidos = 0;
var tokensEntrada = 0;
var tokensSalida = 0;
foreach (var caso in casos)
{
    var redactada = await redactor.RedactarAsync(caso.Entrada, CancellationToken.None);
    tokensEntrada += redactada.TokensEntrada;
    tokensSalida += redactada.TokensSalida;
    var codigo = caso.Entrada.Alumno.Split(' ').Last();
    var encargo = new EncargoObservacion(Guid.Empty, Guid.Empty, caso.Entrada, caso.Entrada.Alumno, "Nombre", caso.Entrada.Estilo.Longitud);
    var terminado = RedactarObservacionesTrabajo.Terminar(encargo, redactada);
    var problemas = Criterios.Revisar(caso.Entrada, codigo, redactada.Texto, terminado);
    fallidos += problemas.Count > 0 ? 1 : 0;

    reporte.AppendLine(CultureInfo.InvariantCulture, $"## {caso.Id}: {(problemas.Count == 0 ? "cumple" : "NO CUMPLE")}");
    reporte.AppendLine(CultureInfo.InvariantCulture, $"{caso.Descripcion}; longitud {caso.Entrada.Estilo.Longitud}, persona {caso.Entrada.Estilo.Persona}.");
    foreach (var problema in problemas)
    {
        reporte.AppendLine(CultureInfo.InvariantCulture, $"- {problema}");
    }

    reporte.AppendLine();
    reporte.AppendLine(CultureInfo.InvariantCulture, $"> {redactada.Texto ?? $"(sin texto: {redactada.Fallo})"}");
    reporte.AppendLine();
    Console.WriteLine($"{caso.Id}: {(problemas.Count == 0 ? "cumple" : string.Join("; ", problemas))}");
}

reporte.AppendLine(CultureInfo.InvariantCulture, $"**Resultado:** {casos.Count - fallidos} de {casos.Count} cumplen. Tokens: {tokensEntrada} de entrada, {tokensSalida} de salida.");
var ruta = args.Length > 0 ? args[0] : "evaluacion-observaciones.md";
await File.WriteAllTextAsync(ruta, reporte.ToString());
Console.WriteLine($"{casos.Count - fallidos} de {casos.Count} cumplen. Reporte en {Path.GetFullPath(ruta)}");
return fallidos == 0 ? 0 : 1;

internal sealed record Caso(string Id, string Descripcion, EntradaObservacion Entrada);

/// <summary>Criterios automáticos de reglas/05-ia: longitud, sin seudónimo, sin palabras prohibidas y concordancia con el trato.</summary>
internal static partial class Criterios
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true };

    private static readonly (string Masculino, string Femenino)[] Adjetivos =
    [
        ("atento", "atenta"), ("dedicado", "dedicada"), ("comprometido", "comprometida"), ("participativo", "participativa"),
        ("organizado", "organizada"), ("cumplido", "cumplida"), ("ordenado", "ordenada"), ("cuidadoso", "cuidadosa"),
        ("trabajador", "trabajadora"), ("creativo", "creativa"), ("motivado", "motivada"), ("concentrado", "concentrada"),
    ];

    public static List<string> Revisar(EntradaObservacion entrada, string codigo, string? crudo, TextoTerminado terminado)
    {
        var problemas = new List<string>();
        if (terminado.Fallo is { } fallo)
        {
            problemas.Add($"falla: {fallo} ({LongitudObservacion.ContarPalabras(crudo ?? string.Empty)} palabras)");
        }

        if (crudo is null)
        {
            return problemas;
        }

        if (crudo.Contains(codigo, StringComparison.OrdinalIgnoreCase))
        {
            problemas.Add("incluye el seudónimo");
        }

        foreach (Match m in Prohibidas().Matches(crudo))
        {
            problemas.Add($"palabra prohibida: \"{m.Value}\"");
        }

        if (Formato().IsMatch(crudo))
        {
            problemas.Add("trae formato, encabezado o comillas");
        }

        if (entrada.Estilo.Persona == PersonaObservacion.Tercera)
        {
            foreach (var (masculino, femenino) in Adjetivos)
            {
                var conMasculino = Regex.IsMatch(crudo, $@"\b{masculino}s?\b", RegexOptions.IgnoreCase);
                var conFemenino = Regex.IsMatch(crudo, $@"\b{femenino}s?\b", RegexOptions.IgnoreCase);
                if ((entrada.Trato == "ella" && conMasculino) || (entrada.Trato == "el" && conFemenino)
                    || (entrada.Trato == "neutro" && (conMasculino || conFemenino)))
                {
                    problemas.Add($"no concuerda con el trato {entrada.Trato}: \"{(conMasculino ? masculino : femenino)}\"");
                }
            }
        }

        return problemas;
    }

    // Diagnósticos, comparaciones con otros, etiquetas y juicios sobre la familia (RN-047).
    [GeneratedRegex(
        @"\b(tdah|d[eé]ficit de atenci[oó]n|dislexi\w*|discalcul\w*|ansiedad|ansios[oa]|depresi[oó]n|deprimid[oa]|autis\w*|asperger|hiperactiv\w*|trastorno\w*|problemas? de aprendizaje|que sus compa[ñn]er[oa]s|que el resto|que los dem[aá]s|que el grupo|mejor que|peor que|flo[jx][oa]|latos[oa]|problem[aá]tic[oa]|distra[ií]d[oa]|burr[oa]|el peor|la peor|el mejor|la mejor|su familia no|descuid\w* familiar)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex Prohibidas();

    [GeneratedRegex(@"[*#`]|^\s*[-•]|^[^\n]{0,40}:\s*$|^[""“«]", RegexOptions.Multiline)]
    private static partial Regex Formato();
}
