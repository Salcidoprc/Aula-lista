namespace AulaLista.Application.Comun;

/// <summary>Bitácora de acciones sensibles (RNF-005). Se guarda con la siguiente llamada a la unidad de trabajo.</summary>
public interface IAuditoria
{
    void Registrar(string accion, string entidad, Guid? entidadId, Guid? docenteId, int? cantidad = null);
}
