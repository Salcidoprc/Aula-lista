using AulaLista.Application.Comun;
using AulaLista.Application.Grupos;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Evaluacion;
using AulaLista.Domain.Grupos;
using FluentValidation;

namespace AulaLista.Application.Evaluacion;

public sealed record EquipoDto(Guid Id, string Nombre, IReadOnlyList<Guid> AlumnoIds)
{
    public static EquipoDto De(Equipo e) => new(e.Id, e.Nombre, e.Integrantes.Select(i => i.AlumnoId).ToList());
}

/// <summary>La composición con que se calificó una actividad por equipo (RN-116).</summary>
public sealed record ComposicionDto(Guid ActividadId, Guid EquipoId, string Nombre, IReadOnlyList<Guid> AlumnoIds);

/// <summary>Los equipos tal como deben quedar. Un equipo sin <c>Id</c> es nuevo; uno que falta se quita.</summary>
public sealed record ReemplazarEquipos(IReadOnlyList<DatosEquipo> Equipos);

internal sealed class ReemplazarEquiposValidador : AbstractValidator<ReemplazarEquipos>
{
    public ReemplazarEquiposValidador()
    {
        RuleFor(c => c.Equipos).NotNull().Must(e => e.Count <= Equipo.MaximoPorGrupo);
        RuleForEach(c => c.Equipos).ChildRules(e =>
        {
            e.RuleFor(x => x.Nombre).NotNull().MaximumLength(Equipo.LongitudNombre * 2);
            e.RuleFor(x => x.AlumnoIds).NotNull().Must(a => a.Count <= Grupo.MaximoAlumnosActivos);
        });
    }
}

public sealed class ListarEquiposCasoUso(IRepositorioGrupos grupos, IRepositorioEvaluacion repositorio)
{
    public async Task<IReadOnlyList<EquipoDto>?> EjecutarAsync(Guid grupoId, CancellationToken ct) =>
        await grupos.GrupoAsync(grupoId, ct) is null ? null : (await repositorio.EquiposAsync(grupoId, ct)).Select(EquipoDto.De).ToList();
}

/// <summary>
/// RN-114: reemplaza los equipos del grupo. Reorganizar no toca calificaciones (RN-116, RN-117): las actividades
/// ya calificadas por equipo conservan su composición.
/// </summary>
public sealed class ReemplazarEquiposCasoUso(
    IRepositorioGrupos grupos, IRepositorioEvaluacion repositorio, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<IReadOnlyList<EquipoDto>>?> EjecutarAsync(Guid grupoId, ReemplazarEquipos comando, CancellationToken ct)
    {
        if (await grupos.GrupoAsync(grupoId, ct) is not { } grupo)
        {
            return null;
        }

        if (grupo.EstaArchivado)
        {
            return Resultado.Fallo<IReadOnlyList<EquipoDto>>(ErroresGrupos.GrupoArchivado);
        }

        var existentes = await repositorio.EquiposAsync(grupoId, ct);
        var error = Equipo.Validar(comando.Equipos, await grupos.AlumnosAsync(grupoId, ct));
        if (error is null && comando.Equipos.Any(e => e.Id is { } id && existentes.All(x => x.Id != id)))
        {
            error = ErroresEvaluacion.EquiposInvalidos; // un id que no es de este grupo
        }

        if (error is not null)
        {
            return Resultado.Fallo<IReadOnlyList<EquipoDto>>(error);
        }

        var ahora = reloj.GetUtcNow();
        foreach (var quitado in existentes.Where(e => comando.Equipos.All(d => d.Id != e.Id)))
        {
            repositorio.Borrar(quitado);
        }

        // Un alumno que cambia de equipo sale de uno y entra a otro en el mismo guardado: EF borra antes de
        // insertar sobre el índice único de equipo_alumno.
        var resultado = new List<Equipo>();
        foreach (var (datos, orden) in comando.Equipos.Select((d, i) => (d, i)))
        {
            if (existentes.FirstOrDefault(e => e.Id == datos.Id) is { } equipo)
            {
                equipo.Asignar(datos, orden, ahora);
                resultado.Add(equipo);
            }
            else
            {
                var nuevo = Equipo.Nuevo(grupo, datos, orden, ahora);
                repositorio.Agregar(nuevo);
                resultado.Add(nuevo);
            }
        }

        await unidad.GuardarAsync(ct);
        return Resultado.Exito<IReadOnlyList<EquipoDto>>(resultado.Select(EquipoDto.De).ToList());
    }
}
