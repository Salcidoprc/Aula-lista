using System.Diagnostics.Metrics;
using Aulisima.Domain.Operacion;

namespace Aulisima.Infrastructure.Operacion;

/// <summary>
/// SPEC-09: métricas propias para las alertas de Grafana. Las etiquetas son de pocos valores (el origen), nunca IDs, para
/// no multiplicar series (ADR-0015).
/// </summary>
public sealed class MetricasAulisima
{
    public const string Medidor = "Aulisima";

    private readonly Counter<long> _trabajosFallidos;
    private int _gruposCostoAlto;

    public MetricasAulisima(IMeterFactory medidores)
    {
        ArgumentNullException.ThrowIfNull(medidores);
        var medidor = medidores.Create(Medidor);
        _trabajosFallidos = medidor.CreateCounter<long>(
            "aulisima.trabajos.fallidos", "{trabajo}", "Trabajos que fallaron tras sus reintentos (error_trabajo).");
        medidor.CreateObservableGauge(
            "aulisima.grupos.costo_alto", () => Volatile.Read(ref _gruposCostoAlto), "{grupo}",
            "Grupos cuyo costo estimado del mes pasa Operacion:AlertaCostoGrupoMxn.");

        // Cada serie arranca en 0: sin ese punto, el primer fallo de un origen aparece ya en 1 y su aumento no se ve.
        foreach (var origen in OrigenError.Todos)
        {
            _trabajosFallidos.Add(0, new KeyValuePair<string, object?>("origen", origen));
        }
    }

    public int GruposCostoAlto => Volatile.Read(ref _gruposCostoAlto);

    public void TrabajoFallido(string origen) =>
        _trabajosFallidos.Add(1, new KeyValuePair<string, object?>("origen", origen));

    public void FijarGruposCostoAlto(int grupos) => Volatile.Write(ref _gruposCostoAlto, grupos);
}
