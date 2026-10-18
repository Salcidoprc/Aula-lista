namespace AulaLista.Application.Comun;

/// <summary>Encola trabajos en segundo plano. Los trabajos reciben solo IDs (reglas/01).</summary>
public interface IColaTrabajos
{
    void EncolarExportacion(Guid docenteId, Guid exportacionId);

    /// <summary>Redacta con la IA las observaciones dadas de un periodo (RF-041).</summary>
    void EncolarObservaciones(Guid docenteId, Guid periodoId, IReadOnlyList<Guid> observacionIds);
}
