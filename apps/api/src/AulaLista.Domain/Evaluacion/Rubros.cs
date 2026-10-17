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
    /// (RN-031); la de los criterios, siempre (RN-111).
    /// </summary>
    public static ErrorNegocio? Validar(IReadOnlyList<DatosRubro> rubros, IReadOnlyDictionary<Guid, TipoRubro> tipos)
    {
        if (rubros.Count > Rubro.MaximoPorPeriodo)
        {
            return ErroresEvaluacion.RubrosInvalidos;
        }

        foreach (var rubro in rubros)
        {
            if (string.IsNullOrWhiteSpace(rubro.Nombre) || rubro.Nombre.Trim().Length > Rubro.LongitudNombre
                || !EsPeso(rubro.Peso))
            {
                return ErroresEvaluacion.RubrosInvalidos;
            }

            if (!tipos.TryGetValue(rubro.TipoRubroId, out var tipo) || !tipo.Activo)
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
