using System.Collections.Concurrent;
using AulaLista.Application.Grupos;
using AulaLista.Domain.Cierre;
using AulaLista.Infrastructure.Ia;

namespace AulaLista.Pruebas.Integracion;

/// <summary>Sustituye a la IA al leer fotos de lista: guarda cuántos bytes recibió y responde los nombres o la falla pedida.</summary>
public sealed class LectorDePrueba : ILectorListas
{
    public ConcurrentQueue<ImagenLista> Recibidas { get; } = new();

    /// <summary>Lo que "leyó" la IA, un renglón por elemento, tal como lo regresaría el modelo.</summary>
    public IReadOnlyList<string> Nombres { get; set; } = ["1. Acosta Beltrán Ana Sofía", "2. Bojórquez Félix Diego", "Castro Lizárraga Valeria"];

    /// <summary>Si se fija, cada lectura responde esta falla (por ejemplo <see cref="FalloGeneracion.Rechazo"/>).</summary>
    public string? Fallo { get; set; }

    public Task<ListaLeida> LeerAsync(ImagenLista imagen, CancellationToken cancellationToken)
    {
        Recibidas.Enqueue(imagen);
        return Task.FromResult(Fallo is { } fallo
            ? new ListaLeida([], fallo, RedactorSimulado.Modelo, "lista-v1", 1600, 0, 10)
            : new ListaLeida(Nombres, null, RedactorSimulado.Modelo, "lista-v1", 1600, 180, 10));
    }
}
