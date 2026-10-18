using AulaLista.Domain.Evaluacion;

namespace AulaLista.Application.Evaluacion;

/// <summary>Rubros, criterios, actividades, calificaciones, equipos, notas y asistencia: tablas con RLS por docente (SPEC-03, SPEC-10).</summary>
public interface IRepositorioEvaluacion
{
    Task<IReadOnlyList<TipoRubro>> TiposRubroAsync(CancellationToken cancellationToken);

    /// <summary>Rubros del periodo con sus criterios, en orden.</summary>
    Task<IReadOnlyList<Rubro>> RubrosAsync(Guid periodoId, CancellationToken cancellationToken);

    Task<Rubro?> RubroAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Rubro>> RubrosPorIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    void Agregar(Rubro rubro);

    void Borrar(Rubro rubro);

    Task<IReadOnlyList<Actividad>> ActividadesAsync(IReadOnlyCollection<Guid> rubroIds, CancellationToken cancellationToken);

    Task<Actividad?> ActividadAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Actividad>> ActividadesPorIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    void Agregar(Actividad actividad);

    void Borrar(Actividad actividad);

    /// <summary>Calificaciones con su detalle por criterio de las actividades dadas.</summary>
    Task<IReadOnlyList<Calificacion>> CalificacionesAsync(IReadOnlyCollection<Guid> actividadIds, CancellationToken cancellationToken);

    void Agregar(Calificacion calificacion);

    /// <summary>Rubros que tienen alguna celda capturada (no vacía).</summary>
    Task<IReadOnlySet<Guid>> RubrosConCalificacionesAsync(Guid periodoId, CancellationToken cancellationToken);

    /// <summary>Criterios con algún valor capturado.</summary>
    Task<IReadOnlySet<Guid>> CriteriosConCalificacionesAsync(Guid periodoId, CancellationToken cancellationToken);

    /// <summary>Las claves de la cola sin conexión que ya se aplicaron.</summary>
    Task<IReadOnlySet<Guid>> CambiosAplicadosAsync(IReadOnlyCollection<Guid> claves, CancellationToken cancellationToken);

    void Agregar(CambioSincronizado cambio);

    /// <summary>Equipos del grupo con sus integrantes, en orden (RN-114).</summary>
    Task<IReadOnlyList<Equipo>> EquiposAsync(Guid grupoId, CancellationToken cancellationToken);

    void Agregar(Equipo equipo);

    void Borrar(Equipo equipo);

    /// <summary>Composición guardada de las actividades por equipo dadas (RN-116).</summary>
    Task<IReadOnlyList<ActividadEquipo>> ComposicionesAsync(IReadOnlyCollection<Guid> actividadIds, CancellationToken cancellationToken);

    void Agregar(ActividadEquipo integrante);

    Task<IReadOnlyList<Nota>> NotasAsync(Guid alumnoId, CancellationToken cancellationToken);

    Task<Nota?> NotaAsync(Guid id, CancellationToken cancellationToken);

    void Agregar(Nota nota);

    /// <summary>Tomas del grupo con fecha en el rango (inclusive), con el estado de cada alumno (RN-100).</summary>
    Task<IReadOnlyList<TomaAsistencia>> TomasAsync(Guid grupoId, DateOnly desde, DateOnly hasta, CancellationToken cancellationToken);

    Task<TomaAsistencia?> TomaAsync(Guid grupoId, DateOnly fecha, CancellationToken cancellationToken);

    void Agregar(TomaAsistencia toma);

    void Borrar(TomaAsistencia toma);
}
