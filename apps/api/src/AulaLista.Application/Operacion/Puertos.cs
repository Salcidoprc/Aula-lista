using System.ComponentModel.DataAnnotations;
using AulaLista.Domain.Evaluacion;
using AulaLista.Domain.Operacion;

namespace AulaLista.Application.Operacion;

/// <summary>SPEC-09: quién administra, contra qué se miden los costos y cuánto se conserva.</summary>
public sealed class OpcionesOperacion
{
    public const string Seccion = "Operacion";

    /// <summary>
    /// Correos con rol <c>admin</c> por configuración (<c>Operacion__CorreosAdmin__0</c>), además de los marcados con
    /// <c>--admin</c>. Nunca se asigna desde la interfaz.
    /// </summary>
    public string[] CorreosAdmin { get; init; } = [];

    /// <summary>Para convertir el precio de la IA (en dólares) a pesos en el panel de costos.</summary>
    [Range(typeof(decimal), "0", "1000")]
    public decimal TipoCambioUsdMxn { get; init; }

    /// <summary>RNF-022: el costo variable de un grupo al mes no pasa de este monto.</summary>
    [Range(typeof(decimal), "0", "10000")]
    public decimal AlertaCostoGrupoMxn { get; init; }

    /// <summary>reglas/05-ia: alerta si la IA de un grupo cuesta más que esto en un mes.</summary>
    [Range(typeof(decimal), "0", "10000")]
    public decimal AlertaCostoIaGrupoMxn { get; init; }

    [Range(1, 52)]
    public int SemanasMetricas { get; init; } = 12;

    /// <summary>RNF-009: los grupos archivados se conservan estos años desde el fin de su ciclo.</summary>
    [Range(1, 10)]
    public int AniosGruposArchivados { get; init; } = 2;

    /// <summary>Metas del piloto (00-prd, sección 4) que el panel pone junto a cada métrica.</summary>
    [Required]
    public MetasPiloto Metas { get; init; } = new();
}

public sealed class MetasPiloto
{
    public int DocentesActivos { get; init; }

    public decimal PorcentajeCierran { get; init; }

    public decimal MinutosCierre { get; init; }

    public decimal PorcentajeObservacionesAceptadas { get; init; }

    public decimal PorcentajeTutoresAbren { get; init; }

    public decimal PorcentajeConversion { get; init; }
}

/// <summary>RF-090: eventos de producto con ids internos y números; nada personal.</summary>
public interface IEventosProducto
{
    /// <summary>
    /// Se escribe en la transacción en curso. Con <paramref name="clave"/>, un mismo docente, evento y clave se guarda una
    /// sola vez (por ejemplo una captura por día y vista).
    /// </summary>
    Task RegistrarAsync(
        string nombre, Guid docenteId, IReadOnlyDictionary<string, object?> propiedades, string? clave, CancellationToken cancellationToken);
}

/// <summary>
/// Errores de trabajos en segundo plano para el panel: solo un tipo (el de la excepción o un código), nunca su mensaje,
/// que podría llevar datos de alguien.
/// </summary>
public interface IRegistroErrores
{
    Task RegistrarAsync(string origen, string trabajo, Guid? docenteId, Guid? referenciaId, string tipoError, CancellationToken cancellationToken);
}

public static class RegistroErroresExtensiones
{
    public static Task RegistrarAsync(
        this IRegistroErrores registro, string origen, string trabajo, Guid? docenteId, Guid? referenciaId, Exception excepcion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registro);
        ArgumentNullException.ThrowIfNull(excepcion);
        var tipo = excepcion.GetType().FullName ?? excepcion.GetType().Name;
        return registro.RegistrarAsync(origen, trabajo, docenteId, referenciaId, tipo, cancellationToken);
    }
}

/// <summary>Una semana de las métricas del piloto, ya contada en la base.</summary>
public sealed record FilaMetricas(
    DateOnly Semana,
    int DocentesActivos,
    int DocentesCierran,
    decimal? MinutosCierre,
    int Observaciones,
    int ObservacionesMenores,
    int Enlaces,
    int Aperturas,
    int ConPrueba,
    int Pagan);

public sealed record FilaDocente(
    Guid DocenteId,
    string Correo,
    string? Nombre,
    string Estado,
    DateTimeOffset RegistradoEn,
    DateTimeOffset? UltimoAcceso,
    int Grupos,
    int GruposPro,
    string? EstadoSuscripcion,
    string? Modalidad,
    DateTimeOffset? PruebaInicio,
    int LlamadasIa,
    long TokensEntrada,
    long TokensSalida,
    long TokensCache,
    int MensajesWhatsapp,
    decimal CostoWhatsapp);

public sealed record FilaCosto(
    Guid GrupoId,
    Guid DocenteId,
    string Correo,
    string Nivel,
    int Grado,
    string Nombre,
    string? Materia,
    bool ProPagado,
    bool EnPrueba,
    int LlamadasIa,
    long TokensEntrada,
    long TokensSalida,
    long TokensCache,
    int MensajesWhatsapp,
    decimal CostoWhatsapp);

public sealed record FilaError(
    Guid Id, string Origen, string Trabajo, Guid? DocenteId, string? Correo, Guid? ReferenciaId, string TipoError, DateTimeOffset Fecha);

public sealed record FilaAuditoria(
    Guid Id, Guid AdminId, string? Correo, string Accion, string Entidad, Guid? EntidadId, string? Antes, string? Despues, DateTimeOffset Fecha);

/// <summary>
/// SPEC-09: lecturas del panel a través de funciones <c>SECURITY DEFINER</c> que solo devuelven lo que RN-090 permite
/// (conteos, correos y nombres de docentes), y las escrituras del catálogo como el administrador de la solicitud.
/// </summary>
public interface IRepositorioAdmin
{
    Task<IReadOnlyList<FilaMetricas>> MetricasAsync(DateOnly desde, int semanas, decimal umbralCambioMayor, CancellationToken cancellationToken);

    Task<IReadOnlyList<FilaDocente>> DocentesAsync(DateOnly mes, CancellationToken cancellationToken);

    Task<IReadOnlyList<FilaCosto>> CostosAsync(DateOnly mes, int diasMaximoPrueba, CancellationToken cancellationToken);

    Task<IReadOnlyList<FilaError>> ErroresAsync(int limite, CancellationToken cancellationToken);

    Task<IReadOnlyList<FilaAuditoria>> AuditoriaAsync(DateTimeOffset? antesDe, int limite, CancellationToken cancellationToken);

    /// <summary>Cuántos rubros, de cualquier docente, usan cada tipo (RN-094): solo números.</summary>
    Task<IReadOnlyDictionary<Guid, long>> RubrosPorTipoAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<TipoRubro>> TiposRubroAsync(CancellationToken cancellationToken);

    Task<TipoRubro?> TipoRubroAsync(Guid id, CancellationToken cancellationToken);

    void Agregar(TipoRubro tipo);

    void Agregar(RegistroAuditoriaAdmin registro);
}

/// <summary>RNF-009: borra los grupos archivados vencidos y devuelve sus periodos, para borrar también sus PDF.</summary>
public interface IRetencionGrupos
{
    Task<IReadOnlyList<Guid>> EliminarArchivadosAsync(DateOnly cicloTerminadoAntesDe, CancellationToken cancellationToken);
}
