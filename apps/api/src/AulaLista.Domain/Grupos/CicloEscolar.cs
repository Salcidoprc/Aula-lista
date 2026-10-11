using AulaLista.Domain.Comun;

namespace AulaLista.Domain.Grupos;

/// <summary>Cómo se divide el ciclo (RN-024). Define cuántas unidades se proponen y cómo se llaman.</summary>
public static class TipoUnidad
{
    public const string Bimestre = "bimestre";
    public const string Trimestre = "trimestre";
    public const string Cuatrimestre = "cuatrimestre";
    public const string Semestre = "semestre";
    public const string Mensual = "mensual";
    public const string Personalizada = "personalizada";

    public static readonly IReadOnlyList<string> Todos = [Bimestre, Trimestre, Cuatrimestre, Semestre, Mensual, Personalizada];

    /// <summary>Meses que dura una unidad del tipo; nulo en la personalizada.</summary>
    public static int? Meses(string tipo) => tipo switch
    {
        Bimestre => 2,
        Trimestre => 3,
        Cuatrimestre => 4,
        Semestre => 6,
        Mensual => 1,
        _ => null,
    };

    public static string NombreUnidad(string tipo, int numero) => tipo switch
    {
        Bimestre => $"Bimestre {numero}",
        Trimestre => $"Trimestre {numero}",
        Cuatrimestre => $"Cuatrimestre {numero}",
        Semestre => $"Semestre {numero}",
        Mensual => $"Mes {numero}",
        _ => $"Unidad {numero}",
    };
}

/// <summary>Unidad que el docente define o edita: sin id es nueva.</summary>
public sealed record UnidadPropuesta(Guid? Id, string Nombre, DateOnly Inicio, DateOnly Fin);

/// <summary>
/// Ciclo escolar del docente (RN-022) dividido en unidades consecutivas (RN-023). Todos los grupos del
/// ciclo las heredan (RN-025).
/// </summary>
public sealed class CicloEscolar : Entidad
{
    public const int MaximoUnidades = 12;
    public const int LongitudNombre = 20;
    private const int MesesMaximos = 13;

    private readonly List<Unidad> _unidades = [];

    private CicloEscolar(Guid docenteId, string nombre, DateOnly inicio, DateOnly fin, string tipoUnidad, DateTimeOffset ahora)
        : base(ahora)
    {
        DocenteId = docenteId;
        Nombre = nombre;
        Inicio = inicio;
        Fin = fin;
        TipoUnidad = tipoUnidad;
    }

    private CicloEscolar()
    {
    }

    public Guid DocenteId { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public DateOnly Inicio { get; private set; }

    public DateOnly Fin { get; private set; }

    public string TipoUnidad { get; private set; } = Grupos.TipoUnidad.Trimestre;

    public IReadOnlyList<Unidad> Unidades => _unidades;

    /// <summary>Crea el ciclo con las unidades propuestas (RN-024). Sin cantidad, la propone según la duración.</summary>
    public static Resultado<CicloEscolar> Crear(
        Guid docenteId, string nombre, DateOnly inicio, DateOnly fin, string tipoUnidad, int? cantidad, DateTimeOffset ahora)
    {
        if (ValidarFechas(inicio, fin) is { } error)
        {
            return Resultado.Fallo<CicloEscolar>(error);
        }

        if (!Grupos.TipoUnidad.Todos.Contains(tipoUnidad))
        {
            return Resultado.Fallo<CicloEscolar>(ErroresGrupos.TipoUnidadInvalido);
        }

        var total = cantidad ?? CantidadPropuesta(inicio, fin, tipoUnidad);
        if (total is < 1 or > MaximoUnidades)
        {
            return Resultado.Fallo<CicloEscolar>(ErroresGrupos.CantidadUnidadesInvalida);
        }

        var ciclo = new CicloEscolar(docenteId, nombre.Trim(), inicio, fin, tipoUnidad, ahora);
        var dias = fin.DayNumber - inicio.DayNumber + 1;
        if (total > dias)
        {
            return Resultado.Fallo<CicloEscolar>(ErroresGrupos.CantidadUnidadesInvalida);
        }

        for (var i = 0; i < total; i++)
        {
            var desde = inicio.AddDays(i * dias / total);
            var hasta = inicio.AddDays(((i + 1) * dias / total) - 1);
            ciclo._unidades.Add(new Unidad(ciclo, Grupos.TipoUnidad.NombreUnidad(tipoUnidad, i + 1), i + 1, desde, hasta, ahora));
        }

        return Resultado.Exito(ciclo);
    }

    /// <summary>
    /// Cantidad que corresponde a la duración (RN-024): piso de meses entre meses por unidad más 0.25, para que
    /// un ciclo de agosto a julio dé 3 trimestres, 5 bimestres y 2 semestres (ADR-0014).
    /// </summary>
    public static int CantidadPropuesta(DateOnly inicio, DateOnly fin, string tipoUnidad)
    {
        if (Grupos.TipoUnidad.Meses(tipoUnidad) is not { } meses)
        {
            return 1;
        }

        var duracion = (fin.DayNumber - inicio.DayNumber + 1) / 30.44m;
        return Math.Clamp((int)Math.Floor((duracion / meses) + 0.25m), 1, MaximoUnidades);
    }

    /// <summary>Cambia nombre y fechas; las unidades deben seguir dentro del ciclo.</summary>
    public Resultado Editar(string nombre, DateOnly inicio, DateOnly fin, DateTimeOffset ahora)
    {
        if (ValidarFechas(inicio, fin) is { } error)
        {
            return Resultado.Fallo(error);
        }

        if (_unidades.Any(u => u.Inicio < inicio || u.Fin > fin))
        {
            return Resultado.Fallo(ErroresGrupos.UnidadFueraDeCiclo);
        }

        Nombre = nombre.Trim();
        Inicio = inicio;
        Fin = fin;
        MarcarActualizada(ahora);
        return Resultado.Exito();
    }

    /// <summary>
    /// Reemplaza las unidades (RN-023, RN-026, RN-027). Las que traen id se editan, las demás se agregan y las que
    /// faltan se borran. Una unidad cerrada en algún grupo no cambia de fechas ni se borra. Devuelve las nuevas.
    /// </summary>
    public Resultado<IReadOnlyList<Unidad>> ReemplazarUnidades(
        IReadOnlyList<UnidadPropuesta> propuestas, IReadOnlySet<Guid> cerradas, DateTimeOffset ahora)
    {
        if (propuestas.Count is < 1 or > MaximoUnidades)
        {
            return Resultado.Fallo<IReadOnlyList<Unidad>>(ErroresGrupos.CantidadUnidadesInvalida);
        }

        if (propuestas.Any(p => p.Fin < p.Inicio))
        {
            return Resultado.Fallo<IReadOnlyList<Unidad>>(ErroresGrupos.UnidadFechasInvalidas);
        }

        if (propuestas.Any(p => p.Inicio < Inicio || p.Fin > Fin))
        {
            return Resultado.Fallo<IReadOnlyList<Unidad>>(ErroresGrupos.UnidadFueraDeCiclo);
        }

        var conId = propuestas.Where(p => p.Id is not null).Select(p => p.Id!.Value).ToList();
        if (conId.Distinct().Count() != conId.Count || conId.Any(id => _unidades.All(u => u.Id != id)))
        {
            return Resultado.Fallo<IReadOnlyList<Unidad>>(ErroresGrupos.UnidadInexistente);
        }

        var ordenadas = propuestas.OrderBy(p => p.Inicio).ToList();
        for (var i = 1; i < ordenadas.Count; i++)
        {
            if (ordenadas[i].Inicio <= ordenadas[i - 1].Fin)
            {
                return Resultado.Fallo<IReadOnlyList<Unidad>>(ErroresGrupos.UnidadesTraslapadas);
            }
        }

        foreach (var cerrada in _unidades.Where(u => cerradas.Contains(u.Id)))
        {
            var propuesta = propuestas.FirstOrDefault(p => p.Id == cerrada.Id);
            if (propuesta is null || propuesta.Inicio != cerrada.Inicio || propuesta.Fin != cerrada.Fin)
            {
                return Resultado.Fallo<IReadOnlyList<Unidad>>(ErroresGrupos.UnidadCerrada);
            }
        }

        _unidades.RemoveAll(u => !conId.Contains(u.Id));
        var nuevas = new List<Unidad>();
        for (var i = 0; i < ordenadas.Count; i++)
        {
            var p = ordenadas[i];
            if (p.Id is { } id)
            {
                _unidades.Single(u => u.Id == id).Actualizar(p.Nombre.Trim(), i + 1, p.Inicio, p.Fin, ahora);
            }
            else
            {
                var unidad = new Unidad(this, p.Nombre.Trim(), i + 1, p.Inicio, p.Fin, ahora);
                _unidades.Add(unidad);
                nuevas.Add(unidad);
            }
        }

        MarcarActualizada(ahora);
        return Resultado.Exito<IReadOnlyList<Unidad>>(nuevas);
    }

    private static ErrorNegocio? ValidarFechas(DateOnly inicio, DateOnly fin)
    {
        if (fin <= inicio)
        {
            return ErroresGrupos.CicloFechasInvalidas;
        }

        return fin > inicio.AddMonths(MesesMaximos) ? ErroresGrupos.CicloMuyLargo : null;
    }
}

/// <summary>Tramo del ciclo con nombre y fechas (RN-023). Cada grupo la evalúa en su <see cref="Periodo"/>.</summary>
public sealed class Unidad : Entidad
{
    public const int LongitudNombre = 40;

    internal Unidad(CicloEscolar ciclo, string nombre, int orden, DateOnly inicio, DateOnly fin, DateTimeOffset ahora)
        : base(ahora)
    {
        CicloEscolarId = ciclo.Id;
        DocenteId = ciclo.DocenteId;
        Nombre = nombre;
        Orden = orden;
        Inicio = inicio;
        Fin = fin;
    }

    private Unidad()
    {
    }

    public Guid CicloEscolarId { get; private set; }

    public Guid DocenteId { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public int Orden { get; private set; }

    public DateOnly Inicio { get; private set; }

    public DateOnly Fin { get; private set; }

    internal void Actualizar(string nombre, int orden, DateOnly inicio, DateOnly fin, DateTimeOffset ahora)
    {
        Nombre = nombre;
        Orden = orden;
        Inicio = inicio;
        Fin = fin;
        MarcarActualizada(ahora);
    }
}
