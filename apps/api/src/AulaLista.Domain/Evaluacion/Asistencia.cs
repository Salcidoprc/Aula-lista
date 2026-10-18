using AulaLista.Domain.Comun;
using AulaLista.Domain.Grupos;

namespace AulaLista.Domain.Evaluacion;

/// <summary>Estado de un alumno en una toma (RN-101), en el orden en que cambia con cada toque.</summary>
public static class EstadoAsistencia
{
    public const string Presente = "presente";
    public const string Falta = "falta";
    public const string Retardo = "retardo";
    public const string Justificada = "justificada";

    public static readonly IReadOnlyList<string> Todos = [Presente, Falta, Retardo, Justificada];
}

/// <summary>
/// La lista de un grupo en una fecha (RN-100): una por grupo y fecha, dentro de una unidad del ciclo. Al crearla
/// todos los alumnos activos quedan presentes (RN-101); los que se dan de alta o de baja después no cuentan en
/// ella (RN-105).
/// </summary>
public sealed class TomaAsistencia : Entidad
{
    private readonly List<Asistencia> _alumnos = [];

    private TomaAsistencia(Grupo grupo, DateOnly fecha, DateTimeOffset ahora)
        : base(ahora)
    {
        GrupoId = grupo.Id;
        DocenteId = grupo.DocenteId;
        Fecha = fecha;
    }

    private TomaAsistencia()
    {
    }

    public Guid GrupoId { get; private set; }

    public Guid DocenteId { get; private set; }

    public DateOnly Fecha { get; private set; }

    public IReadOnlyList<Asistencia> Alumnos => _alumnos;

    public static TomaAsistencia Crear(Grupo grupo, DateOnly fecha, IEnumerable<Alumno> alumnos, DateTimeOffset ahora)
    {
        var toma = new TomaAsistencia(grupo, fecha, ahora);
        foreach (var alumno in alumnos.Where(a => a.EstaActivo))
        {
            toma._alumnos.Add(new Asistencia(toma, alumno.Id, EstadoAsistencia.Presente, ahora));
        }

        return toma;
    }

    /// <summary>
    /// Cambia el estado de un alumno: por alumno gana la última escritura (SPEC-10, casos borde). Un alumno activo
    /// que no estaba en la toma se agrega; uno dado de baja solo se corrige si ya estaba.
    /// </summary>
    public ErrorNegocio? Marcar(Alumno alumno, string estado, DateTimeOffset ahora)
    {
        if (!EstadoAsistencia.Todos.Contains(estado))
        {
            return ErroresEvaluacion.AsistenciaInvalida;
        }

        if (_alumnos.FirstOrDefault(a => a.AlumnoId == alumno.Id) is { } actual)
        {
            actual.Cambiar(estado, ahora);
        }
        else if (alumno.EstaActivo)
        {
            _alumnos.Add(new Asistencia(this, alumno.Id, estado, ahora));
        }
        else
        {
            return ErroresGrupos.AlumnoDadoDeBaja;
        }

        MarcarActualizada(ahora);
        return null;
    }
}

/// <summary>El estado de un alumno en una toma. <see cref="Version"/> sube con cada cambio.</summary>
public sealed class Asistencia : Entidad
{
    internal Asistencia(TomaAsistencia toma, Guid alumnoId, string estado, DateTimeOffset ahora)
        : base(ahora)
    {
        TomaAsistenciaId = toma.Id;
        DocenteId = toma.DocenteId;
        AlumnoId = alumnoId;
        Estado = estado;
        Version = 1;
    }

    private Asistencia()
    {
    }

    public Guid TomaAsistenciaId { get; private set; }

    public Guid DocenteId { get; private set; }

    public Guid AlumnoId { get; private set; }

    public string Estado { get; private set; } = EstadoAsistencia.Presente;

    public int Version { get; private set; }

    internal void Cambiar(string estado, DateTimeOffset ahora)
    {
        if (Estado != estado)
        {
            Estado = estado;
            Version++;
            MarcarActualizada(ahora);
        }
    }
}
