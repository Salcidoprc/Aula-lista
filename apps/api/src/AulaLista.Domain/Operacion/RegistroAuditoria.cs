using AulaLista.Domain.Comun;

namespace AulaLista.Domain.Operacion;

/// <summary>Bitácora inmutable de acciones sensibles (RNF-005). Nunca guarda nombres, correos ni teléfonos.</summary>
public sealed class RegistroAuditoria : Entidad
{
    public RegistroAuditoria(
        string actorTipo,
        Guid? actorId,
        string accion,
        string entidad,
        Guid? entidadId,
        string? ip,
        DateTimeOffset ahora)
        : base(ahora)
    {
        ActorTipo = actorTipo;
        ActorId = actorId;
        Accion = accion;
        Entidad = entidad;
        EntidadId = entidadId;
        Ip = ip;
        Fecha = ahora;
    }

    private RegistroAuditoria()
    {
    }

    public string ActorTipo { get; private set; } = string.Empty;

    public Guid? ActorId { get; private set; }

    public string Accion { get; private set; } = string.Empty;

    public string Entidad { get; private set; } = string.Empty;

    public Guid? EntidadId { get; private set; }

    public string? Ip { get; private set; }

    public DateTimeOffset Fecha { get; private set; }
}

public static class AccionAuditoria
{
    public const string ReutilizacionTokenActualizacion = "reutilizacion_token_actualizacion";
    public const string CierreDeTodasLasSesiones = "cierre_de_todas_las_sesiones";
    public const string ExportacionSolicitada = "exportacion_solicitada";
    public const string EliminacionSolicitada = "eliminacion_cuenta_solicitada";
    public const string ContrasenaRestablecida = "contrasena_restablecida";
}
