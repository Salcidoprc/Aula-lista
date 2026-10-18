using AulaLista.Application.Comun;
using AulaLista.Application.Grupos;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Evaluacion;
using FluentValidation;

namespace AulaLista.Application.Evaluacion;

/// <summary>Una actividad con cuántas celdas ya tienen calificación (el avance de la lista).</summary>
public sealed record ActividadDto(Guid Id, Guid RubroId, string Nombre, DateOnly? Fecha, string Modo, int Orden, int Capturadas)
{
    public static ActividadDto De(Actividad a, int capturadas) => new(a.Id, a.RubroId, a.Nombre, a.Fecha, a.Modo, a.Orden, capturadas);
}

/// <summary>Sin <see cref="Modo"/>, una actividad nueva es individual y una editada conserva el suyo (RN-115).</summary>
public sealed record DatosActividad(string Nombre, DateOnly? Fecha, string? Modo = null);

internal sealed class DatosActividadValidador : AbstractValidator<DatosActividad>
{
    public DatosActividadValidador()
    {
        RuleFor(c => c.Nombre).NotNull().MaximumLength(Actividad.LongitudNombre * 2);
        RuleFor(c => c.Modo).MaximumLength(20);
    }
}

/// <summary>El rubro con su periodo, para aplicar las reglas del periodo a sus actividades.</summary>
public sealed class ContextoRubros(IRepositorioGrupos grupos, IRepositorioEvaluacion repositorio)
{
    public async Task<(Rubro Rubro, PeriodoCompleto Periodo, TipoRubro Tipo)?> RubroAsync(Guid rubroId, CancellationToken ct)
    {
        if (await repositorio.RubroAsync(rubroId, ct) is not { } rubro || await grupos.PeriodoAsync(rubro.PeriodoId, ct) is not { } periodo)
        {
            return null;
        }

        var tipo = (await repositorio.TiposRubroAsync(ct)).Single(t => t.Id == rubro.TipoRubroId);
        return (rubro, periodo, tipo);
    }
}

public sealed class ListarActividadesCasoUso(IRepositorioEvaluacion repositorio)
{
    public async Task<IReadOnlyList<ActividadDto>?> EjecutarAsync(Guid rubroId, CancellationToken ct)
    {
        if (await repositorio.RubroAsync(rubroId, ct) is null)
        {
            return null;
        }

        var actividades = await repositorio.ActividadesAsync([rubroId], ct);
        var capturadas = (await repositorio.CalificacionesAsync(actividades.Select(a => a.Id).ToList(), ct))
            .Where(c => !c.EstaVacia).CountBy(c => c.ActividadId).ToDictionary();
        return actividades.OrderBy(a => a.Orden).Select(a => ActividadDto.De(a, capturadas.GetValueOrDefault(a.Id))).ToList();
    }
}

/// <summary>RF-032, RN-039: agrega una actividad al final del rubro. También en cierre (RN-037), no en cerrado.</summary>
public sealed class CrearActividadCasoUso(ContextoRubros contexto, IRepositorioEvaluacion repositorio, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<ActividadDto>?> EjecutarAsync(Guid rubroId, DatosActividad comando, CancellationToken ct)
    {
        if (await contexto.RubroAsync(rubroId, ct) is not { } encontrado)
        {
            return null;
        }

        var (rubro, periodo, tipo) = encontrado;
        if (ReglasPeriodo.PuedeCapturar(periodo) is { } bloqueo)
        {
            return Resultado.Fallo<ActividadDto>(bloqueo);
        }

        var existentes = await repositorio.ActividadesAsync([rubroId], ct);
        var orden = existentes.Count == 0 ? 0 : existentes.Max(a => a.Orden) + 1;
        var creada = Actividad.Crear(
            rubro, tipo, periodo.Unidad, comando.Nombre, comando.Fecha, comando.Modo ?? ModoActividad.Individual, orden, reloj.GetUtcNow());
        if (creada.EsFallo)
        {
            return Resultado.Fallo<ActividadDto>(creada.Error);
        }

        repositorio.Agregar(creada.Valor);
        await unidad.GuardarAsync(ct);
        return Resultado.Exito(ActividadDto.De(creada.Valor, 0));
    }
}

public sealed class EditarActividadCasoUso(
    IRepositorioGrupos grupos, IRepositorioEvaluacion repositorio, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<ActividadDto>?> EjecutarAsync(Guid id, DatosActividad comando, CancellationToken ct)
    {
        if (await repositorio.ActividadAsync(id, ct) is not { } actividad
            || await repositorio.RubroAsync(actividad.RubroId, ct) is not { } rubro
            || await grupos.PeriodoAsync(rubro.PeriodoId, ct) is not { } periodo)
        {
            return null;
        }

        if (ReglasPeriodo.PuedeCapturar(periodo) is { } bloqueo)
        {
            return Resultado.Fallo<ActividadDto>(bloqueo);
        }

        var editada = actividad.Editar(periodo.Unidad, comando.Nombre, comando.Fecha, comando.Modo ?? actividad.Modo, reloj.GetUtcNow());
        if (editada.EsFallo)
        {
            return Resultado.Fallo<ActividadDto>(editada.Error);
        }

        await unidad.GuardarAsync(ct);
        var capturadas = (await repositorio.CalificacionesAsync([id], ct)).Count(c => !c.EstaVacia);
        return Resultado.Exito(ActividadDto.De(actividad, capturadas));
    }
}

/// <summary>SPEC-03: borrar una actividad con calificaciones pide confirmación; los promedios se recalculan solos.</summary>
public sealed class BorrarActividadCasoUso(
    IRepositorioGrupos grupos, IRepositorioEvaluacion repositorio, IUnidadDeTrabajo unidad, Cierre.AvisoDatosCambiaron aviso)
{
    public async Task<Resultado?> EjecutarAsync(Guid id, bool confirmar, CancellationToken ct)
    {
        if (await repositorio.ActividadAsync(id, ct) is not { } actividad
            || await repositorio.RubroAsync(actividad.RubroId, ct) is not { } rubro
            || await grupos.PeriodoAsync(rubro.PeriodoId, ct) is not { } periodo)
        {
            return null;
        }

        if (ReglasPeriodo.PuedeCapturar(periodo) is { } bloqueo)
        {
            return Resultado.Fallo(bloqueo);
        }

        var calificados = (await repositorio.CalificacionesAsync([id], ct)).Where(c => !c.EstaVacia).Select(c => c.AlumnoId).ToList();
        if (!confirmar && calificados.Count > 0)
        {
            return Resultado.Fallo(ErroresEvaluacion.BorradoRequiereConfirmacion);
        }

        await aviso.AvisarAsync(periodo.Periodo.Id, calificados, ct); // RN-044
        repositorio.Borrar(actividad);
        await unidad.GuardarAsync(ct);
        return Resultado.Exito();
    }
}
