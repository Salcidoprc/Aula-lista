using System.Collections.Concurrent;
using AulaLista.Application.Cierre;
using AulaLista.Domain.Cierre;
using AulaLista.Infrastructure.Ia;

namespace AulaLista.Pruebas.Integracion;

/// <summary>
/// Sustituye a la IA en las pruebas: guarda el contenido exacto de cada solicitud (sistema, ejemplos y datos del
/// alumno, como los manda <see cref="RedactorClaude"/>) y responde con el redactor simulado o con la falla pedida.
/// </summary>
public sealed class RedactorDePrueba : IRedactorObservaciones
{
    private readonly RedactorSimulado _simulado = new();

    public ConcurrentQueue<string> Enviados { get; } = new();

    /// <summary>Si se fija, cada solicitud responde esta falla (por ejemplo <see cref="FalloGeneracion.Rechazo"/>).</summary>
    public string? Fallo { get; set; }

    /// <summary>Si se fija, cada solicitud responde este texto en lugar del simulado.</summary>
    public string? Texto { get; set; }

    public async Task<ObservacionRedactada> RedactarAsync(EntradaObservacion entrada, CancellationToken cancellationToken)
    {
        Enviados.Enqueue(string.Join('\n', PromptObservacion.Sistema, PromptObservacion.Ejemplos(entrada.Estilo), PromptObservacion.Usuario(entrada)));
        if (Fallo is { } fallo)
        {
            return new ObservacionRedactada(null, fallo, RedactorSimulado.Modelo, PromptObservacion.Version, 900, 0, 0, 10);
        }

        var redactada = await _simulado.RedactarAsync(entrada, cancellationToken);
        return Texto is { } texto ? redactada with { Texto = texto } : redactada;
    }
}
