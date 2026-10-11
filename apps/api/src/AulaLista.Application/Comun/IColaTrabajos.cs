namespace AulaLista.Application.Comun;

/// <summary>Encola trabajos en segundo plano. Los trabajos reciben solo IDs (reglas/01).</summary>
public interface IColaTrabajos
{
    void EncolarExportacion(Guid docenteId, Guid exportacionId);
}
