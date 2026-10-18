using AulaLista.Application.Comun;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Grupos;
using FluentValidation;

namespace AulaLista.Application.Grupos;

public sealed record GrupoDto(
    Guid Id,
    string Nombre,
    string Nivel,
    int Grado,
    string? Materia,
    Guid CicloId,
    string CicloNombre,
    string Plan,
    int AlumnosActivos,
    decimal CalifMinima,
    decimal CalifAprobatoria,
    int DecimalesFinal,
    string Redondeo,
    decimal ValorRetardo,
    DateTimeOffset? ArchivadoEn)
{
    /// <summary>SPEC-08: <c>gratis</c>, <c>pro</c> o <c>prueba</c> hoy (no solo lo guardado en <see cref="Plan"/>).</summary>
    public string PlanVigente { get; init; } = Planes.PlanVigente.Gratis;

    /// <summary>RN-081: días que le quedan a la prueba Pro, solo en su grupo.</summary>
    public int? DiasPrueba { get; init; }

    public GrupoDto ConPlan(Planes.PlanGrupoDto plan) => this with { PlanVigente = plan.Plan, DiasPrueba = plan.DiasPrueba };

    public static GrupoDto De(GrupoConConteo g) => new(
        g.Grupo.Id, g.Grupo.Nombre, g.Grupo.Nivel, g.Grupo.Grado, g.Grupo.Materia, g.Grupo.CicloEscolarId, g.CicloNombre,
        g.Grupo.Plan, g.AlumnosActivos, g.Grupo.CalifMinima, g.Grupo.CalifAprobatoria, g.Grupo.DecimalesFinal,
        g.Grupo.Redondeo, g.Grupo.ValorRetardo, g.Grupo.ArchivadoEn);
}

public sealed record PeriodoDto(Guid Id, Guid UnidadId, string Nombre, int Orden, DateOnly Inicio, DateOnly Fin, string Estado);

public sealed class ListarGruposCasoUso(IRepositorioGrupos repositorio, Planes.AccesoPro acceso)
{
    public async Task<IReadOnlyList<GrupoDto>> EjecutarAsync(bool archivados, CancellationToken ct)
    {
        var lista = new List<GrupoDto>();
        foreach (var g in await repositorio.GruposAsync(archivados, ct))
        {
            lista.Add(GrupoDto.De(g).ConPlan(await acceso.PlanAsync(g.Grupo, ct)));
        }

        return lista;
    }
}

public sealed class ObtenerGrupoCasoUso(IRepositorioGrupos repositorio, Planes.AccesoPro acceso)
{
    public async Task<GrupoDto?> EjecutarAsync(Guid id, CancellationToken ct) =>
        (await repositorio.GruposAsync(archivados: true, ct)).FirstOrDefault(g => g.Grupo.Id == id) is { } g
            ? GrupoDto.De(g).ConPlan(await acceso.PlanAsync(g.Grupo, ct))
            : null;
}

public sealed record CrearGrupo(string Nombre, string Nivel, int Grado, string? Materia, Guid CicloId);

internal sealed class CrearGrupoValidador : AbstractValidator<CrearGrupo>
{
    public CrearGrupoValidador()
    {
        RuleFor(c => c.Nombre).NotEmpty().MaximumLength(Grupo.LongitudNombre);
        RuleFor(c => c.Nivel).NotEmpty().MaximumLength(20);
        RuleFor(c => c.Materia).MaximumLength(Grupo.LongitudMateria);
    }
}

/// <summary>RN-021: datos comunes y de 1 a 15 nombres; se crean todos o ninguno.</summary>
public sealed record CrearGruposLote(string Nivel, int Grado, string? Materia, Guid CicloId, IReadOnlyList<string> Nombres);

internal sealed class CrearGruposLoteValidador : AbstractValidator<CrearGruposLote>
{
    public CrearGruposLoteValidador()
    {
        RuleFor(c => c.Nivel).NotEmpty().MaximumLength(20);
        RuleFor(c => c.Materia).MaximumLength(Grupo.LongitudMateria);
        RuleFor(c => c.Nombres).NotNull();
        RuleForEach(c => c.Nombres).NotEmpty().MaximumLength(Grupo.LongitudNombre);
    }
}

/// <summary>RF-010, RF-020: crea uno o varios grupos con un periodo por unidad del ciclo (RN-025).</summary>
public sealed class CrearGruposCasoUso(IRepositorioGrupos repositorio, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public const int MaximoPorLote = 15;

    public Task<Resultado<IReadOnlyList<GrupoDto>>> EjecutarAsync(CrearGrupo comando, CancellationToken ct) =>
        CrearAsync(comando.Nivel, comando.Grado, comando.Materia, comando.CicloId, [comando.Nombre], ct);

    public async Task<Resultado<IReadOnlyList<GrupoDto>>> EjecutarAsync(CrearGruposLote comando, CancellationToken ct)
    {
        var nombres = comando.Nombres.Select(n => n.Trim()).ToList();
        if (nombres.Count is < 1 or > MaximoPorLote
            || nombres.Distinct(StringComparer.OrdinalIgnoreCase).Count() != nombres.Count)
        {
            return Resultado.Fallo<IReadOnlyList<GrupoDto>>(ErroresGrupos.LoteInvalido);
        }

        return await CrearAsync(comando.Nivel, comando.Grado, comando.Materia, comando.CicloId, nombres, ct);
    }

    private async Task<Resultado<IReadOnlyList<GrupoDto>>> CrearAsync(
        string nivel, int grado, string? materia, Guid cicloId, IReadOnlyList<string> nombres, CancellationToken ct)
    {
        if (await repositorio.CicloAsync(cicloId, ct) is not { } ciclo)
        {
            return Resultado.Fallo<IReadOnlyList<GrupoDto>>(ErroresGrupos.CicloInexistente);
        }

        var ahora = reloj.GetUtcNow();
        var existentes = await repositorio.NombresDeGruposAsync(cicloId, null, ct);
        var creados = new List<GrupoDto>();
        foreach (var nombre in nombres)
        {
            var creado = Grupo.Crear(ciclo, nombre, nivel, grado, materia, ahora);
            if (creado.EsFallo)
            {
                return Resultado.Fallo<IReadOnlyList<GrupoDto>>(creado.Error);
            }

            var grupo = creado.Valor;
            if (EsDuplicado(existentes, grupo.Grado, grupo.Nombre, grupo.Materia))
            {
                return Resultado.Fallo<IReadOnlyList<GrupoDto>>(ErroresGrupos.GrupoDuplicado);
            }

            repositorio.Agregar(grupo);
            repositorio.Agregar(grupo.CrearPeriodos(ciclo.Unidades, ahora));
            creados.Add(GrupoDto.De(new GrupoConConteo(grupo, ciclo.Nombre, 0)));
        }

        await unidad.GuardarAsync(ct);
        return Resultado.Exito<IReadOnlyList<GrupoDto>>(creados);
    }

    /// <summary>"4° A" y "1° A" conviven: un grupo se repite solo con el mismo grado, nombre y materia.</summary>
    internal static bool EsDuplicado(
        IEnumerable<(int Grado, string Nombre, string? Materia)> existentes, int grado, string nombre, string? materia) =>
        existentes.Any(e => e.Grado == grado
            && string.Equals(e.Nombre, nombre, StringComparison.OrdinalIgnoreCase)
            && string.Equals(e.Materia ?? string.Empty, materia ?? string.Empty, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Sin <see cref="ValorRetardo"/>, el retardo conserva su valor (RN-102).</summary>
public sealed record EditarGrupo(
    string Nombre, int Grado, string? Materia, decimal CalifMinima, decimal CalifAprobatoria, int DecimalesFinal, string Redondeo,
    decimal? ValorRetardo = null);

internal sealed class EditarGrupoValidador : AbstractValidator<EditarGrupo>
{
    public EditarGrupoValidador()
    {
        RuleFor(c => c.Nombre).NotEmpty().MaximumLength(Grupo.LongitudNombre);
        RuleFor(c => c.Materia).MaximumLength(Grupo.LongitudMateria);
        RuleFor(c => c.Redondeo).NotEmpty().MaximumLength(10);
    }
}

/// <summary>Edita datos y configuración de calificación del grupo (RN-030, RN-035, RN-102). Archivado no se edita (RN-018).</summary>
public sealed class EditarGrupoCasoUso(IRepositorioGrupos repositorio, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado?> EjecutarAsync(Guid id, EditarGrupo comando, CancellationToken ct)
    {
        if (await repositorio.GrupoAsync(id, ct) is not { } grupo)
        {
            return null;
        }

        var existentes = await repositorio.NombresDeGruposAsync(grupo.CicloEscolarId, id, ct);
        var materia = grupo.Nivel == Domain.Cuentas.NivelEducativo.Secundaria ? comando.Materia?.Trim() : null;
        if (CrearGruposCasoUso.EsDuplicado(existentes, comando.Grado, comando.Nombre.Trim(), string.IsNullOrEmpty(materia) ? null : materia))
        {
            return Resultado.Fallo(ErroresGrupos.GrupoDuplicado);
        }

        var configuracion = new ConfiguracionCalificacion(comando.CalifMinima, comando.CalifAprobatoria, comando.DecimalesFinal, comando.Redondeo);
        var resultado = grupo.Editar(comando.Nombre, comando.Grado, comando.Materia, configuracion, reloj.GetUtcNow(), comando.ValorRetardo);
        if (resultado.EsExito)
        {
            await unidad.GuardarAsync(ct);
        }

        return resultado;
    }
}

/// <summary>RF-019: archiva el grupo; queda de solo lectura.</summary>
public sealed class ArchivarGrupoCasoUso(IRepositorioGrupos repositorio, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<bool> EjecutarAsync(Guid id, CancellationToken ct)
    {
        if (await repositorio.GrupoAsync(id, ct) is not { } grupo)
        {
            return false;
        }

        grupo.Archivar(reloj.GetUtcNow());
        await unidad.GuardarAsync(ct);
        return true;
    }
}

/// <summary>Borra el grupo con sus periodos y alumnos. SPEC-02 solo lo permite sin calificaciones.</summary>
public sealed class BorrarGrupoCasoUso(IRepositorioGrupos repositorio, IDatosDeEvaluacion evaluacion, IUnidadDeTrabajo unidad)
{
    public async Task<Resultado?> EjecutarAsync(Guid id, CancellationToken ct)
    {
        if (await repositorio.GrupoAsync(id, ct) is not { } grupo)
        {
            return null;
        }

        if (await evaluacion.GrupoTieneCalificacionesAsync(id, ct))
        {
            return Resultado.Fallo(Domain.Evaluacion.ErroresEvaluacion.GrupoConCalificaciones);
        }

        repositorio.Borrar(grupo);
        await unidad.GuardarAsync(ct);
        return Resultado.Exito();
    }
}

/// <summary>RF-011: una entrada por unidad del ciclo, con el estado propio del grupo.</summary>
public sealed class PeriodosDeGrupoCasoUso(IRepositorioGrupos repositorio)
{
    public async Task<IReadOnlyList<PeriodoDto>?> EjecutarAsync(Guid grupoId, CancellationToken ct)
    {
        if (await repositorio.GrupoAsync(grupoId, ct) is null)
        {
            return null;
        }

        return (await repositorio.PeriodosAsync(grupoId, ct))
            .OrderBy(p => p.Unidad.Orden)
            .Select(p => new PeriodoDto(p.Periodo.Id, p.Unidad.Id, p.Unidad.Nombre, p.Unidad.Orden, p.Unidad.Inicio, p.Unidad.Fin, p.Periodo.Estado))
            .ToList();
    }
}
