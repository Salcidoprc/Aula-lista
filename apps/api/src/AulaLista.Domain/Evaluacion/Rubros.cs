using AulaLista.Domain.Comun;
using AulaLista.Domain.Grupos;

namespace AulaLista.Domain.Evaluacion;

/// <summary>
/// Tipo de rubro del catálogo global (ADR-0012): nombre, comportamiento de cálculo y valores sugeridos. Lo edita
/// solo el administrador (panel en H6); la app lo lee.
/// </summary>
public sealed class TipoRubro : Entidad
{
    private TipoRubro(Guid id, string nombre, string comportamiento, string formaSugerida, string[] nombresSugeridos, int orden, DateTimeOffset ahora)
        : base(id, ahora)
    {
        Nombre = nombre;
        Comportamiento = comportamiento;
        FormaSugerida = formaSugerida;
        NombresSugeridos = nombresSugeridos;
        Orden = orden;
        Activo = true;
    }

    private TipoRubro()
    {
    }

    /// <summary>Tipo activo con id fijo: el catálogo sembrado y las pruebas.</summary>
    public static TipoRubro Nuevo(
        Guid id, string nombre, string comportamiento, string formaSugerida, string[] nombresSugeridos, int orden, DateTimeOffset ahora) =>
        new(id, nombre, comportamiento, formaSugerida, nombresSugeridos, orden, ahora);

    public string Nombre { get; private set; } = string.Empty;

    public string Descripcion { get; private set; } = string.Empty;

    public string Icono { get; private set; } = string.Empty;

    public string Comportamiento { get; private set; } = Evaluacion.Comportamiento.Actividades;

    public string FormaSugerida { get; private set; } = FormaCalificacion.Numerica;

    public string[] NombresSugeridos { get; private set; } = [];

    public int Orden { get; private set; }

    public bool Activo { get; private set; }

    /// <summary>RN-092: un tipo nuevo del panel queda activo y disponible al instante.</summary>
    public static TipoRubro Crear(DatosTipoRubro datos, DateTimeOffset ahora)
    {
        var tipo = new TipoRubro(Guid.CreateVersion7(ahora), datos.Nombre.Trim(), datos.Comportamiento, datos.FormaSugerida, [], datos.Orden, ahora);
        tipo.Asignar(datos);
        return tipo;
    }

    /// <summary>RN-094: desactivar lo oculta al crear rubros; los que ya lo usan siguen igual.</summary>
    public void Editar(DatosTipoRubro datos, DateTimeOffset ahora)
    {
        Asignar(datos);
        MarcarActualizada(ahora);
    }

    private void Asignar(DatosTipoRubro datos)
    {
        Nombre = datos.Nombre.Trim();
        Descripcion = datos.Descripcion.Trim();
        Icono = datos.Icono;
        Comportamiento = datos.Comportamiento;
        FormaSugerida = datos.FormaSugerida;
        NombresSugeridos = datos.NombresSugeridos.Select(n => n.Trim()).ToArray();
        Orden = datos.Orden;
        Activo = datos.Activo;
    }
}

/// <summary>Lo que el administrador define para un tipo de rubro (RN-091).</summary>
public sealed record DatosTipoRubro(
    string Nombre, string Descripcion, string Icono, string Comportamiento, string FormaSugerida, IReadOnlyList<string> NombresSugeridos,
    int Orden, bool Activo);

/// <summary>Reglas del catálogo de tipos de rubro (RN-091 a RN-094).</summary>
public static class ReglasTipoRubro
{
    public const int LongitudNombre = 40;
    public const int LongitudDescripcion = 200;
    public const int LongitudIcono = 40;
    public const int MaximoNombresSugeridos = 5;
    public const int OrdenMaximo = 999;

    public static ErrorNegocio? Validar(DatosTipoRubro datos)
    {
        ArgumentNullException.ThrowIfNull(datos);
        if (!Evaluacion.Comportamiento.Todos.Contains(datos.Comportamiento))
        {
            return Operacion.ErroresOperacion.ComportamientoInexistente;
        }

        var valido = !string.IsNullOrWhiteSpace(datos.Nombre) && datos.Nombre.Trim().Length <= LongitudNombre
            && datos.Descripcion is not null && datos.Descripcion.Trim().Length <= LongitudDescripcion
            && EsIcono(datos.Icono)
            && FormaCalificacion.Todas.Contains(datos.FormaSugerida)
            && datos.NombresSugeridos is { Count: >= 1 and <= MaximoNombresSugeridos }
            && datos.NombresSugeridos.All(n => !string.IsNullOrWhiteSpace(n) && n.Trim().Length <= Rubro.LongitudNombre)
            && datos.Orden is >= 1 and <= OrdenMaximo;
        return valido ? null : Operacion.ErroresOperacion.TipoRubroDatosInvalidos;
    }

    /// <summary>El icono es una clave de la app (minúsculas y guion bajo), no una imagen.</summary>
    private static bool EsIcono(string? icono) =>
        !string.IsNullOrEmpty(icono) && icono.Length <= LongitudIcono && icono.All(c => c is (>= 'a' and <= 'z') or '_');
}

/// <summary>Lo que el docente define para un rubro o criterio; se valida antes de tocar entidades.</summary>
public sealed record DatosCriterio(Guid? Id, string Nombre, decimal Peso, string Forma, decimal CalifMinima);

public sealed record DatosRubro(
    Guid? Id, string Nombre, Guid TipoRubroId, decimal Peso, string Forma, decimal CalifMinima, IReadOnlyList<DatosCriterio> Criterios);

/// <summary>Un rubro del periodo (RN-031, RN-110): nombre libre, tipo del catálogo, peso, forma y mínima.</summary>
public sealed class Rubro : Entidad
{
    public const int LongitudNombre = 40;
    public const int MaximoPorPeriodo = 20;
    public const int MinimoCriterios = 2;
    public const int MaximoCriterios = 10;

    private readonly List<Criterio> _criterios = [];

    private Rubro(Periodo periodo, DatosRubro datos, int orden, DateTimeOffset ahora)
        : base(ahora)
    {
        PeriodoId = periodo.Id;
        DocenteId = periodo.DocenteId;
        Asignar(datos, orden);
    }

    private Rubro()
    {
    }

    public Guid PeriodoId { get; private set; }

    public Guid DocenteId { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public Guid TipoRubroId { get; private set; }

    public decimal Peso { get; private set; }

    public string Forma { get; private set; } = FormaCalificacion.Numerica;

    public decimal CalifMinima { get; private set; }

    public int Orden { get; private set; }

    public IReadOnlyList<Criterio> Criterios => _criterios;

    public static Rubro Crear(Periodo periodo, DatosRubro datos, int orden, DateTimeOffset ahora)
    {
        var rubro = new Rubro(periodo, datos, orden, ahora);
        rubro.ReemplazarCriterios(datos.Criterios, ahora);
        return rubro;
    }

    /// <summary>Cambia los datos del rubro; los criterios que ya no vienen se quitan (RN-113).</summary>
    public void Actualizar(DatosRubro datos, int orden, DateTimeOffset ahora)
    {
        Asignar(datos, orden);
        ReemplazarCriterios(datos.Criterios, ahora);
        MarcarActualizada(ahora);
    }

    /// <summary>Copia del rubro en otro periodo (RF-031), con sus criterios.</summary>
    public Rubro CopiarA(Periodo destino, DateTimeOffset ahora) => Crear(
        destino,
        new DatosRubro(null, Nombre, TipoRubroId, Peso, Forma, CalifMinima,
            _criterios.Select(c => new DatosCriterio(null, c.Nombre, c.Peso, c.Forma, c.CalifMinima)).ToList()),
        Orden,
        ahora);

    private void Asignar(DatosRubro datos, int orden)
    {
        Nombre = datos.Nombre.Trim();
        TipoRubroId = datos.TipoRubroId;
        Peso = datos.Peso;
        Forma = datos.Forma;
        CalifMinima = datos.CalifMinima;
        Orden = orden;
    }

    private void ReemplazarCriterios(IReadOnlyList<DatosCriterio> criterios, DateTimeOffset ahora)
    {
        _criterios.RemoveAll(c => criterios.All(d => d.Id != c.Id));
        for (var i = 0; i < criterios.Count; i++)
        {
            var datos = criterios[i];
            if (_criterios.FirstOrDefault(c => c.Id == datos.Id) is { } existente)
            {
                existente.Actualizar(datos, i, ahora);
            }
            else
            {
                _criterios.Add(new Criterio(this, datos, i, ahora));
            }
        }

        _criterios.Sort((a, b) => a.Orden.CompareTo(b.Orden));
    }
}

/// <summary>Subrubro con peso, forma y mínima propios (RN-111, RN-119).</summary>
public sealed class Criterio : Entidad
{
    public const int LongitudNombre = 40;

    internal Criterio(Rubro rubro, DatosCriterio datos, int orden, DateTimeOffset ahora)
        : base(ahora)
    {
        RubroId = rubro.Id;
        DocenteId = rubro.DocenteId;
        Asignar(datos, orden);
    }

    private Criterio()
    {
    }

    public Guid RubroId { get; private set; }

    public Guid DocenteId { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public decimal Peso { get; private set; }

    public string Forma { get; private set; } = FormaCalificacion.Numerica;

    public decimal CalifMinima { get; private set; }

    public int Orden { get; private set; }

    internal void Actualizar(DatosCriterio datos, int orden, DateTimeOffset ahora)
    {
        Asignar(datos, orden);
        MarcarActualizada(ahora);
    }

    private void Asignar(DatosCriterio datos, int orden)
    {
        Nombre = datos.Nombre.Trim();
        Peso = datos.Peso;
        Forma = datos.Forma;
        CalifMinima = datos.CalifMinima;
        Orden = orden;
    }
}

/// <summary>Reglas del conjunto de rubros de un periodo (RN-031, RN-110, RN-111, RN-118).</summary>
public static class ReglasRubros
{
    /// <summary>
    /// Valida lo que se puede guardar como borrador: la suma 100 de los rubros solo se exige al iniciar el cierre
    /// (RN-031); la de los criterios, siempre (RN-111). <paramref name="tiposActuales"/> es el tipo de cada rubro ya
    /// guardado: un rubro que conserva un tipo desactivado sigue siendo válido (RN-094).
    /// </summary>
    public static ErrorNegocio? Validar(
        IReadOnlyList<DatosRubro> rubros, IReadOnlyDictionary<Guid, TipoRubro> tipos, IReadOnlyDictionary<Guid, Guid>? tiposActuales = null)
    {
        if (rubros.Count > Rubro.MaximoPorPeriodo)
        {
            return ErroresEvaluacion.RubrosInvalidos;
        }

        // RN-104: un solo rubro de asistencia por unidad y grupo.
        if (rubros.Count(r => tipos.TryGetValue(r.TipoRubroId, out var t) && t.Comportamiento == Comportamiento.Asistencia) > 1)
        {
            return ErroresEvaluacion.AsistenciaDuplicada;
        }

        foreach (var rubro in rubros)
        {
            if (string.IsNullOrWhiteSpace(rubro.Nombre) || rubro.Nombre.Trim().Length > Rubro.LongitudNombre
                || !EsPeso(rubro.Peso))
            {
                return ErroresEvaluacion.RubrosInvalidos;
            }

            if (!tipos.TryGetValue(rubro.TipoRubroId, out var tipo)
                || (!tipo.Activo && !(rubro.Id is { } id && tiposActuales?.GetValueOrDefault(id) == rubro.TipoRubroId)))
            {
                return ErroresEvaluacion.TipoRubroInvalido;
            }

            if (ValidarForma(rubro.Forma, rubro.CalifMinima) is { } errorForma)
            {
                return errorForma;
            }

            if (rubro.Criterios.Count == 0)
            {
                continue;
            }

            if (tipo.Comportamiento != Comportamiento.Actividades
                || rubro.Criterios.Count is < Rubro.MinimoCriterios or > Rubro.MaximoCriterios
                || rubro.Criterios.Any(c => string.IsNullOrWhiteSpace(c.Nombre) || c.Nombre.Trim().Length > Criterio.LongitudNombre || !EsPeso(c.Peso)))
            {
                return ErroresEvaluacion.CriteriosInvalidos;
            }

            if (rubro.Criterios.Select(c => ValidarForma(c.Forma, c.CalifMinima)).FirstOrDefault(e => e is not null) is { } errorCriterio)
            {
                return errorCriterio;
            }

            if (rubro.Criterios.Sum(c => c.Peso) != 100m)
            {
                return ErroresEvaluacion.PesosCriteriosNoSuman100; // RN-111
            }
        }

        return null;
    }

    /// <summary>RN-031: al iniciar el cierre los pesos deben sumar exactamente 100.</summary>
    public static bool SumanCien(IEnumerable<decimal> pesos) => pesos.Sum() == 100m;

    private static ErrorNegocio? ValidarForma(string forma, decimal minima)
    {
        if (!FormaCalificacion.Todas.Contains(forma))
        {
            return ErroresEvaluacion.FormaInvalida;
        }

        return minima is < 0m or > 10m || decimal.Round(minima, 2) != minima ? ErroresEvaluacion.MinimaInvalida : null;
    }

    private static bool EsPeso(decimal peso) => peso is >= 0m and <= 100m && decimal.Round(peso, 2) == peso;
}
