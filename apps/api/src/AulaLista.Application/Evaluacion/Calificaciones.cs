using AulaLista.Application.Comun;
using AulaLista.Application.Grupos;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Evaluacion;
using AulaLista.Domain.Grupos;
using FluentValidation;

namespace AulaLista.Application.Evaluacion;

public sealed record CriterioCeldaDto(Guid CriterioId, decimal? Valor, string Marca);

/// <summary>Una celda guardada. <see cref="Version"/> es la vigente en el servidor.</summary>
public sealed record CeldaDto(Guid ActividadId, Guid AlumnoId, decimal? Valor, string Marca, string Origen, int Version, IReadOnlyList<CriterioCeldaDto> Criterios)
{
    public static CeldaDto De(Calificacion c) => new(
        c.ActividadId, c.AlumnoId, c.Valor, c.Marca, c.Origen, c.Version,
        c.Criterios.Select(d => new CriterioCeldaDto(d.CriterioId, d.Valor, d.Marca)).ToList());
}

public sealed record AlumnoMatrizDto(Guid Id, int NumeroLista, string Nombres, string Apellidos, bool DadoDeBaja);

public sealed record PromedioRubroDto(Guid RubroId, decimal? Promedio);

/// <summary>RN-033 a RN-035: promedios con 2 decimales, el del periodo y el final con la configuración del grupo.</summary>
public sealed record ResultadoAlumnoDto(
    Guid AlumnoId, IReadOnlyList<PromedioRubroDto> Rubros, decimal? Periodo, decimal? Final, IReadOnlyList<Guid> RubrosSinDatos);

public sealed record RubroMatrizDto(RubroDto Rubro, IReadOnlyList<ActividadDto> Actividades);

public sealed record ConfiguracionMatrizDto(decimal CalifMinima, decimal CalifAprobatoria, int DecimalesFinal, string Redondeo);

/// <summary>Todo lo que la captura y la tabla necesitan del periodo, también para guardarlo sin conexión.</summary>
public sealed record MatrizCalificacionesDto(
    Guid PeriodoId,
    Guid GrupoId,
    string Estado,
    bool SoloLectura,
    ConfiguracionMatrizDto Configuracion,
    decimal SumaPesos,
    IReadOnlyList<RubroMatrizDto> Rubros,
    IReadOnlyList<AlumnoMatrizDto> Alumnos,
    IReadOnlyList<CeldaDto> Celdas,
    IReadOnlyList<ResultadoAlumnoDto> Resultados);

/// <summary>RF-033, RF-034: matriz del periodo. Alumnos activos y, de solo lectura, las bajas con calificaciones.</summary>
public sealed class MatrizCalificacionesCasoUso(IRepositorioGrupos grupos, IRepositorioEvaluacion repositorio, ConsultaRubros consulta)
{
    public async Task<MatrizCalificacionesDto?> EjecutarAsync(Guid periodoId, CancellationToken ct)
    {
        if (await grupos.PeriodoAsync(periodoId, ct) is not { } periodo)
        {
            return null;
        }

        var rubros = await consulta.LeerAsync(periodo, ct);
        var actividades = await repositorio.ActividadesAsync(rubros.Rubros.Select(r => r.Id).ToList(), ct);
        var celdas = (await repositorio.CalificacionesAsync(actividades.Select(a => a.Id).ToList(), ct)).Where(c => !c.EstaVacia).ToList();
        var conCeldas = celdas.Select(c => c.AlumnoId).ToHashSet();
        var alumnos = (await grupos.AlumnosAsync(periodo.Grupo.Id, ct))
            .Where(a => a.EstaActivo || conCeldas.Contains(a.Id))
            .OrderBy(a => a.NumeroLista).ThenBy(a => a.Apellidos)
            .ToList();
        celdas = celdas.Where(c => alumnos.Any(a => a.Id == c.AlumnoId)).ToList();

        var capturadas = celdas.CountBy(c => c.ActividadId).ToDictionary();
        var porRubro = actividades.ToLookup(a => a.RubroId);
        var grupo = periodo.Grupo;
        return new MatrizCalificacionesDto(
            periodo.Periodo.Id,
            grupo.Id,
            periodo.Periodo.Estado,
            ReglasPeriodo.PuedeCapturar(periodo) is not null,
            new ConfiguracionMatrizDto(grupo.CalifMinima, grupo.CalifAprobatoria, grupo.DecimalesFinal, grupo.Redondeo),
            rubros.SumaPesos,
            rubros.Rubros.Select(r => new RubroMatrizDto(
                r, porRubro[r.Id].OrderBy(a => a.Orden).Select(a => ActividadDto.De(a, capturadas.GetValueOrDefault(a.Id))).ToList())).ToList(),
            alumnos.Select(a => new AlumnoMatrizDto(a.Id, a.NumeroLista, a.Nombres, a.Apellidos, !a.EstaActivo)).ToList(),
            celdas.Select(CeldaDto.De).ToList(),
            Calcular(rubros.Rubros, porRubro, celdas, alumnos, ReglasPeriodo.Configuracion(grupo)));
    }

    private static List<ResultadoAlumnoDto> Calcular(
        IReadOnlyList<RubroDto> rubros, ILookup<Guid, Actividad> porRubro, IReadOnlyList<Calificacion> celdas,
        IReadOnlyList<Alumno> alumnos, ConfiguracionCalculo configuracion)
    {
        var porCelda = celdas.ToDictionary(c => (c.ActividadId, c.AlumnoId));
        return alumnos.Select(alumno =>
        {
            var entrada = rubros.Select(r => new RubroCalculo(
                r.Id, r.Peso, r.Comportamiento, r.Forma, r.CalifMinima,
                r.Criterios.Select(c => new CriterioCalculo(c.Id, c.Peso, c.Forma, c.CalifMinima)).ToList(),
                porRubro[r.Id].Select(a => porCelda.TryGetValue((a.Id, alumno.Id), out var c) ? c.ACalculo() : CeldaCalculo.Vacia).ToList()))
                .ToList();
            var resultado = Calculo.Periodo(entrada, configuracion);
            return new ResultadoAlumnoDto(
                alumno.Id,
                resultado.Rubros.Select(r => new PromedioRubroDto(r.RubroId, r.Promedio is { } p ? Calculo.Redondear(p, 2, Redondeo.Cercano) : null)).ToList(),
                resultado.Periodo,
                resultado.Final,
                resultado.RubrosSinDatos);
        }).ToList();
    }
}

public sealed record CriterioCambio(Guid CriterioId, decimal? Valor, string Marca);

/// <summary>Lo capturado en una celda. <see cref="Id"/> es el UUID de la cola sin conexión (RF-037).</summary>
public sealed record CambioCelda(
    Guid? Id, Guid ActividadId, Guid AlumnoId, decimal? Valor, string Marca, IReadOnlyList<CriterioCambio>? Criterios, int? Version);

public sealed record GuardarCalificaciones(IReadOnlyList<CambioCelda> Celdas);

/// <summary>RF-037: la cola sin conexión, en lotes de hasta 100 cambios con su UUID.</summary>
public sealed record SincronizarCambios(IReadOnlyList<CambioCelda> Cambios);

/// <summary>Una celda que no se guardó, con su motivo y la versión vigente para que el cliente la reponga.</summary>
public sealed record CeldaRechazada(Guid? Id, Guid ActividadId, Guid AlumnoId, string Codigo, string Mensaje, CeldaDto? Vigente);

/// <summary>Aplicadas y rechazadas por celda: una celda mala no detiene la cola.</summary>
public sealed record ResultadoCalificaciones(IReadOnlyList<CeldaDto> Aplicadas, IReadOnlyList<CeldaRechazada> Rechazadas);

internal static class ReglasLote
{
    public const int Maximo = 100;

    public static void Validar(AbstractValidator<CambioCelda> v)
    {
        v.RuleFor(c => c.ActividadId).NotEmpty();
        v.RuleFor(c => c.AlumnoId).NotEmpty();
        v.RuleFor(c => c.Marca).NotNull().MaximumLength(20);
        v.RuleFor(c => c.Criterios).Must(c => c is null || c.Count <= Rubro.MaximoCriterios);
        v.RuleForEach(c => c.Criterios).ChildRules(c => c.RuleFor(x => x.Marca).NotNull().MaximumLength(20));
    }
}

internal sealed class CambioCeldaValidador : AbstractValidator<CambioCelda>
{
    public CambioCeldaValidador() => ReglasLote.Validar(this);
}

internal sealed class GuardarCalificacionesValidador : AbstractValidator<GuardarCalificaciones>
{
    public GuardarCalificacionesValidador()
    {
        RuleFor(c => c.Celdas).NotEmpty().Must(c => c.Count <= ReglasLote.Maximo);
        RuleForEach(c => c.Celdas).SetValidator(new CambioCeldaValidador());
    }
}

internal sealed class SincronizarCambiosValidador : AbstractValidator<SincronizarCambios>
{
    public SincronizarCambiosValidador()
    {
        RuleFor(c => c.Cambios).NotEmpty().Must(c => c.Count <= ReglasLote.Maximo);
        RuleForEach(c => c.Cambios).SetValidator(new CambioCeldaValidador());
        RuleForEach(c => c.Cambios).Must(c => c.Id is { } id && id != Guid.Empty).WithMessage("Cada cambio lleva su UUID.");
        RuleFor(c => c.Cambios).Must(c => c.Select(x => x.Id).Distinct().Count() == c.Count).WithMessage("Los UUID no se repiten.");
    }
}

/// <summary>
/// RF-033, RF-037: guarda celdas en lote. La última escritura gana (02-arquitectura) y cada celda se valida sola
/// (RN-030, RN-118, RN-119). Un cambio cuyo UUID ya se aplicó no se aplica otra vez: responde la celda vigente.
/// </summary>
public sealed class GuardarCalificacionesCasoUso(
    IRepositorioGrupos grupos, IRepositorioEvaluacion repositorio, IContextoUsuario usuario, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    /// <summary>PUT /calificaciones: el lote entero usa la <c>Idempotency-Key</c> como su UUID.</summary>
    public async Task<ResultadoCalificaciones> EjecutarAsync(GuardarCalificaciones comando, Guid? claveIdempotencia, CancellationToken ct)
    {
        if (claveIdempotencia is { } clave && (await repositorio.CambiosAplicadosAsync([clave], ct)).Count > 0)
        {
            return await VigentesAsync(comando.Celdas, ct);
        }

        var resultado = await AplicarAsync(comando.Celdas, ct);
        if (claveIdempotencia is { } nueva)
        {
            repositorio.Agregar(CambioSincronizado.Registrar(nueva, Docente, reloj.GetUtcNow()));
        }

        await unidad.GuardarAsync(ct);
        return resultado;
    }

    /// <summary>POST /sincronizacion: cada cambio trae su UUID y se descarta si ya llegó.</summary>
    public async Task<ResultadoCalificaciones> EjecutarAsync(SincronizarCambios comando, CancellationToken ct)
    {
        var aplicados = await repositorio.CambiosAplicadosAsync(comando.Cambios.Select(c => c.Id!.Value).ToList(), ct);
        var repetidos = comando.Cambios.Where(c => aplicados.Contains(c.Id!.Value)).ToList();
        var nuevos = comando.Cambios.Where(c => !aplicados.Contains(c.Id!.Value)).ToList();

        var resultado = await AplicarAsync(nuevos, ct);
        var ahora = reloj.GetUtcNow();
        foreach (var cambio in nuevos.Where(c => resultado.Rechazadas.All(r => r.Id != c.Id)))
        {
            repositorio.Agregar(CambioSincronizado.Registrar(cambio.Id!.Value, Docente, ahora));
        }

        await unidad.GuardarAsync(ct);
        if (repetidos.Count == 0)
        {
            return resultado;
        }

        var vigentes = await VigentesAsync(repetidos, ct);
        return new ResultadoCalificaciones([.. vigentes.Aplicadas, .. resultado.Aplicadas], [.. vigentes.Rechazadas, .. resultado.Rechazadas]);
    }

    private readonly Dictionary<Guid, (Rubro Rubro, PeriodoCompleto Periodo, IReadOnlyList<Alumno> Alumnos)?> _rubros = [];
    private readonly Dictionary<Guid, Actividad?> _actividades = [];
    private readonly Dictionary<(Guid, Guid), Calificacion> _celdas = [];

    private async Task<ResultadoCalificaciones> AplicarAsync(IReadOnlyList<CambioCelda> cambios, CancellationToken ct)
    {
        await CargarAsync(cambios, ct);
        var ahora = reloj.GetUtcNow();
        var aplicadas = new Dictionary<(Guid, Guid), Calificacion>();
        var rechazadas = new List<CeldaRechazada>();
        foreach (var cambio in cambios)
        {
            var clave = (cambio.ActividadId, cambio.AlumnoId);
            if (Revisar(cambio) is { } error)
            {
                rechazadas.Add(Rechazo(cambio, error));
                continue;
            }

            var actividad = _actividades[cambio.ActividadId]!;
            var rubro = _rubros[actividad.RubroId]!.Value.Rubro;
            var existente = _celdas.GetValueOrDefault(clave);
            var celda = existente ?? Calificacion.Nueva(actividad, cambio.AlumnoId, ahora);

            var reglas = new ReglasCelda(rubro.Forma, rubro.CalifMinima,
                rubro.Criterios.Select(c => new CriterioCalculo(c.Id, c.Peso, c.Forma, c.CalifMinima)).ToList());
            var criterios = (cambio.Criterios ?? []).Select(c => new CeldaCriterio(c.CriterioId, c.Valor, c.Marca)).ToList();
            var aplicado = celda.Aplicar(reglas, cambio.Valor, cambio.Marca, criterios, ahora);
            if (aplicado.EsFallo)
            {
                rechazadas.Add(Rechazo(cambio, aplicado.Error));
                continue;
            }

            if (existente is null)
            {
                repositorio.Agregar(celda); // una celda nueva se guarda solo si pasó sus reglas
                _celdas[clave] = celda;
            }

            aplicadas[clave] = celda;
        }

        return new ResultadoCalificaciones(aplicadas.Values.Select(CeldaDto.De).ToList(), rechazadas);
    }

    private ErrorNegocio? Revisar(CambioCelda cambio)
    {
        if (_actividades.GetValueOrDefault(cambio.ActividadId) is not { } actividad
            || _rubros.GetValueOrDefault(actividad.RubroId) is not { } contexto
            || contexto.Alumnos.FirstOrDefault(a => a.Id == cambio.AlumnoId) is not { } alumno)
        {
            return ErroresEvaluacion.CeldaInexistente;
        }

        if (ReglasPeriodo.PuedeCapturar(contexto.Periodo) is { } bloqueo)
        {
            return bloqueo;
        }

        return alumno.EstaActivo ? null : ErroresGrupos.AlumnoDadoDeBaja;
    }

    private CeldaRechazada Rechazo(CambioCelda cambio, ErrorNegocio error) => new(
        cambio.Id, cambio.ActividadId, cambio.AlumnoId, error.Codigo, error.Mensaje,
        _celdas.TryGetValue((cambio.ActividadId, cambio.AlumnoId), out var vigente) ? CeldaDto.De(vigente) : null);

    private async Task CargarAsync(IReadOnlyList<CambioCelda> cambios, CancellationToken ct)
    {
        var ids = cambios.Select(c => c.ActividadId).Distinct().Where(id => !_actividades.ContainsKey(id)).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var actividades = await repositorio.ActividadesPorIdAsync(ids, ct);
        foreach (var id in ids)
        {
            _actividades[id] = actividades.FirstOrDefault(a => a.Id == id);
        }

        var rubroIds = actividades.Select(a => a.RubroId).Distinct().Where(id => !_rubros.ContainsKey(id)).ToList();
        var alumnosPorGrupo = new Dictionary<Guid, IReadOnlyList<Alumno>>();
        foreach (var rubro in await repositorio.RubrosPorIdAsync(rubroIds, ct))
        {
            if (await grupos.PeriodoAsync(rubro.PeriodoId, ct) is not { } periodo)
            {
                continue;
            }

            if (!alumnosPorGrupo.TryGetValue(periodo.Grupo.Id, out var alumnos))
            {
                alumnos = await grupos.AlumnosAsync(periodo.Grupo.Id, ct);
                alumnosPorGrupo[periodo.Grupo.Id] = alumnos;
            }

            _rubros[rubro.Id] = (rubro, periodo, alumnos);
        }

        foreach (var celda in await repositorio.CalificacionesAsync(actividades.Select(a => a.Id).ToList(), ct))
        {
            _celdas.TryAdd((celda.ActividadId, celda.AlumnoId), celda);
        }
    }

    private async Task<ResultadoCalificaciones> VigentesAsync(IReadOnlyList<CambioCelda> cambios, CancellationToken ct)
    {
        await CargarAsync(cambios, ct);
        var aplicadas = cambios
            .Select(c => _celdas.GetValueOrDefault((c.ActividadId, c.AlumnoId)))
            .OfType<Calificacion>()
            .DistinctBy(c => (c.ActividadId, c.AlumnoId))
            .Select(CeldaDto.De)
            .ToList();
        return new ResultadoCalificaciones(aplicadas, []);
    }

    private Guid Docente => usuario.DocenteId ?? throw new InvalidOperationException("La captura requiere un docente autenticado.");
}
