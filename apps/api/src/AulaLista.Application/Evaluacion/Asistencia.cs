using AulaLista.Application.Comun;
using AulaLista.Application.Grupos;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Evaluacion;
using AulaLista.Domain.Grupos;
using FluentValidation;

namespace AulaLista.Application.Evaluacion;

public sealed record EstadoAlumnoDto(Guid AlumnoId, string Estado, int Version);

/// <summary>Una toma con el estado de cada alumno que cuenta en ella (RN-101, RN-105).</summary>
public sealed record TomaAsistenciaDto(DateOnly Fecha, IReadOnlyList<EstadoAlumnoDto> Alumnos)
{
    public static TomaAsistenciaDto De(TomaAsistencia t) =>
        new(t.Fecha, t.Alumnos.Select(a => new EstadoAlumnoDto(a.AlumnoId, a.Estado, a.Version)).ToList());
}

/// <summary>
/// La toma como quedó. <see cref="Omitidos"/> son los alumnos del lote que no se guardaron porque ya no son del
/// grupo o se dieron de baja antes de esa toma.
/// </summary>
public sealed record ResultadoAsistencia(TomaAsistenciaDto Toma, IReadOnlyList<Guid> Omitidos);

public sealed record EstadoAlumno(Guid AlumnoId, string Estado);

/// <summary>Estados a guardar en la toma de una fecha; los que no vienen se quedan como están.</summary>
public sealed record GuardarAsistencia(IReadOnlyList<EstadoAlumno> Alumnos);

internal sealed class GuardarAsistenciaValidador : AbstractValidator<GuardarAsistencia>
{
    public GuardarAsistenciaValidador()
    {
        RuleFor(c => c.Alumnos).NotNull().Must(a => a.Count <= Grupo.MaximoAlumnosActivos * 2);
        RuleForEach(c => c.Alumnos).ChildRules(a =>
        {
            a.RuleFor(x => x.AlumnoId).NotEmpty();
            a.RuleFor(x => x.Estado).NotNull().MaximumLength(20);
        });
        RuleFor(c => c.Alumnos).Must(a => a.Select(x => x.AlumnoId).Distinct().Count() == a.Count)
            .WithMessage("Cada alumno va una sola vez.");
    }
}

/// <summary>RF-101: tomas del grupo en un rango de fechas; sin rango, todas.</summary>
public sealed class ListarAsistenciasCasoUso(IRepositorioGrupos grupos, IRepositorioEvaluacion repositorio)
{
    public async Task<IReadOnlyList<TomaAsistenciaDto>?> EjecutarAsync(Guid grupoId, DateOnly? desde, DateOnly? hasta, CancellationToken ct)
    {
        if (await grupos.GrupoAsync(grupoId, ct) is null)
        {
            return null;
        }

        var tomas = await repositorio.TomasAsync(grupoId, desde ?? DateOnly.MinValue, hasta ?? DateOnly.MaxValue, ct);
        return tomas.Select(TomaAsistenciaDto.De).ToList();
    }
}

/// <summary>
/// RF-100, RF-103: crea la toma de la fecha con todos presentes (RN-101) y aplica el lote encima. Por alumno gana la
/// última escritura; con <c>Idempotency-Key</c> repetida responde la toma vigente sin volver a aplicar.
/// </summary>
public sealed class GuardarAsistenciaCasoUso(
    IRepositorioGrupos grupos, IRepositorioEvaluacion repositorio, IContextoUsuario usuario, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<ResultadoAsistencia>?> EjecutarAsync(
        Guid grupoId, DateOnly fecha, GuardarAsistencia comando, Guid? claveIdempotencia, CancellationToken ct)
    {
        if (await grupos.GrupoAsync(grupoId, ct) is not { } grupo)
        {
            return null;
        }

        var toma = await repositorio.TomaAsync(grupoId, fecha, ct);
        if (claveIdempotencia is { } clave && (await repositorio.CambiosAplicadosAsync([clave], ct)).Count > 0)
        {
            return toma is null ? null : Resultado.Exito(new ResultadoAsistencia(TomaAsistenciaDto.De(toma), []));
        }

        if (await ReglasAsistencia.PeriodoDeAsync(grupos, grupo, fecha, ct) is not { } periodo)
        {
            return Resultado.Fallo<ResultadoAsistencia>(ErroresEvaluacion.FechaFueraDeUnidad); // RN-100
        }

        if (ReglasPeriodo.PuedeCapturar(periodo) is { } bloqueo)
        {
            return Resultado.Fallo<ResultadoAsistencia>(bloqueo); // RN-106
        }

        if (comando.Alumnos.Any(a => !EstadoAsistencia.Todos.Contains(a.Estado)))
        {
            return Resultado.Fallo<ResultadoAsistencia>(ErroresEvaluacion.AsistenciaInvalida);
        }

        var ahora = reloj.GetUtcNow();
        var alumnos = await grupos.AlumnosAsync(grupoId, ct);
        if (toma is null)
        {
            toma = TomaAsistencia.Crear(grupo, fecha, alumnos, ahora);
            repositorio.Agregar(toma);
        }

        var omitidos = new List<Guid>();
        foreach (var cambio in comando.Alumnos)
        {
            if (alumnos.FirstOrDefault(a => a.Id == cambio.AlumnoId) is not { } alumno || toma.Marcar(alumno, cambio.Estado, ahora) is not null)
            {
                omitidos.Add(cambio.AlumnoId);
            }
        }

        if (claveIdempotencia is { } nueva)
        {
            repositorio.Agregar(CambioSincronizado.Registrar(nueva, Docente, ahora));
        }

        await unidad.GuardarAsync(ct);
        return Resultado.Exito(new ResultadoAsistencia(TomaAsistenciaDto.De(toma), omitidos));
    }

    private Guid Docente => usuario.DocenteId ?? throw new InvalidOperationException("Pasar lista requiere un docente autenticado.");
}

/// <summary>Borra la toma de una fecha; en una unidad cerrada no se puede (RN-106).</summary>
public sealed class BorrarAsistenciaCasoUso(IRepositorioGrupos grupos, IRepositorioEvaluacion repositorio, IUnidadDeTrabajo unidad)
{
    public async Task<Resultado?> EjecutarAsync(Guid grupoId, DateOnly fecha, CancellationToken ct)
    {
        if (await grupos.GrupoAsync(grupoId, ct) is not { } grupo || await repositorio.TomaAsync(grupoId, fecha, ct) is not { } toma)
        {
            return null;
        }

        if (grupo.EstaArchivado)
        {
            return Resultado.Fallo(ErroresGrupos.GrupoArchivado);
        }

        if (await ReglasAsistencia.PeriodoDeAsync(grupos, grupo, fecha, ct) is { } periodo && ReglasPeriodo.PuedeCapturar(periodo) is { } bloqueo)
        {
            return Resultado.Fallo(bloqueo);
        }

        repositorio.Borrar(toma);
        await unidad.GuardarAsync(ct);
        return Resultado.Exito();
    }
}

internal static class ReglasAsistencia
{
    /// <summary>El periodo del grupo cuya unidad contiene la fecha (RN-023), o nulo si ninguna la contiene.</summary>
    public static async Task<PeriodoCompleto?> PeriodoDeAsync(IRepositorioGrupos grupos, Grupo grupo, DateOnly fecha, CancellationToken ct) =>
        (await grupos.PeriodosAsync(grupo.Id, ct)).FirstOrDefault(p => p.Unidad.Inicio <= fecha && fecha <= p.Unidad.Fin) is { } p
            ? new PeriodoCompleto(p.Periodo, p.Unidad, grupo)
            : null;

    /// <summary>RN-103: el estado de cada alumno en cada toma de la unidad, en orden de fecha.</summary>
    public static IReadOnlyDictionary<Guid, IReadOnlyList<string>> PorAlumno(IEnumerable<TomaAsistencia> tomas) =>
        tomas.OrderBy(t => t.Fecha)
            .SelectMany(t => t.Alumnos)
            .GroupBy(a => a.AlumnoId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(a => a.Estado).ToList());
}
