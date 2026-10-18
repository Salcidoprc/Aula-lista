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
        DateTimeOffset ahora,
        int? cantidad = null)
        : base(ahora)
    {
        ActorTipo = actorTipo;
        ActorId = actorId;
        Accion = accion;
        Entidad = entidad;
        EntidadId = entidadId;
        Ip = ip;
        Fecha = ahora;
        Cantidad = cantidad;
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

    /// <summary>Cuántos registros tocó la acción, por ejemplo las boletas publicadas (SPEC-05).</summary>
    public int? Cantidad { get; private set; }
}

public static class AccionAuditoria
{
    public const string ReutilizacionTokenActualizacion = "reutilizacion_token_actualizacion";
    public const string CierreDeTodasLasSesiones = "cierre_de_todas_las_sesiones";
    public const string ExportacionSolicitada = "exportacion_solicitada";
    public const string EliminacionSolicitada = "eliminacion_cuenta_solicitada";
    public const string ContrasenaRestablecida = "contrasena_restablecida";
    public const string BoletasPublicadas = "boletas_publicadas";
    public const string BoletaCorregida = "boleta_corregida";
    public const string CalificacionesExportadas = "calificaciones_exportadas";
    public const string PeriodoCerrado = "periodo_cerrado";
    public const string PeriodoReabierto = "periodo_reabierto";
}
