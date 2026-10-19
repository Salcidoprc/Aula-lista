using System.ComponentModel.DataAnnotations;
using AulaLista.Domain.Cierre;

namespace AulaLista.Application.Cierre;

/// <summary>Observaciones, estilo y llamadas a la IA: tablas con RLS por docente (SPEC-04).</summary>
public interface IRepositorioCierre
{
    Task<IReadOnlyList<Observacion>> ObservacionesAsync(Guid periodoId, CancellationToken cancellationToken);

    Task<Observacion?> ObservacionAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Observacion>> ObservacionesPorIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    void Agregar(Observacion observacion);

    /// <summary>
    /// RN-044: las aprobadas de esos alumnos (o de todos, si es nulo) vuelven a borrador con la marca de revisión.
    /// Corre en la transacción de la solicitud.
    /// </summary>
    Task MarcarDatosCambiaronAsync(Guid periodoId, IReadOnlyCollection<Guid>? alumnoIds, DateTimeOffset ahora, CancellationToken cancellationToken);

    Task<EstiloObservaciones?> EstiloAsync(Guid docenteId, CancellationToken cancellationToken);

    void Agregar(EstiloObservaciones estilo);

    void Agregar(LlamadaIa llamada);

    /// <summary>Llamadas a la IA del docente desde esa hora más las observaciones que esperan la suya (RNF-004).</summary>
    Task<int> SolicitudesIaDesdeAsync(DateTimeOffset desde, DateTimeOffset ahora, CancellationToken cancellationToken);
}

/// <summary>Un rubro como lo ve la IA: sin ids ni nombres de actividades.</summary>
public sealed record RubroParaIa(string Nombre, decimal Peso, decimal? Promedio, int NoEntrego);

public sealed record NotaParaIa(DateOnly Fecha, string Etiqueta, string Texto);

public sealed record EstiloParaIa(string Longitud, string Persona, IReadOnlyList<string> Ejemplos);

/// <summary>
/// Lo único que viaja a la IA por alumno (SPEC-04, Contrato con la IA), ya seudonimizado (RN-019): nunca nombres,
/// teléfonos, correos, escuela ni CCT.
/// </summary>
public sealed record EntradaObservacion(
    string Alumno,
    string Trato,
    string Nivel,
    int Grado,
    string? Materia,
    string Periodo,
    decimal? CalificacionPeriodo,
    decimal MinimaAprobatoria,
    IReadOnlyList<RubroParaIa> Rubros,
    IReadOnlyList<NotaParaIa> Notas,
    EstiloParaIa Estilo,
    string? InstruccionExtra);

/// <summary>Lo que devolvió la IA. Sin texto si se rechazó, se cortó o falló (<see cref="Fallo"/>).</summary>
public sealed record ObservacionRedactada(
    string? Texto, string? Fallo, string Modelo, string VersionPrompt, int TokensEntrada, int TokensSalida, int TokensCacheLeidos, int DuracionMs);

/// <summary>Redacta el borrador de una observación con la IA (02-arquitectura, Integraciones).</summary>
public interface IRedactorObservaciones
{
    Task<ObservacionRedactada> RedactarAsync(EntradaObservacion entrada, CancellationToken cancellationToken);
}

/// <summary>Modelo, esfuerzo y límites de la IA (reglas/05-ia): viven en configuración, nunca en el código.</summary>
public sealed class OpcionesIa
{
    public const string Seccion = "Ia";
    public const string ProveedorClaude = "claude";
    public const string ProveedorSimulado = "simulado";

    /// <summary><c>claude</c> en staging y producción; <c>simulado</c> en local y pruebas, sin red ni costo.</summary>
    [Required]
    [AllowedValues(ProveedorClaude, ProveedorSimulado)]
    public string Proveedor { get; init; } = ProveedorSimulado;

    [Required]
    public string Modelo { get; init; } = string.Empty;

    [Required]
    [AllowedValues("low", "medium", "high")]
    public string Esfuerzo { get; init; } = "low";

    [Range(200, 8000)]
    public int MaximoTokensSalida { get; init; } = 1200;

    /// <summary>Reintentos ante errores pasajeros, con espera exponencial (SPEC-04, Errores).</summary>
    [Range(0, 5)]
    public int Reintentos { get; init; } = 3;

    [Range(1, 20)]
    public int SimultaneasPorDocente { get; init; } = 5;

    [Range(1, 10000)]
    public int SolicitudesPorHora { get; init; } = 200;

    /// <summary>Clave de la API; solo por variable de entorno (<c>Ia__ApiKey</c>).</summary>
    public string? ApiKey { get; init; }

    /// <summary>SPEC-09, costos: precio del modelo en dólares por millón de tokens de entrada.</summary>
    [Range(typeof(decimal), "0", "1000")]
    public decimal PrecioEntradaUsdPorMillon { get; init; }

    [Range(typeof(decimal), "0", "1000")]
    public decimal PrecioSalidaUsdPorMillon { get; init; }

    /// <summary>Tokens leídos de la caché de prompts.</summary>
    [Range(typeof(decimal), "0", "1000")]
    public decimal PrecioCacheLeidoUsdPorMillon { get; init; }
}
