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

/// <summary>Cuánto vale un retardo para el promedio (RN-102): 1, 0.5 o 0; por defecto 1.</summary>
public static class ValorRetardo
{
    public const decimal PorDefecto = 1m;

    public static readonly IReadOnlyList<decimal> Todos = [1m, 0.5m, 0m];

    public static bool EsValido(decimal valor) => Todos.Contains(valor);
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

    /// <summary>
    /// Pro termina en esta fecha aunque <see cref="Plan"/> diga Pro: un grupo quitado espera al siguiente corte
    /// (RN-083), la gracia (RN-085) o el fin del prepago más su gracia (RN-089).
    /// </summary>
    public DateTimeOffset? ProHasta { get; private set; }

    public decimal CalifMinima { get; private set; } = ConfiguracionCalificacion.PorDefecto.CalifMinima;

    public decimal CalifAprobatoria { get; private set; } = ConfiguracionCalificacion.PorDefecto.CalifAprobatoria;

    public int DecimalesFinal { get; private set; } = ConfiguracionCalificacion.PorDefecto.DecimalesFinal;

    public string Redondeo { get; private set; } = ConfiguracionCalificacion.PorDefecto.Redondeo;

    /// <summary>Lo que vale un retardo en la asistencia (RN-102).</summary>
    public decimal ValorRetardo { get; private set; } = Grupos.ValorRetardo.PorDefecto;

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

    /// <summary>Sin <paramref name="valorRetardo"/>, el retardo conserva su valor.</summary>
    public Resultado Editar(
        string nombre, int grado, string? materia, ConfiguracionCalificacion configuracion, DateTimeOffset ahora,
        decimal? valorRetardo = null)
    {
        if (EstaArchivado)
        {
            return Resultado.Fallo(ErroresGrupos.GrupoArchivado);
        }

        if (ValidarNivel(Nivel, grado) is { } error)
        {
            return Resultado.Fallo(error);
        }

        if (!configuracion.EsValida || valorRetardo is { } v && !Grupos.ValorRetardo.EsValido(v))
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
        ValorRetardo = valorRetardo ?? ValorRetardo;
        MarcarActualizada(ahora);
        return Resultado.Exito();
    }

    /// <summary>SPEC-08: Pro pagado ahora mismo (sin contar la prueba).</summary>
    public bool TieneProPagado(DateTimeOffset ahora) => Plan == PlanGrupo.Pro && (ProHasta is null || ahora < ProHasta);

    /// <summary>Pro sin fecha de fin, o hasta <paramref name="hasta"/>.</summary>
    public void ActivarPro(DateTimeOffset? hasta, DateTimeOffset ahora)
    {
        Plan = PlanGrupo.Pro;
        ProHasta = hasta;
        MarcarActualizada(ahora);
    }

    /// <summary>Sigue en Pro hasta esa fecha; si ya terminaba antes, no se alarga.</summary>
    public void TerminarProEn(DateTimeOffset hasta, DateTimeOffset ahora)
    {
        if (Plan == PlanGrupo.Pro && (ProHasta is null || hasta < ProHasta))
        {
            ProHasta = hasta;
            MarcarActualizada(ahora);
        }
    }

    /// <summary>RN-085: pasa a Gratis sin borrar nada; lo publicado sigue visible para los tutores.</summary>
    public void PasarAGratis(DateTimeOffset ahora)
    {
        if (Plan != PlanGrupo.Gratis || ProHasta is not null)
        {
            Plan = PlanGrupo.Gratis;
            ProHasta = null;
            MarcarActualizada(ahora);
        }
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

    /// <summary>La primera vez que entró en cierre; mide el tiempo de cierre del piloto (SPEC-09). Reabrir no lo cambia.</summary>
    public DateTimeOffset? CierreIniciadoEn { get; private set; }

    /// <summary>RN-040 y RF-040: los pesos quedan fijos; la captura sigue (RN-037). Los requisitos los revisa el caso de uso.</summary>
    public void IniciarCierre(DateTimeOffset ahora)
    {
        if (Estado == EstadoPeriodo.Abierto)
        {
            Estado = EstadoPeriodo.EnCierre;
            CierreIniciadoEn ??= ahora;
            MarcarActualizada(ahora);
        }
    }

    /// <summary>RN-048: de solo lectura, al publicar todas las boletas o cuando el docente lo decide.</summary>
    public void Cerrar(DateTimeOffset ahora)
    {
        if (Estado == EstadoPeriodo.EnCierre)
        {
            Estado = EstadoPeriodo.Cerrado;
            MarcarActualizada(ahora);
        }
    }

    /// <summary>RN-038: vuelve a cierre para corregir; quien lo llama lo confirma y lo audita.</summary>
    public void Reabrir(DateTimeOffset ahora)
    {
        if (Estado == EstadoPeriodo.Cerrado)
        {
            Estado = EstadoPeriodo.EnCierre;
            MarcarActualizada(ahora);
        }
    }
}
