using AulaLista.Application.Comun;
using AulaLista.Application.Grupos;
using AulaLista.Domain.Cierre;
using AulaLista.Domain.Comun;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace AulaLista.Application.Cierre;

public sealed record ObservacionDto(
    Guid Id,
    Guid AlumnoId,
    Guid PeriodoId,
    string Estado,
    string? Texto,
    string? Instruccion,
    int Regeneraciones,
    int RegeneracionesRestantes,
    bool DatosCambiaron,
    string? Fallo,
    DateTimeOffset? AprobadaEn)
{
    public static ObservacionDto De(Observacion o, DateTimeOffset ahora)
    {
        var atorada = o.GeneracionAtorada(ahora);
        return new(
            o.Id, o.AlumnoId, o.PeriodoId,
            atorada ? (o.Texto is null ? EstadoObservacion.SinGenerar : EstadoObservacion.Borrador) : o.Estado,
            o.Texto, o.Instruccion, o.Regeneraciones, Math.Max(0, Observacion.RegeneracionesMaximas - o.Regeneraciones),
            o.DatosCambiaron, atorada ? FalloGeneracion.Error : o.Fallo, o.AprobadaEn);
    }
}

public sealed class ListarObservacionesCasoUso(IRepositorioGrupos grupos, IRepositorioCierre repositorio, TimeProvider reloj)
{
    public async Task<IReadOnlyList<ObservacionDto>?> EjecutarAsync(Guid periodoId, CancellationToken ct)
    {
        if (await grupos.PeriodoAsync(periodoId, ct) is null)
        {
            return null;
        }

        var ahora = reloj.GetUtcNow();
        return (await repositorio.ObservacionesAsync(periodoId, ct)).Select(o => ObservacionDto.De(o, ahora)).ToList();
    }
}

public sealed record EditarObservacion(string Texto);

public sealed record RegenerarObservacion(string? Instruccion);

/// <summary>Aprobar con el texto que está en pantalla, si lo hay, en una sola llamada.</summary>
public sealed record AprobarObservacion(string? Texto);

internal sealed class EditarObservacionValidador : AbstractValidator<EditarObservacion>
{
    public EditarObservacionValidador() => RuleFor(c => c.Texto).NotNull().MaximumLength(Observacion.LongitudTexto * 2);
}

internal sealed class RegenerarObservacionValidador : AbstractValidator<RegenerarObservacion>
{
    public RegenerarObservacionValidador() => RuleFor(c => c.Instruccion).MaximumLength(Observacion.LongitudInstruccion * 2);
}

internal sealed class AprobarObservacionValidador : AbstractValidator<AprobarObservacion>
{
    public AprobarObservacionValidador() => RuleFor(c => c.Texto).MaximumLength(Observacion.LongitudTexto * 2);
}

/// <summary>
/// RF-042, RF-046: editar, regenerar, aprobar y reabrir una observación. Todas piden el periodo en cierre; una
/// observación ajena no existe (404).
/// </summary>
public sealed class AccionesObservacion(
    IRepositorioGrupos grupos,
    IRepositorioCierre repositorio,
    IColaTrabajos cola,
    IContextoUsuario usuario,
    IUnidadDeTrabajo unidad,
    Planes.AccesoPro acceso,
    IOptions<OpcionesIa> opciones,
    TimeProvider reloj)
{
    public Task<Resultado<ObservacionDto>?> EditarAsync(Guid id, EditarObservacion comando, CancellationToken ct) =>
        AplicarAsync(id, (o, ahora) => Task.FromResult(o.Editar(comando.Texto, ahora)), ct);

    public Task<Resultado<ObservacionDto>?> AprobarAsync(Guid id, AprobarObservacion comando, CancellationToken ct) =>
        AplicarAsync(id, (o, ahora) => Task.FromResult(o.Aprobar(comando.Texto, ahora)), ct);

    public Task<Resultado<ObservacionDto>?> ReabrirAsync(Guid id, CancellationToken ct) =>
        AplicarAsync(id, (o, ahora) => Task.FromResult(o.Reabrir(ahora)), ct);

    /// <summary>RN-043: reemplaza el borrador con otra versión; cuenta para el límite por hora (RNF-004).</summary>
    public async Task<Resultado<ObservacionDto>?> RegenerarAsync(Guid id, RegenerarObservacion comando, CancellationToken ct)
    {
        Guid? periodoId = null;
        var resultado = await AplicarAsync(id, async (o, ahora) =>
        {
            if (await repositorio.SolicitudesIaDesdeAsync(ahora.AddHours(-1), ahora, ct) >= opciones.Value.SolicitudesPorHora)
            {
                return Resultado.Fallo(ErroresCierre.LimiteIa);
            }

            periodoId = o.PeriodoId;
            return o.PedirRegeneracion(comando.Instruccion, ahora);
        }, ct);

        if (resultado is { EsExito: true } && periodoId is { } periodo)
        {
            cola.EncolarObservaciones(usuario.DocenteId!.Value, periodo, [id]);
        }

        return resultado;
    }

    private async Task<Resultado<ObservacionDto>?> AplicarAsync(
        Guid id, Func<Observacion, DateTimeOffset, Task<Resultado>> accion, CancellationToken ct)
    {
        if (await repositorio.ObservacionAsync(id, ct) is not { } observacion
            || await grupos.PeriodoAsync(observacion.PeriodoId, ct) is not { } periodo)
        {
            return null;
        }

        if (ReglasCierre.PuedeRevisar(periodo, await acceso.TieneProAsync(periodo.Grupo, ct)) is { } bloqueo)
        {
            return Resultado.Fallo<ObservacionDto>(bloqueo);
        }

        var ahora = reloj.GetUtcNow();
        var resultado = await accion(observacion, ahora);
        if (resultado.EsFallo)
        {
            return Resultado.Fallo<ObservacionDto>(resultado.Error);
        }

        await unidad.GuardarAsync(ct);
        return Resultado.Exito(ObservacionDto.De(observacion, ahora));
    }
}

public sealed class EditarObservacionCasoUso(AccionesObservacion acciones)
{
    public Task<Resultado<ObservacionDto>?> EjecutarAsync(Guid id, EditarObservacion comando, CancellationToken ct) => acciones.EditarAsync(id, comando, ct);
}

public sealed class RegenerarObservacionCasoUso(AccionesObservacion acciones)
{
    public Task<Resultado<ObservacionDto>?> EjecutarAsync(Guid id, RegenerarObservacion comando, CancellationToken ct) => acciones.RegenerarAsync(id, comando, ct);
}

public sealed class AprobarObservacionCasoUso(AccionesObservacion acciones)
{
    public Task<Resultado<ObservacionDto>?> EjecutarAsync(Guid id, AprobarObservacion comando, CancellationToken ct) => acciones.AprobarAsync(id, comando, ct);
}

public sealed class ReabrirObservacionCasoUso(AccionesObservacion acciones)
{
    public Task<Resultado<ObservacionDto>?> EjecutarAsync(Guid id, CancellationToken ct) => acciones.ReabrirAsync(id, ct);
}

public sealed record EstiloDto(string Longitud, string Persona, IReadOnlyList<string> Ejemplos, int PalabrasMinimas, int PalabrasMaximas)
{
    public static EstiloDto De(EstiloObservaciones e)
    {
        var (minimo, maximo) = LongitudObservacion.Palabras(e.Longitud);
        return new(e.Longitud, e.Persona, e.Ejemplos, minimo, maximo);
    }
}

public sealed record GuardarEstilo(string Longitud, string Persona, IReadOnlyList<string> Ejemplos);

internal sealed class GuardarEstiloValidador : AbstractValidator<GuardarEstilo>
{
    public GuardarEstiloValidador()
    {
        RuleFor(c => c.Longitud).NotEmpty().MaximumLength(20);
        RuleFor(c => c.Persona).NotEmpty().MaximumLength(20);
        RuleFor(c => c.Ejemplos).NotNull();
        RuleForEach(c => c.Ejemplos).NotNull().MaximumLength(EstiloObservaciones.LongitudEjemplo * 2);
    }
}

/// <summary>RF-043: el estilo del docente; sin configurar es el de por defecto.</summary>
public sealed class ObtenerEstiloCasoUso(IRepositorioCierre repositorio, IContextoUsuario usuario, TimeProvider reloj)
{
    public async Task<EstiloDto> EjecutarAsync(CancellationToken ct)
    {
        var docente = usuario.DocenteId!.Value;
        return EstiloDto.De(await repositorio.EstiloAsync(docente, ct) ?? EstiloObservaciones.PorDefecto(docente, reloj.GetUtcNow()));
    }
}

public sealed class GuardarEstiloCasoUso(IRepositorioCierre repositorio, IContextoUsuario usuario, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<EstiloDto>> EjecutarAsync(GuardarEstilo comando, CancellationToken ct)
    {
        var docente = usuario.DocenteId!.Value;
        var ahora = reloj.GetUtcNow();
        var estilo = await repositorio.EstiloAsync(docente, ct);
        var nuevo = estilo is null;
        estilo ??= EstiloObservaciones.PorDefecto(docente, ahora);
        var resultado = estilo.Actualizar(comando.Longitud, comando.Persona, comando.Ejemplos, ahora);
        if (resultado.EsFallo)
        {
            return Resultado.Fallo<EstiloDto>(resultado.Error);
        }

        if (nuevo)
        {
            repositorio.Agregar(estilo);
        }

        await unidad.GuardarAsync(ct);
        return Resultado.Exito(EstiloDto.De(estilo));
    }
}
