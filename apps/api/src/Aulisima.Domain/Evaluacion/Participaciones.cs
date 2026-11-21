using Aulisima.Domain.Comun;
using Aulisima.Domain.Grupos;

namespace Aulisima.Domain.Evaluacion;

/// <summary>
/// Una participación extra que el docente dio a un alumno en clase (RN-122, ADR-0019). La fecha decide la unidad. Cada
/// una es un registro nuevo con el id que genera el cliente: dos dispositivos sin señal nunca chocan y reenviarla no la
/// duplica. Quitarla es borrar el registro. Lo que vale lo decide el grupo al calcular (RN-123).
/// </summary>
public sealed class Participacion : Entidad
{
    private Participacion(Guid id, Alumno alumno, Guid periodoId, DateOnly fecha, DateTimeOffset ahora)
        : base(id, ahora)
    {
        GrupoId = alumno.GrupoId;
        AlumnoId = alumno.Id;
        DocenteId = alumno.DocenteId;
        PeriodoId = periodoId;
        Fecha = fecha;
    }

    private Participacion()
    {
    }

    public Guid GrupoId { get; private set; }

    public Guid AlumnoId { get; private set; }

    public Guid DocenteId { get; private set; }

    public Guid PeriodoId { get; private set; }

    public DateOnly Fecha { get; private set; }

    /// <summary>Solo a alumnos activos (RN-122); la unidad la valida quien la crea (RN-125).</summary>
    public static Resultado<Participacion> Crear(Guid id, Alumno alumno, Periodo periodo, DateOnly fecha, DateTimeOffset ahora)
    {
        if (id == Guid.Empty || periodo.GrupoId != alumno.GrupoId)
        {
            return Resultado.Fallo<Participacion>(ErroresEvaluacion.ParticipacionInvalida);
        }

        return alumno.EstaActivo
            ? Resultado.Exito(new Participacion(id, alumno, periodo.Id, fecha, ahora))
            : Resultado.Fallo<Participacion>(ErroresGrupos.AlumnoDadoDeBaja);
    }
}

/// <summary>RN-123: puntos extra = participaciones de la unidad por el valor vigente del grupo.</summary>
public static class ReglasParticipacion
{
    public static decimal Puntos(int cantidad, decimal valor) => cantidad * valor;
}
