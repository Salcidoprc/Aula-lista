using AulaLista.Domain.Comun;
using AulaLista.Domain.Grupos;

namespace AulaLista.Domain.Evaluacion;

/// <summary>Cómo se califica una actividad (RN-115).</summary>
public static class ModoActividad
{
    public const string Individual = "individual";
    public const string PorEquipo = "por_equipo";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string> { Individual, PorEquipo };
}

/// <summary>Una tarea, examen o práctica dentro de un rubro de actividades (RF-032, RN-032).</summary>
public sealed class Actividad : Entidad
{
    public const int LongitudNombre = 60;

    private Actividad(Rubro rubro, string nombre, DateOnly? fecha, string modo, int orden, DateTimeOffset ahora)
        : base(ahora)
    {
        RubroId = rubro.Id;
        DocenteId = rubro.DocenteId;
        Nombre = nombre;
        Fecha = fecha;
        Modo = modo;
        Orden = orden;
    }

    private Actividad()
    {
    }

    public Guid RubroId { get; private set; }

    public Guid DocenteId { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public DateOnly? Fecha { get; private set; }

    public string Modo { get; private set; } = ModoActividad.Individual;

    public int Orden { get; private set; }

    /// <summary>RN-039: solo en rubros de actividades y con fecha dentro de la unidad del periodo.</summary>
    public static Resultado<Actividad> Crear(
        Rubro rubro, TipoRubro tipo, Unidad unidad, string nombre, DateOnly? fecha, string modo, int orden, DateTimeOffset ahora)
    {
        if (tipo.Comportamiento != Comportamiento.Actividades)
        {
            return Resultado.Fallo<Actividad>(ErroresEvaluacion.RubroSinActividades);
        }

        return Validar(unidad, nombre, fecha, modo) is { } error
            ? Resultado.Fallo<Actividad>(error)
            : Resultado.Exito(new Actividad(rubro, nombre.Trim(), fecha, modo, orden, ahora));
    }

    /// <summary>
    /// El modo se puede cambiar cuando sea: cada alumno conserva su calificación (RN-117) y la composición de
    /// equipos se toma con la primera calificación por equipo (RN-116).
    /// </summary>
    public Resultado Editar(Unidad unidad, string nombre, DateOnly? fecha, string modo, DateTimeOffset ahora)
    {
        if (Validar(unidad, nombre, fecha, modo) is { } error)
        {
            return Resultado.Fallo(error);
        }

        Nombre = nombre.Trim();
        Fecha = fecha;
        Modo = modo;
        MarcarActualizada(ahora);
        return Resultado.Exito();
    }

    private static ErrorNegocio? Validar(Unidad unidad, string nombre, DateOnly? fecha, string modo)
    {
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Trim().Length > LongitudNombre || !ModoActividad.Todos.Contains(modo))
        {
            return ErroresEvaluacion.ActividadInvalida;
        }

        return fecha is { } f && (f < unidad.Inicio || f > unidad.Fin) ? ErroresEvaluacion.FechaFueraDeUnidad : null;
    }
}

/// <summary>De dónde salió la calificación de un alumno (RN-115).</summary>
public static class OrigenCalificacion
{
    public const string Individual = "individual";
    public const string Equipo = "equipo";
    public const string Ajustada = "ajustada";
}

/// <summary>Las reglas de un rubro que una celda necesita para validarse y calcularse.</summary>
public sealed record ReglasCelda(string Forma, decimal CalifMinima, IReadOnlyList<CriterioCalculo> Criterios);

/// <summary>
/// La calificación de un alumno en una actividad (RN-032, RN-118). Con criterios, <see cref="Valor"/> guarda el
/// resultado de RN-112 y el detalle va en <see cref="Criterios"/>. La última escritura gana; la versión crece.
/// </summary>
public sealed class Calificacion : Entidad
{
    private readonly List<CalificacionCriterio> _criterios = [];

    private Calificacion(Guid actividadId, Guid alumnoId, Guid docenteId, DateTimeOffset ahora)
        : base(ahora)
    {
        ActividadId = actividadId;
        AlumnoId = alumnoId;
        DocenteId = docenteId;
    }

    private Calificacion()
    {
    }

    public Guid ActividadId { get; private set; }

    public Guid AlumnoId { get; private set; }

    public Guid DocenteId { get; private set; }

    public decimal? Valor { get; private set; }

    public string Marca { get; private set; } = MarcaCalificacion.Ninguna;

    public string Origen { get; private set; } = OrigenCalificacion.Individual;

    public int Version { get; private set; }

    public IReadOnlyList<CalificacionCriterio> Criterios => _criterios;

    public bool EstaVacia => Valor is null && Marca == MarcaCalificacion.Ninguna && _criterios.Count == 0;

    public static Calificacion Nueva(Actividad actividad, Guid alumnoId, DateTimeOffset ahora) =>
        new(actividad.Id, alumnoId, actividad.DocenteId, ahora);

    public CeldaCalculo ACalculo() => new(
        Valor, Marca, _criterios.Select(c => new CeldaCriterio(c.CriterioId, c.Valor, c.Marca)).ToList());

    /// <summary>
    /// Valida y aplica lo capturado. Una celda sin valor ni marca queda vacía y no cuenta. <paramref name="origen"/>
    /// dice si vino del alumno, de su equipo o de un ajuste (RN-115); una celda que se vacía vuelve a ser individual.
    /// </summary>
    public Resultado Aplicar(
        ReglasCelda reglas, decimal? valor, string marca, IReadOnlyList<CeldaCriterio> criterios, DateTimeOffset ahora,
        string origen = OrigenCalificacion.Individual)
    {
        if (Validar(reglas, valor, marca, criterios) is { } error)
        {
            return Resultado.Fallo(error);
        }

        Marca = marca;
        _criterios.Clear();
        if (reglas.Criterios.Count > 0)
        {
            if (marca == MarcaCalificacion.Ninguna)
            {
                _criterios.AddRange(criterios
                    .Where(c => c.Valor is not null || c.Marca != MarcaCalificacion.Ninguna)
                    .Select(c => new CalificacionCriterio(this, c.CriterioId, c.Valor, c.Marca)));
            }

            Valor = Calculo.ValorConCriterios(reglas.Criterios, criterios.Where(_ => marca == MarcaCalificacion.Ninguna).ToList());
        }
        else
        {
            Valor = reglas.Forma == FormaCalificacion.Numerica && marca == MarcaCalificacion.Ninguna ? valor : null;
        }

        Origen = EstaVacia ? OrigenCalificacion.Individual : origen;
        Version++;
        MarcarActualizada(ahora);
        return Resultado.Exito();
    }

    /// <summary>RN-113: al cambiar pesos o mínimas de los criterios se recalcula el resultado guardado.</summary>
    public void Recalcular(ReglasCelda reglas, DateTimeOffset ahora)
    {
        if (reglas.Criterios.Count == 0)
        {
            return;
        }

        _criterios.RemoveAll(c => reglas.Criterios.All(r => r.Id != c.CriterioId));
        var nuevo = Marca == MarcaCalificacion.Ninguna ? Calculo.ValorConCriterios(reglas.Criterios, ACalculo().Criterios) : null;
        if (nuevo != Valor)
        {
            Valor = nuevo;
            Version++;
            MarcarActualizada(ahora);
        }
    }

    /// <summary>RN-030, RN-118, RN-119: rango, decimales, mínima y marcas válidas para la forma.</summary>
    public static ErrorNegocio? Validar(ReglasCelda reglas, decimal? valor, string marca, IReadOnlyList<CeldaCriterio> criterios)
    {
        if (!MarcaCalificacion.Todas.Contains(marca))
        {
            return ErroresEvaluacion.MarcaInvalida;
        }

        if (reglas.Criterios.Count > 0)
        {
            if (marca is MarcaCalificacion.Cumplio or MarcaCalificacion.NoCumplio || valor is not null)
            {
                return ErroresEvaluacion.MarcaInvalida; // RN-112: con criterios, la actividad completa solo lleva NE o Exento
            }

            foreach (var celda in criterios)
            {
                if (reglas.Criterios.FirstOrDefault(c => c.Id == celda.CriterioId) is not { } criterio)
                {
                    return ErroresEvaluacion.CriterioInexistente;
                }

                if (celda.Marca is MarcaCalificacion.NoEntrego or MarcaCalificacion.Exento)
                {
                    return ErroresEvaluacion.MarcaInvalida;
                }

                if (ValidarSimple(criterio.Forma, criterio.CalifMinima, celda.Valor, celda.Marca) is { } error)
                {
                    return error;
                }
            }

            return null;
        }

        return criterios.Count > 0 ? ErroresEvaluacion.CriterioInexistente : ValidarSimple(reglas.Forma, reglas.CalifMinima, valor, marca);
    }

    private static ErrorNegocio? ValidarSimple(string forma, decimal minima, decimal? valor, string marca)
    {
        if (forma == FormaCalificacion.Numerica)
        {
            if (marca is MarcaCalificacion.Cumplio or MarcaCalificacion.NoCumplio)
            {
                return ErroresEvaluacion.MarcaInvalida;
            }

            if (valor is not { } v || marca != MarcaCalificacion.Ninguna)
            {
                return valor is null ? null : ErroresEvaluacion.MarcaInvalida;
            }

            if (v is < 0m or > 10m)
            {
                return ErroresEvaluacion.CalificacionFueraDeRango;
            }

            if (decimal.Round(v, 2) != v)
            {
                return ErroresEvaluacion.CalificacionInvalida;
            }

            return v < minima ? ErroresEvaluacion.CalificacionMenorAMinima : null;
        }

        return valor is null ? null : ErroresEvaluacion.MarcaInvalida; // cumplimiento: solo marcas
    }
}

/// <summary>Detalle por criterio de una calificación (RN-112).</summary>
public sealed class CalificacionCriterio : Entidad
{
    internal CalificacionCriterio(Calificacion calificacion, Guid criterioId, decimal? valor, string marca)
        : base(calificacion.UpdatedAt)
    {
        CalificacionId = calificacion.Id;
        DocenteId = calificacion.DocenteId;
        CriterioId = criterioId;
        Valor = valor;
        Marca = marca;
    }

    private CalificacionCriterio()
    {
    }

    public Guid CalificacionId { get; private set; }

    public Guid DocenteId { get; private set; }

    public Guid CriterioId { get; private set; }

    public decimal? Valor { get; private set; }

    public string Marca { get; private set; } = MarcaCalificacion.Ninguna;
}

/// <summary>
/// Un cambio de la cola sin conexión ya aplicado (RF-037). Su clave es el UUID que generó el cliente: si vuelve
/// a llegar, no se aplica otra vez.
/// </summary>
public sealed class CambioSincronizado : Entidad
{
    private CambioSincronizado(Guid clave, Guid docenteId, DateTimeOffset ahora)
        : base(clave, ahora) => DocenteId = docenteId;

    private CambioSincronizado()
    {
    }

    public Guid DocenteId { get; private set; }

    public static CambioSincronizado Registrar(Guid clave, Guid docenteId, DateTimeOffset ahora) => new(clave, docenteId, ahora);
}
