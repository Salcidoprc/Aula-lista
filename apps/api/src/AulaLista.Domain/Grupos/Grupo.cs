using AulaLista.Domain.Comun;
using AulaLista.Domain.Cuentas;

namespace AulaLista.Domain.Grupos;

/// <summary>Sentido del redondeo de la calificación final (RN-035).</summary>
public static class Redondeo
{
    public const string Arriba = "arriba";
    public const string Abajo = "abajo";
    public const string Cercano = "cercano";

    public static readonly IReadOnlyList<string> Todos = [Arriba, Abajo, Cercano];
}

public static class PlanGrupo
{
    public const string Gratis = "gratis";
    public const string Pro = "pro";
}

/// <summary>Configuración de calificación del grupo (RN-030 y RN-035).</summary>
public sealed record ConfiguracionCalificacion(decimal CalifMinima, decimal CalifAprobatoria, int DecimalesFinal, string Redondeo)
{
    public static readonly ConfiguracionCalificacion PorDefecto = new(5m, 6m, 2, Grupos.Redondeo.Cercano);

    public bool EsValida =>
        CalifMinima is >= 0m and <= 10m
        && CalifAprobatoria >= CalifMinima && CalifAprobatoria <= 10m
        && decimal.Round(CalifMinima, 2) == CalifMinima
        && decimal.Round(CalifAprobatoria, 2) == CalifAprobatoria
        && DecimalesFinal is >= 0 and <= 2
        && Grupos.Redondeo.Todos.Contains(Redondeo);
}

/// <summary>
/// Hasta 50 alumnos de un docente en un ciclo (RN-010, RN-011). En secundaria hay un grupo por materia.
/// Archivado queda de solo lectura (RN-018).
/// </summary>
public sealed class Grupo : Entidad
{
    public const int LongitudNombre = 40;
    public const int LongitudMateria = 60;
    public const int MaximoAlumnosActivos = 50;

    private Grupo(Guid docenteId, Guid cicloId, string nombre, string nivel, int grado, string? materia, DateTimeOffset ahora)
        : base(ahora)
    {
        DocenteId = docenteId;
        CicloEscolarId = cicloId;
        Nombre = nombre;
        Nivel = nivel;
        Grado = grado;
        Materia = materia;
    }

    private Grupo()
    {
    }

    public Guid DocenteId { get; private set; }

    public Guid CicloEscolarId { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public string Nivel { get; private set; } = NivelEducativo.Primaria;

    public int Grado { get; private set; }

    public string? Materia { get; private set; }

    public string Plan { get; private set; } = PlanGrupo.Gratis;

    public decimal CalifMinima { get; private set; } = ConfiguracionCalificacion.PorDefecto.CalifMinima;

    public decimal CalifAprobatoria { get; private set; } = ConfiguracionCalificacion.PorDefecto.CalifAprobatoria;

    public int DecimalesFinal { get; private set; } = ConfiguracionCalificacion.PorDefecto.DecimalesFinal;

    public string Redondeo { get; private set; } = ConfiguracionCalificacion.PorDefecto.Redondeo;

    public DateTimeOffset? ArchivadoEn { get; private set; }

    public bool EstaArchivado => ArchivadoEn is not null;

    public ConfiguracionCalificacion Configuracion => new(CalifMinima, CalifAprobatoria, DecimalesFinal, Redondeo);

    /// <summary>RF-010. La materia solo aplica en secundaria; en primaria se descarta.</summary>
    public static Resultado<Grupo> Crear(
        CicloEscolar ciclo, string nombre, string nivel, int grado, string? materia, DateTimeOffset ahora)
    {
        if (ValidarNivel(nivel, grado) is { } error)
        {
            return Resultado.Fallo<Grupo>(error);
        }

        return Resultado.Exito(new Grupo(ciclo.DocenteId, ciclo.Id, nombre.Trim(), nivel, grado, MateriaDe(nivel, materia), ahora));
    }

    /// <summary>Un periodo por cada unidad del ciclo (RN-025).</summary>
    public IReadOnlyList<Periodo> CrearPeriodos(IEnumerable<Unidad> unidades, DateTimeOffset ahora) =>
        unidades.Select(u => new Periodo(this, u, ahora)).ToList();

    public Resultado Editar(
        string nombre, int grado, string? materia, ConfiguracionCalificacion configuracion, DateTimeOffset ahora)
    {
        if (EstaArchivado)
        {
            return Resultado.Fallo(ErroresGrupos.GrupoArchivado);
        }

        if (ValidarNivel(Nivel, grado) is { } error)
        {
            return Resultado.Fallo(error);
        }

        if (!configuracion.EsValida)
        {
            return Resultado.Fallo(ErroresGrupos.ConfiguracionCalificacionInvalida);
        }

        Nombre = nombre.Trim();
        Grado = grado;
        Materia = MateriaDe(Nivel, materia);
        CalifMinima = configuracion.CalifMinima;
        CalifAprobatoria = configuracion.CalifAprobatoria;
        DecimalesFinal = configuracion.DecimalesFinal;
        Redondeo = configuracion.Redondeo;
        MarcarActualizada(ahora);
        return Resultado.Exito();
    }

    /// <summary>RF-019. Archivar dos veces no cambia la fecha.</summary>
    public void Archivar(DateTimeOffset ahora)
    {
        if (!EstaArchivado)
        {
            ArchivadoEn = ahora;
            MarcarActualizada(ahora);
        }
    }

    private static ErrorNegocio? ValidarNivel(string nivel, int grado)
    {
        if (!NivelEducativo.Todos.Contains(nivel))
        {
            return ErroresCuentas.NivelInvalido;
        }

        var maximo = nivel == NivelEducativo.Primaria ? 6 : 3;
        return grado < 1 || grado > maximo ? ErroresGrupos.GradoInvalido : null;
    }

    private static string? MateriaDe(string nivel, string? materia) =>
        nivel == NivelEducativo.Secundaria && !string.IsNullOrWhiteSpace(materia) ? materia.Trim() : null;
}

public static class EstadoPeriodo
{
    public const string Abierto = "abierto";
    public const string EnCierre = "en_cierre";
    public const string Cerrado = "cerrado";
}

/// <summary>La unidad vista desde un grupo: ahí viven los rubros, el cierre y las boletas (glosario).</summary>
public sealed class Periodo : Entidad
{
    internal Periodo(Grupo grupo, Unidad unidad, DateTimeOffset ahora)
        : base(ahora)
    {
        GrupoId = grupo.Id;
        UnidadId = unidad.Id;
        DocenteId = grupo.DocenteId;
    }

    private Periodo()
    {
    }

    public Guid GrupoId { get; private set; }

    public Guid UnidadId { get; private set; }

    public Guid DocenteId { get; private set; }

    public string Estado { get; private set; } = EstadoPeriodo.Abierto;
}
