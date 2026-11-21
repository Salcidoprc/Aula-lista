using Aulisima.Application.Comun;
using Aulisima.Application.Grupos;
using Aulisima.Domain.Comun;
using Aulisima.Domain.Evaluacion;
using Aulisima.Domain.Grupos;
using FluentValidation;

namespace Aulisima.Application.Evaluacion;

public sealed record ParticipacionDto(Guid Id, Guid AlumnoId, DateOnly Fecha)
{
    public static ParticipacionDto De(Participacion p) => new(p.Id, p.AlumnoId, p.Fecha);
}

/// <summary>
/// Una participación nueva (RN-122). <see cref="Id"/> lo genera el cliente, también sin conexión: si ya llegó, se
/// responde la guardada y no se duplica. Sin fecha, es la de hoy.
/// </summary>
public sealed record DarParticipacion(Guid Id, Guid AlumnoId, DateOnly? Fecha);

internal sealed class DarParticipacionValidador : AbstractValidator<DarParticipacion>
{
    public DarParticipacionValidador()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.AlumnoId).NotEmpty();
    }
}

/// <summary>RF-110: participaciones del grupo en un rango de fechas; sin rango, todas.</summary>
public sealed class ListarParticipacionesCasoUso(IRepositorioGrupos grupos, IRepositorioEvaluacion repositorio)
{
    public async Task<IReadOnlyList<ParticipacionDto>?> EjecutarAsync(Guid grupoId, DateOnly? desde, DateOnly? hasta, CancellationToken ct)
    {
        if (await grupos.GrupoAsync(grupoId, ct) is null)
        {
            return null;
        }

        var lista = await repositorio.ParticipacionesAsync(grupoId, desde ?? DateOnly.MinValue, hasta ?? DateOnly.MaxValue, ct);
        return lista.Select(ParticipacionDto.De).ToList();
    }
}

/// <summary>
/// RF-110, RN-122 a RN-125: da una participación a un alumno activo del grupo en la unidad que contiene la fecha, si
/// esa unidad admite captura. Cambia la calificación, así que en cierre marca la observación del alumno (RN-044).
/// </summary>
public sealed class DarParticipacionCasoUso(
    IRepositorioGrupos grupos, IRepositorioEvaluacion repositorio, IUnidadDeTrabajo unidad, Cierre.AvisoDatosCambiaron aviso, TimeProvider reloj)
{
    public async Task<Resultado<ParticipacionDto>?> EjecutarAsync(Guid grupoId, DarParticipacion comando, CancellationToken ct)
    {
        if (await grupos.GrupoAsync(grupoId, ct) is not { } grupo)
        {
            return null;
        }

        if (await repositorio.ParticipacionAsync(comando.Id, ct) is { } repetida)
        {
            return repetida.GrupoId == grupoId ? Resultado.Exito(ParticipacionDto.De(repetida)) : null;
        }

        if (await grupos.AlumnoAsync(comando.AlumnoId, ct) is not { } alumno || alumno.GrupoId != grupoId)
        {
            return Resultado.Fallo<ParticipacionDto>(ErroresEvaluacion.ParticipacionInvalida);
        }

        var ahora = reloj.GetUtcNow();
        var fecha = comando.Fecha ?? DateOnly.FromDateTime(ahora.UtcDateTime);
        if (await ReglasAsistencia.PeriodoDeAsync(grupos, grupo, fecha, ct) is not { } periodo)
        {
            return Resultado.Fallo<ParticipacionDto>(ErroresEvaluacion.FechaFueraDeUnidad);
        }

        if (ReglasPeriodo.PuedeCapturar(periodo) is { } bloqueo)
        {
            return Resultado.Fallo<ParticipacionDto>(bloqueo); // RN-125
        }

        var creada = Participacion.Crear(comando.Id, alumno, periodo.Periodo, fecha, ahora);
        if (creada.EsFallo)
        {
            return Resultado.Fallo<ParticipacionDto>(creada.Error);
        }

        repositorio.Agregar(creada.Valor);
        await aviso.AvisarAsync(periodo.Periodo.Id, [alumno.Id], ct); // RN-044
        await unidad.GuardarAsync(ct);
        return Resultado.Exito(ParticipacionDto.De(creada.Valor));
    }
}

/// <summary>RN-125: quita una participación mientras su unidad admita captura.</summary>
public sealed class QuitarParticipacionCasoUso(
    IRepositorioGrupos grupos, IRepositorioEvaluacion repositorio, IUnidadDeTrabajo unidad, Cierre.AvisoDatosCambiaron aviso)
{
    public async Task<Resultado?> EjecutarAsync(Guid id, CancellationToken ct)
    {
        if (await repositorio.ParticipacionAsync(id, ct) is not { } participacion
            || await grupos.PeriodoAsync(participacion.PeriodoId, ct) is not { } periodo)
        {
            return null;
        }

        if (ReglasPeriodo.PuedeCapturar(periodo) is { } bloqueo)
        {
            return Resultado.Fallo(bloqueo);
        }

        repositorio.Borrar(participacion);
        await aviso.AvisarAsync(periodo.Periodo.Id, [participacion.AlumnoId], ct); // RN-044
        await unidad.GuardarAsync(ct);
        return Resultado.Exito();
    }
}
