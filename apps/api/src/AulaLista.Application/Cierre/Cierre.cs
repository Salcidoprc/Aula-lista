using AulaLista.Application.Comun;
using AulaLista.Application.Evaluacion;
using AulaLista.Application.Grupos;
using AulaLista.Domain.Cierre;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Grupos;
using AulaLista.Domain.Operacion;
using Microsoft.Extensions.Options;

namespace AulaLista.Application.Cierre;

/// <summary>RN-040: lo que pide iniciar el cierre.</summary>
public sealed record RequisitosCierreDto(decimal SumaPesos, bool RubrosSuman100, bool HayCalificaciones);

/// <summary>RF-044: cuántos alumnos hay en cada estado.</summary>
public sealed record ConteosCierreDto(int SinGenerar, int Generando, int Borrador, int Aprobada, int Publicada);

/// <summary>Un alumno activo en el cierre. <see cref="Estado"/> es <c>sin_generar</c> si aún no tiene observación.</summary>
public sealed record AlumnoCierreDto(
    Guid AlumnoId, int NumeroLista, string Nombres, string Apellidos, decimal? Calificacion, string Estado,
    Guid? ObservacionId, bool DatosCambiaron, string? Fallo);

public sealed record CierreDto(
    Guid PeriodoId,
    Guid GrupoId,
    string Estado,
    bool SoloLectura,
    RequisitosCierreDto Requisitos,
    ConteosCierreDto Conteos,
    IReadOnlyList<AlumnoCierreDto> Alumnos);

/// <summary>Qué permite el cierre según el estado del periodo, su grupo y el plan.</summary>
internal static class ReglasCierre
{
    /// <summary>
    /// RN-041: el cierre es de Pro o de la prueba Pro. Decisión del 2026-10-18: mientras no existan los planes (H6)
    /// queda abierto para todos; H6 pone aquí la revisión y responde <see cref="ErroresCierre.RequierePro"/>.
    /// </summary>
    public static ErrorNegocio? PlanPermiteCierre(Grupo grupo) => null;

    /// <summary>Generar, editar y aprobar: con el periodo en cierre y el grupo sin archivar.</summary>
    public static ErrorNegocio? PuedeRevisar(PeriodoCompleto periodo) => periodo.Periodo.Estado switch
    {
        _ when periodo.Grupo.EstaArchivado => ErroresGrupos.GrupoArchivado,
        EstadoPeriodo.Abierto => ErroresCierre.PeriodoSinCierre,
        EstadoPeriodo.Cerrado => Domain.Evaluacion.ErroresEvaluacion.PeriodoCerrado,
        _ => PlanPermiteCierre(periodo.Grupo),
    };
}

/// <summary>Arma el estado del cierre con el cálculo de la matriz (RN-033 a RN-035).</summary>
public sealed class ConsultaCierre(IRepositorioGrupos grupos, IRepositorioCierre repositorio, MatrizCalificacionesCasoUso matriz, TimeProvider reloj)
{
    public async Task<CierreDto?> LeerAsync(Guid periodoId, CancellationToken ct)
    {
        if (await grupos.PeriodoAsync(periodoId, ct) is not { } periodo || await matriz.EjecutarAsync(periodoId, ct) is not { } datos)
        {
            return null;
        }

        var ahora = reloj.GetUtcNow();
        var observaciones = (await repositorio.ObservacionesAsync(periodoId, ct)).ToDictionary(o => o.AlumnoId);
        var resultados = datos.Resultados.ToDictionary(r => r.AlumnoId);
        var alumnos = datos.Alumnos.Where(a => !a.DadoDeBaja).Select(a =>
        {
            var observacion = observaciones.GetValueOrDefault(a.Id);
            var estado = observacion is null || observacion.GeneracionAtorada(ahora) ? EstadoObservacion.SinGenerar : observacion.Estado;
            return new AlumnoCierreDto(
                a.Id, a.NumeroLista, a.Nombres, a.Apellidos, resultados.GetValueOrDefault(a.Id)?.Periodo, estado,
                observacion?.Id, observacion?.DatosCambiaron ?? false,
                observacion?.GeneracionAtorada(ahora) == true ? FalloGeneracion.Error : observacion?.Fallo);
        }).ToList();

        var cuenta = alumnos.CountBy(a => a.Estado).ToDictionary();
        return new CierreDto(
            periodoId,
            periodo.Grupo.Id,
            periodo.Periodo.Estado,
            ReglasCierre.PuedeRevisar(periodo) is not null && periodo.Periodo.Estado != EstadoPeriodo.Abierto,
            new RequisitosCierreDto(datos.SumaPesos, datos.SumaPesos == 100m, datos.Celdas.Count > 0 || datos.Tomas > 0),
            new ConteosCierreDto(
                cuenta.GetValueOrDefault(EstadoObservacion.SinGenerar), cuenta.GetValueOrDefault(EstadoObservacion.Generando),
                cuenta.GetValueOrDefault(EstadoObservacion.Borrador), cuenta.GetValueOrDefault(EstadoObservacion.Aprobada),
                cuenta.GetValueOrDefault(EstadoObservacion.Publicada)),
            alumnos);
    }
}

public sealed class ObtenerCierreCasoUso(ConsultaCierre consulta)
{
    public Task<CierreDto?> EjecutarAsync(Guid periodoId, CancellationToken ct) => consulta.LeerAsync(periodoId, ct);
}

/// <summary>RF-040, RN-040, RN-041: pasa el periodo a <c>en_cierre</c>. Repetirlo no cambia nada.</summary>
public sealed class IniciarCierreCasoUso(
    IRepositorioGrupos grupos, IRepositorioCierre repositorio, ConsultaCierre consulta, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<CierreDto>?> EjecutarAsync(Guid periodoId, CancellationToken ct)
    {
        if (await grupos.PeriodoAsync(periodoId, ct) is not { } periodo || await consulta.LeerAsync(periodoId, ct) is not { } cierre)
        {
            return null;
        }

        if (periodo.Periodo.Estado == EstadoPeriodo.EnCierre)
        {
            return Resultado.Exito(cierre);
        }

        var bloqueo = periodo switch
        {
            _ when periodo.Grupo.EstaArchivado => ErroresGrupos.GrupoArchivado,
            { Periodo.Estado: EstadoPeriodo.Cerrado } => Domain.Evaluacion.ErroresEvaluacion.PeriodoCerrado,
            _ when !cierre.Requisitos.RubrosSuman100 => ErroresCierre.RubrosNoSuman100,
            _ when !cierre.Requisitos.HayCalificaciones => ErroresCierre.SinCalificaciones,
            _ => ReglasCierre.PlanPermiteCierre(periodo.Grupo),
        };
        if (bloqueo is not null)
        {
            return Resultado.Fallo<CierreDto>(bloqueo);
        }

        // Cada alumno activo arranca con su observación vacía, para poder generarla o escribirla a mano.
        var ahora = reloj.GetUtcNow();
        periodo.Periodo.IniciarCierre(ahora);
        var existentes = (await repositorio.ObservacionesAsync(periodoId, ct)).Select(o => o.AlumnoId).ToHashSet();
        foreach (var alumno in (await grupos.AlumnosAsync(periodo.Grupo.Id, ct)).Where(a => a.EstaActivo && !existentes.Contains(a.Id)))
        {
            repositorio.Agregar(Observacion.Nueva(alumno, periodoId, ahora));
        }

        await unidad.GuardarAsync(ct);
        return Resultado.Exito((await consulta.LeerAsync(periodoId, ct))!);
    }
}

/// <summary>Para quiénes generar: todos los alumnos sin observación o solo los indicados (RF-041).</summary>
public sealed record GenerarObservaciones(IReadOnlyList<Guid>? AlumnoIds);

/// <summary>
/// RF-041: encola la generación de los alumnos activos que no tienen texto. Respeta el límite por hora del docente
/// (RNF-004) antes de encolar; el trabajo corre en segundo plano y la pantalla consulta el avance.
/// </summary>
public sealed class GenerarObservacionesCasoUso(
    IRepositorioGrupos grupos,
    IRepositorioCierre repositorio,
    ConsultaCierre consulta,
    IColaTrabajos cola,
    IContextoUsuario usuario,
    IUnidadDeTrabajo unidad,
    IOptions<OpcionesIa> opciones,
    TimeProvider reloj)
{
    public async Task<Resultado<CierreDto>?> EjecutarAsync(Guid periodoId, GenerarObservaciones comando, CancellationToken ct)
    {
        if (await grupos.PeriodoAsync(periodoId, ct) is not { } periodo)
        {
            return null;
        }

        if (ReglasCierre.PuedeRevisar(periodo) is { } bloqueo)
        {
            return Resultado.Fallo<CierreDto>(bloqueo);
        }

        var ahora = reloj.GetUtcNow();
        var pedidos = comando.AlumnoIds?.ToHashSet();
        var alumnos = (await grupos.AlumnosAsync(periodo.Grupo.Id, ct))
            .Where(a => a.EstaActivo && (pedidos is null || pedidos.Contains(a.Id)))
            .ToList();
        var existentes = (await repositorio.ObservacionesAsync(periodoId, ct)).ToDictionary(o => o.AlumnoId);
        var porGenerar = alumnos
            .Where(a => existentes.GetValueOrDefault(a.Id) is not { } o || o.PuedeGenerarse(ahora))
            .ToList();

        if (porGenerar.Count > 0)
        {
            var usadas = await repositorio.SolicitudesIaDesdeAsync(ahora.AddHours(-1), ahora, ct);
            if (usadas + porGenerar.Count > opciones.Value.SolicitudesPorHora)
            {
                return Resultado.Fallo<CierreDto>(ErroresCierre.LimiteIa);
            }

            var ids = new List<Guid>();
            foreach (var alumno in porGenerar)
            {
                if (existentes.GetValueOrDefault(alumno.Id) is { } observacion)
                {
                    observacion.Encolar(ahora);
                    ids.Add(observacion.Id);
                    continue;
                }

                var nueva = Observacion.Nueva(alumno, periodoId, ahora);
                nueva.Encolar(ahora);
                repositorio.Agregar(nueva);
                ids.Add(nueva.Id);
            }

            await unidad.GuardarAsync(ct);
            cola.EncolarObservaciones(Docente, periodoId, ids);
        }

        return Resultado.Exito((await consulta.LeerAsync(periodoId, ct))!);
    }

    private Guid Docente => usuario.DocenteId ?? throw new InvalidOperationException("Generar requiere un docente autenticado.");
}

/// <summary>RN-048: el docente cierra la unidad aunque falten boletas por publicar; queda de solo lectura y en auditoría.</summary>
public sealed class CerrarPeriodoCasoUso(
    IRepositorioGrupos grupos, ConsultaCierre consulta, IAuditoria auditoria, IContextoUsuario usuario, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<CierreDto>?> EjecutarAsync(Guid periodoId, CancellationToken ct)
    {
        if (await grupos.PeriodoAsync(periodoId, ct) is not { } periodo)
        {
            return null;
        }

        if (periodo.Periodo.Estado != EstadoPeriodo.Cerrado)
        {
            if (ReglasCierre.PuedeRevisar(periodo) is { } bloqueo)
            {
                return Resultado.Fallo<CierreDto>(bloqueo);
            }

            periodo.Periodo.Cerrar(reloj.GetUtcNow());
            auditoria.Registrar(AccionAuditoria.PeriodoCerrado, "periodo", periodoId, usuario.DocenteId);
            await unidad.GuardarAsync(ct);
        }

        return Resultado.Exito((await consulta.LeerAsync(periodoId, ct))!);
    }
}

/// <summary>
/// RN-038: reabrir una unidad cerrada la regresa a cierre para corregir calificaciones u observaciones. La confirmación
/// la pide la pantalla y queda en auditoría. Las boletas publicadas no cambian hasta emitir su versión corregida.
/// </summary>
public sealed class ReabrirPeriodoCasoUso(
    IRepositorioGrupos grupos, ConsultaCierre consulta, IAuditoria auditoria, IContextoUsuario usuario, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<CierreDto>?> EjecutarAsync(Guid periodoId, CancellationToken ct)
    {
        if (await grupos.PeriodoAsync(periodoId, ct) is not { } periodo)
        {
            return null;
        }

        if (periodo.Grupo.EstaArchivado)
        {
            return Resultado.Fallo<CierreDto>(ErroresGrupos.GrupoArchivado);
        }

        if (periodo.Periodo.Estado == EstadoPeriodo.Cerrado)
        {
            periodo.Periodo.Reabrir(reloj.GetUtcNow());
            auditoria.Registrar(AccionAuditoria.PeriodoReabierto, "periodo", periodoId, usuario.DocenteId);
            await unidad.GuardarAsync(ct);
        }

        return Resultado.Exito((await consulta.LeerAsync(periodoId, ct))!);
    }
}
