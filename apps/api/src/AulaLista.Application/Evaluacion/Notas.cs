using AulaLista.Application.Comun;
using AulaLista.Application.Grupos;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Evaluacion;
using AulaLista.Domain.Grupos;
using FluentValidation;

namespace AulaLista.Application.Evaluacion;

public sealed record NotaDto(Guid Id, Guid AlumnoId, Guid PeriodoId, string Texto, string Etiqueta, DateOnly Fecha)
{
    public static NotaDto De(Nota n) => new(n.Id, n.AlumnoId, n.PeriodoId, n.Texto, n.Etiqueta, n.Fecha);
}

/// <summary>
/// Una nota nueva. <see cref="Id"/> lo genera el cliente (también sin conexión): si ya llegó, se responde la
/// guardada y no se duplica. Sin fecha, es la de hoy.
/// </summary>
public sealed record CrearNota(Guid Id, Guid PeriodoId, string Texto, string? Etiqueta, DateOnly? Fecha);

internal sealed class CrearNotaValidador : AbstractValidator<CrearNota>
{
    public CrearNotaValidador()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.PeriodoId).NotEmpty();
        RuleFor(c => c.Texto).NotNull().MaximumLength(Nota.LongitudTexto * 2);
        RuleFor(c => c.Etiqueta).MaximumLength(20);
    }
}

public sealed class ListarNotasCasoUso(IRepositorioGrupos grupos, IRepositorioEvaluacion repositorio)
{
    public async Task<IReadOnlyList<NotaDto>?> EjecutarAsync(Guid alumnoId, CancellationToken ct) =>
        await grupos.AlumnoAsync(alumnoId, ct) is null ? null : (await repositorio.NotasAsync(alumnoId, ct)).Select(NotaDto.De).ToList();
}

/// <summary>
/// SPEC-03, notas por alumno: texto de hasta 1,000 caracteres con etiqueta opcional, en un periodo del grupo del
/// alumno que todavía admite captura. Son privadas del docente.
/// </summary>
public sealed class CrearNotaCasoUso(
    IRepositorioGrupos grupos, IRepositorioEvaluacion repositorio, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<NotaDto>?> EjecutarAsync(Guid alumnoId, CrearNota comando, CancellationToken ct)
    {
        if (await grupos.AlumnoAsync(alumnoId, ct) is not { } alumno)
        {
            return null;
        }

        if (await repositorio.NotaAsync(comando.Id, ct) is { } repetida)
        {
            return repetida.AlumnoId == alumnoId ? Resultado.Exito(NotaDto.De(repetida)) : null;
        }

        if (await grupos.PeriodoAsync(comando.PeriodoId, ct) is not { } periodo || periodo.Grupo.Id != alumno.GrupoId)
        {
            return null;
        }

        if (ReglasPeriodo.PuedeCapturar(periodo) is { } bloqueo)
        {
            return Resultado.Fallo<NotaDto>(bloqueo);
        }

        var ahora = reloj.GetUtcNow();
        var creada = Nota.Crear(
            comando.Id, alumno, periodo.Periodo, comando.Texto, comando.Etiqueta ?? EtiquetaNota.Ninguna,
            comando.Fecha ?? DateOnly.FromDateTime(ahora.UtcDateTime), ahora);
        if (creada.EsFallo)
        {
            return Resultado.Fallo<NotaDto>(creada.Error);
        }

        repositorio.Agregar(creada.Valor);
        await unidad.GuardarAsync(ct);
        return Resultado.Exito(NotaDto.De(creada.Valor));
    }
}
