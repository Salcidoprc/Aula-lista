using AulaLista.Domain.Comun;
using AulaLista.Domain.Grupos;

namespace AulaLista.Domain.Evaluacion;

/// <summary>Lo que el docente define para un equipo; <see cref="Id"/> conserva el equipo al reorganizar.</summary>
public sealed record DatosEquipo(Guid? Id, string Nombre, IReadOnlyList<Guid> AlumnoIds);

/// <summary>Un equipo del grupo con nombre e integrantes (RN-114). Un alumno está en un solo equipo a la vez.</summary>
public sealed class Equipo : Entidad
{
    public const int LongitudNombre = 40;
    public const int MaximoPorGrupo = 30;

    private readonly List<EquipoAlumno> _integrantes = [];

    private Equipo(Grupo grupo, DateTimeOffset ahora)
        : base(ahora)
    {
        GrupoId = grupo.Id;
        DocenteId = grupo.DocenteId;
    }

    private Equipo()
    {
    }

    public Guid GrupoId { get; private set; }

    public Guid DocenteId { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public int Orden { get; private set; }

    public IReadOnlyList<EquipoAlumno> Integrantes => _integrantes;

    /// <summary>
    /// RN-114: nombres únicos de hasta 40 caracteres, hasta 30 equipos, integrantes activos del grupo y cada alumno
    /// en un solo equipo. Un equipo puede quedar vacío mientras el docente lo arma.
    /// </summary>
    public static ErrorNegocio? Validar(IReadOnlyList<DatosEquipo> equipos, IReadOnlyCollection<Alumno> alumnos)
    {
        if (equipos.Count > MaximoPorGrupo
            || equipos.Any(e => string.IsNullOrWhiteSpace(e.Nombre) || e.Nombre.Trim().Length > LongitudNombre)
            || equipos.Select(e => e.Nombre.Trim().ToLowerInvariant()).Distinct().Count() != equipos.Count)
        {
            return ErroresEvaluacion.EquiposInvalidos;
        }

        var activos = alumnos.Where(a => a.EstaActivo).Select(a => a.Id).ToHashSet();
        var integrantes = equipos.SelectMany(e => e.AlumnoIds).ToList();
        return integrantes.Distinct().Count() != integrantes.Count || integrantes.Any(id => !activos.Contains(id))
            ? ErroresEvaluacion.EquiposInvalidos
            : null;
    }

    public static Equipo Nuevo(Grupo grupo, DatosEquipo datos, int orden, DateTimeOffset ahora)
    {
        var equipo = new Equipo(grupo, ahora);
        equipo.Asignar(datos, orden, ahora);
        return equipo;
    }

    /// <summary>Cambia nombre, orden e integrantes; los que siguen en el equipo conservan su registro.</summary>
    public void Asignar(DatosEquipo datos, int orden, DateTimeOffset ahora)
    {
        Nombre = datos.Nombre.Trim();
        Orden = orden;
        _integrantes.RemoveAll(i => !datos.AlumnoIds.Contains(i.AlumnoId));
        foreach (var alumnoId in datos.AlumnoIds.Where(id => _integrantes.All(i => i.AlumnoId != id)))
        {
            _integrantes.Add(new EquipoAlumno(this, alumnoId, ahora));
        }

        MarcarActualizada(ahora);
    }
}

/// <summary>Un integrante de un equipo (RN-114).</summary>
public sealed class EquipoAlumno : Entidad
{
    internal EquipoAlumno(Equipo equipo, Guid alumnoId, DateTimeOffset ahora)
        : base(ahora)
    {
        EquipoId = equipo.Id;
        DocenteId = equipo.DocenteId;
        AlumnoId = alumnoId;
    }

    private EquipoAlumno()
    {
    }

    public Guid EquipoId { get; private set; }

    public Guid DocenteId { get; private set; }

    public Guid AlumnoId { get; private set; }
}

/// <summary>
/// RN-116: la composición de equipos con la que se calificó una actividad por equipo. Se guarda con la primera
/// calificación por equipo; reorganizar los equipos después no la cambia.
/// </summary>
public sealed class ActividadEquipo : Entidad
{
    private ActividadEquipo(Actividad actividad, Guid equipoId, string nombreEquipo, Guid alumnoId, DateTimeOffset ahora)
        : base(ahora)
    {
        ActividadId = actividad.Id;
        DocenteId = actividad.DocenteId;
        EquipoId = equipoId;
        NombreEquipo = nombreEquipo;
        AlumnoId = alumnoId;
    }

    private ActividadEquipo()
    {
    }

    public Guid ActividadId { get; private set; }

    public Guid DocenteId { get; private set; }

    /// <summary>El equipo de origen; puede ya no existir si el docente lo quitó.</summary>
    public Guid EquipoId { get; private set; }

    public string NombreEquipo { get; private set; } = string.Empty;

    public Guid AlumnoId { get; private set; }

    /// <summary>Una fila por integrante activo de cada equipo, tal como están hoy.</summary>
    public static IReadOnlyList<ActividadEquipo> Tomar(
        Actividad actividad, IEnumerable<Equipo> equipos, IReadOnlySet<Guid> activos, DateTimeOffset ahora) =>
        equipos
            .SelectMany(e => e.Integrantes.Where(i => activos.Contains(i.AlumnoId))
                .Select(i => new ActividadEquipo(actividad, e.Id, e.Nombre, i.AlumnoId, ahora)))
            .ToList();
}
