namespace AulaLista.Application.Cierre;

/// <summary>
/// RN-044: cuando cambian las calificaciones, la asistencia o las notas de un alumno, su observación aprobada (y sin
/// publicar) vuelve a borrador con la marca "datos cambiaron, revisa". La llaman la captura, las notas y la asistencia.
/// </summary>
public sealed class AvisoDatosCambiaron(IRepositorioCierre repositorio, TimeProvider reloj)
{
    /// <param name="alumnoIds">Los alumnos afectados, o nulo si es todo el grupo (por ejemplo, al borrar una actividad).</param>
    public Task AvisarAsync(Guid periodoId, IReadOnlyCollection<Guid>? alumnoIds, CancellationToken ct) =>
        alumnoIds is { Count: 0 } ? Task.CompletedTask : repositorio.MarcarDatosCambiaronAsync(periodoId, alumnoIds, reloj.GetUtcNow(), ct);
}
