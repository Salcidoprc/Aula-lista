using AulaLista.Application.Comun;
using AulaLista.Application.Grupos;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Evaluacion;
using FluentValidation;

namespace AulaLista.Application.Evaluacion;

public sealed record TipoRubroDto(
    Guid Id, string Nombre, string Descripcion, string Icono, string Comportamiento, string FormaSugerida, IReadOnlyList<string> NombresSugeridos)
{
    public static TipoRubroDto De(TipoRubro t) => new(t.Id, t.Nombre, t.Descripcion, t.Icono, t.Comportamiento, t.FormaSugerida, t.NombresSugeridos);
}

public sealed record CriterioDto(Guid Id, string Nombre, decimal Peso, string Forma, decimal CalifMinima, bool TieneCalificaciones);

public sealed record RubroDto(
    Guid Id,
    string Nombre,
    Guid TipoRubroId,
    string Comportamiento,
    decimal Peso,
    string Forma,
    decimal CalifMinima,
    int Orden,
    bool TieneCalificaciones,
    IReadOnlyList<CriterioDto> Criterios);

/// <summary>
/// Los rubros de un periodo con la suma de sus pesos (siempre visible, RN-031). <see cref="TiposInactivos"/> son los tipos
/// desactivados que aún usan estos rubros (RN-094): el catálogo ya no los ofrece, pero el rubro los conserva.
/// </summary>
public sealed record RubrosDelPeriodoDto(
    Guid PeriodoId, string Estado, decimal SumaPesos, IReadOnlyList<RubroDto> Rubros, IReadOnlyList<TipoRubroDto> TiposInactivos);

public sealed class ListarTiposRubroCasoUso(IRepositorioEvaluacion repositorio)
{
    public async Task<IReadOnlyList<TipoRubroDto>> EjecutarAsync(CancellationToken ct) =>
        (await repositorio.TiposRubroAsync(ct)).Where(t => t.Activo).OrderBy(t => t.Orden).Select(TipoRubroDto.De).ToList();
}

/// <summary>Arma la respuesta de rubros del periodo; la comparten leer, reemplazar y copiar.</summary>
public sealed class ConsultaRubros(IRepositorioEvaluacion repositorio)
{
    public async Task<RubrosDelPeriodoDto> LeerAsync(PeriodoCompleto periodo, CancellationToken ct)
    {
        var rubros = await repositorio.RubrosAsync(periodo.Periodo.Id, ct);
        var tipos = (await repositorio.TiposRubroAsync(ct)).ToDictionary(t => t.Id);
        var rubrosConDatos = await repositorio.RubrosConCalificacionesAsync(periodo.Periodo.Id, ct);
        var criteriosConDatos = await repositorio.CriteriosConCalificacionesAsync(periodo.Periodo.Id, ct);
        return new RubrosDelPeriodoDto(
            periodo.Periodo.Id,
            periodo.Periodo.Estado,
            rubros.Sum(r => r.Peso),
            rubros.OrderBy(r => r.Orden).Select(r => new RubroDto(
                r.Id, r.Nombre, r.TipoRubroId, tipos.TryGetValue(r.TipoRubroId, out var t) ? t.Comportamiento : Comportamiento.Actividades,
                r.Peso, r.Forma, r.CalifMinima, r.Orden, rubrosConDatos.Contains(r.Id),
                r.Criterios.OrderBy(c => c.Orden).Select(c => new CriterioDto(c.Id, c.Nombre, c.Peso, c.Forma, c.CalifMinima, criteriosConDatos.Contains(c.Id))).ToList()))
                .ToList(),
            rubros.Select(r => r.TipoRubroId).Distinct()
                .Select(id => tipos.GetValueOrDefault(id)).OfType<TipoRubro>().Where(t => !t.Activo)
                .OrderBy(t => t.Orden).Select(TipoRubroDto.De).ToList());
    }
}

public sealed class ObtenerRubrosCasoUso(IRepositorioGrupos grupos, ConsultaRubros consulta)
{
    public async Task<RubrosDelPeriodoDto?> EjecutarAsync(Guid periodoId, CancellationToken ct) =>
        await grupos.PeriodoAsync(periodoId, ct) is { } periodo ? await consulta.LeerAsync(periodo, ct) : null;
}

public sealed record CriterioEntrada(Guid? Id, string Nombre, decimal Peso, string Forma, decimal CalifMinima);

public sealed record RubroEntrada(
    Guid? Id, string Nombre, Guid TipoRubroId, decimal Peso, string Forma, decimal CalifMinima, IReadOnlyList<CriterioEntrada>? Criterios);

/// <summary>El conjunto completo de rubros del periodo. Lo que no viene se borra; con calificaciones pide confirmar.</summary>
public sealed record ReemplazarRubros(IReadOnlyList<RubroEntrada> Rubros, bool ConfirmarBorrado);

internal sealed class ReemplazarRubrosValidador : AbstractValidator<ReemplazarRubros>
{
    public ReemplazarRubrosValidador()
    {
        RuleFor(c => c.Rubros).NotNull();
        RuleForEach(c => c.Rubros).ChildRules(r =>
        {
            r.RuleFor(x => x.Nombre).NotNull().MaximumLength(Rubro.LongitudNombre * 2);
            r.RuleFor(x => x.Forma).NotNull().MaximumLength(20);
            r.RuleForEach(x => x.Criterios).ChildRules(c =>
            {
                c.RuleFor(x => x.Nombre).NotNull().MaximumLength(Criterio.LongitudNombre * 2);
                c.RuleFor(x => x.Forma).NotNull().MaximumLength(20);
            });
        });
    }
}

/// <summary>
/// RF-030: guarda los rubros del periodo como borrador (RN-031 se exige al iniciar el cierre). Aplica RN-037 (sin
/// cambios en cierre), RN-110, RN-111, RN-113 (recalcula las actividades con criterios) y RN-120 (forma y mínima).
/// </summary>
public sealed class ReemplazarRubrosCasoUso(
    IRepositorioGrupos grupos, IRepositorioEvaluacion repositorio, ConsultaRubros consulta, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<RubrosDelPeriodoDto>?> EjecutarAsync(Guid periodoId, ReemplazarRubros comando, CancellationToken ct)
    {
        if (await grupos.PeriodoAsync(periodoId, ct) is not { } periodo)
        {
            return null;
        }

        if (ReglasPeriodo.PuedeEditarRubros(periodo) is { } bloqueo)
        {
            return Resultado.Fallo<RubrosDelPeriodoDto>(bloqueo);
        }

        var datos = comando.Rubros.Select(r => new DatosRubro(
            r.Id, r.Nombre, r.TipoRubroId, r.Peso, r.Forma, r.CalifMinima,
            (r.Criterios ?? []).Select(c => new DatosCriterio(c.Id, c.Nombre, c.Peso, c.Forma, c.CalifMinima)).ToList())).ToList();
        var tipos = (await repositorio.TiposRubroAsync(ct)).ToDictionary(t => t.Id);
        var existentes = (await repositorio.RubrosAsync(periodoId, ct)).ToDictionary(r => r.Id);
        if (ReglasRubros.Validar(datos, tipos, existentes.ToDictionary(r => r.Key, r => r.Value.TipoRubroId)) is { } invalido)
        {
            return Resultado.Fallo<RubrosDelPeriodoDto>(invalido);
        }

        if (!IdsValidos(datos, existentes))
        {
            return Resultado.Fallo<RubrosDelPeriodoDto>(ErroresEvaluacion.RubrosInvalidos);
        }

        var actividades = await repositorio.ActividadesAsync(existentes.Keys, ct);
        var rubroDeActividad = actividades.ToDictionary(a => a.Id, a => a.RubroId);
        var celdas = (await repositorio.CalificacionesAsync(rubroDeActividad.Keys, ct)).Where(c => !c.EstaVacia).ToList();
        var celdasPorRubro = celdas.ToLookup(c => rubroDeActividad[c.ActividadId]);

        if (Revisar(datos, existentes, tipos, actividades, celdasPorRubro, comando.ConfirmarBorrado) is { } error)
        {
            return Resultado.Fallo<RubrosDelPeriodoDto>(error);
        }

        var ahora = reloj.GetUtcNow();
        foreach (var quitado in existentes.Values.Where(r => datos.All(d => d.Id != r.Id)))
        {
            repositorio.Borrar(quitado); // RN-113: con confirmación; sus actividades y calificaciones se van con él
        }

        for (var i = 0; i < datos.Count; i++)
        {
            if (datos[i].Id is { } id)
            {
                var rubro = existentes[id];
                rubro.Actualizar(datos[i], i, ahora);
                var reglas = new ReglasCelda(rubro.Forma, rubro.CalifMinima,
                    rubro.Criterios.Select(c => new CriterioCalculo(c.Id, c.Peso, c.Forma, c.CalifMinima)).ToList());
                foreach (var celda in celdasPorRubro[id])
                {
                    celda.Recalcular(reglas, ahora);
                }
            }
            else
            {
                repositorio.Agregar(Rubro.Crear(periodo.Periodo, datos[i], i, ahora));
            }
        }

        await unidad.GuardarAsync(ct);
        return Resultado.Exito(await consulta.LeerAsync(periodo, ct));
    }

    private static bool IdsValidos(IReadOnlyList<DatosRubro> datos, Dictionary<Guid, Rubro> existentes)
    {
        var ids = datos.Where(d => d.Id is not null).Select(d => d.Id!.Value).ToList();
        if (ids.Distinct().Count() != ids.Count || ids.Any(id => !existentes.ContainsKey(id)))
        {
            return false;
        }

        return datos.All(d =>
        {
            var criterios = d.Criterios.Where(c => c.Id is not null).Select(c => c.Id!.Value).ToList();
            return criterios.Distinct().Count() == criterios.Count
                && (criterios.Count == 0 || (d.Id is { } id && criterios.All(c => existentes[id].Criterios.Any(e => e.Id == c))));
        });
    }

    private static ErrorNegocio? Revisar(
        IReadOnlyList<DatosRubro> datos,
        Dictionary<Guid, Rubro> existentes,
        Dictionary<Guid, TipoRubro> tipos,
        IReadOnlyList<Actividad> actividades,
        ILookup<Guid, Calificacion> celdasPorRubro,
        bool confirmado)
    {
        var borraConDatos = existentes.Keys.Any(id => datos.All(d => d.Id != id) && celdasPorRubro[id].Any());
        foreach (var dato in datos.Where(d => d.Id is not null))
        {
            var rubro = existentes[dato.Id!.Value];
            var celdas = celdasPorRubro[rubro.Id].ToList();
            if (tipos[dato.TipoRubroId].Comportamiento != Comportamiento.Actividades && actividades.Any(a => a.RubroId == rubro.Id))
            {
                return ErroresEvaluacion.RubroSinActividades; // RN-039: un rubro con actividades no pasa a asistencia
            }

            if (celdas.Count == 0)
            {
                continue;
            }

            // RN-120: la forma (y si se califica por criterios) no cambia con calificaciones.
            if ((rubro.Criterios.Count == 0) != (dato.Criterios.Count == 0)
                || (rubro.Criterios.Count == 0 && rubro.Forma != dato.Forma))
            {
                return ErroresEvaluacion.FormaConCalificaciones;
            }

            var detalle = celdas.SelectMany(c => c.Criterios).ToList();
            foreach (var criterio in rubro.Criterios)
            {
                var nuevo = dato.Criterios.FirstOrDefault(c => c.Id == criterio.Id);
                var usado = detalle.Any(d => d.CriterioId == criterio.Id);
                if (nuevo is null)
                {
                    borraConDatos |= usado;
                }
                else if (usado && nuevo.Forma != criterio.Forma)
                {
                    return ErroresEvaluacion.FormaConCalificaciones;
                }
            }

            // RN-120: no se guarda una mínima mayor que valores ya capturados.
            var menores = rubro.Criterios.Count == 0
                ? dato.Forma == FormaCalificacion.Numerica ? celdas.Count(c => c.Valor < dato.CalifMinima && c.Marca == MarcaCalificacion.Ninguna) : 0
                : detalle.Count(d => dato.Criterios.FirstOrDefault(c => c.Id == d.CriterioId) is { Forma: FormaCalificacion.Numerica } c
                    && d.Marca == MarcaCalificacion.Ninguna && d.Valor < c.CalifMinima);
            if (menores > 0)
            {
                return ErroresEvaluacion.CalificacionesMenoresAMinima(menores);
            }
        }

        return borraConDatos && !confirmado ? ErroresEvaluacion.BorradoRequiereConfirmacion : null;
    }
}

public sealed record CopiarRubros(Guid DesdePeriodoId);

/// <summary>RF-031, RN-113: copia los rubros (con criterios) de otro periodo del docente a uno sin rubros.</summary>
public sealed class CopiarRubrosCasoUso(
    IRepositorioGrupos grupos, IRepositorioEvaluacion repositorio, ConsultaRubros consulta, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<RubrosDelPeriodoDto>?> EjecutarAsync(Guid periodoId, CopiarRubros comando, CancellationToken ct)
    {
        if (await grupos.PeriodoAsync(periodoId, ct) is not { } destino || await grupos.PeriodoAsync(comando.DesdePeriodoId, ct) is null)
        {
            return null;
        }

        if (ReglasPeriodo.PuedeEditarRubros(destino) is { } bloqueo)
        {
            return Resultado.Fallo<RubrosDelPeriodoDto>(bloqueo);
        }

        if ((await repositorio.RubrosAsync(periodoId, ct)).Count > 0)
        {
            return Resultado.Fallo<RubrosDelPeriodoDto>(ErroresEvaluacion.PeriodoConRubros);
        }

        var ahora = reloj.GetUtcNow();
        foreach (var rubro in await repositorio.RubrosAsync(comando.DesdePeriodoId, ct))
        {
            repositorio.Agregar(rubro.CopiarA(destino.Periodo, ahora));
        }

        await unidad.GuardarAsync(ct);
        return Resultado.Exito(await consulta.LeerAsync(destino, ct));
    }
}
