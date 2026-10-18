using System.Collections.Concurrent;
using AulaLista.Application.Avisos;
using AulaLista.Application.Boletas;
using AulaLista.Application.Cierre;
using AulaLista.Application.Comun;
using AulaLista.Application.Cuentas;
using AulaLista.Infrastructure.Persistencia;
using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AulaLista.Infrastructure.Trabajos;

/// <summary>Envía a la cola los trabajos pedidos durante una solicitud, una vez confirmada su transacción.</summary>
public interface IDespachoTrabajos
{
    void Despachar();
}

/// <summary>
/// Encola en Hangfire (02-arquitectura, Jobs). Solo viajan IDs. Los trabajos se retienen hasta que la
/// transacción de la solicitud se confirma; si no, el trabajo podría correr antes de ver sus datos.
/// </summary>
internal sealed class ColaTrabajosHangfire(IBackgroundJobClient cliente) : IColaTrabajos, IDespachoTrabajos
{
    private readonly List<Action> _pendientes = [];

    public void EncolarExportacion(Guid docenteId, Guid exportacionId) =>
        _pendientes.Add(() => cliente.Enqueue<EjecutorTrabajos>(
            e => e.GenerarExportacionAsync(docenteId, exportacionId, null, CancellationToken.None)));

    public void EncolarObservaciones(Guid docenteId, Guid periodoId, IReadOnlyList<Guid> observacionIds)
    {
        var ids = observacionIds.ToArray();
        _pendientes.Add(() => cliente.Enqueue<EjecutorTrabajos>(
            e => e.RedactarObservacionesAsync(docenteId, periodoId, ids, CancellationToken.None)));
    }

    public void EncolarBoletas(Guid docenteId, IReadOnlyList<Guid> boletaIds)
    {
        var ids = boletaIds.ToArray();
        _pendientes.Add(() => cliente.Enqueue<EjecutorTrabajos>(e => e.PrepararBoletasAsync(docenteId, ids, null, CancellationToken.None)));
    }

    public void EncolarAviso(Guid docenteId, Guid avisoId) =>
        _pendientes.Add(() => cliente.Enqueue<EjecutorTrabajos>(e => e.EnviarAvisoAsync(docenteId, avisoId, CancellationToken.None)));

    public void EncolarAvisoBoletas(Guid docenteId, Guid periodoId, IReadOnlyList<Guid> boletaIds)
    {
        var ids = boletaIds.ToArray();
        _pendientes.Add(() => cliente.Enqueue<EjecutorTrabajos>(e => e.AvisarBoletasAsync(docenteId, periodoId, ids, CancellationToken.None)));
    }

    public void Despachar()
    {
        foreach (var pendiente in _pendientes)
        {
            pendiente();
        }

        _pendientes.Clear();
    }
}

/// <summary>
/// Corre cada trabajo en su propio alcance, como el docente dueño y dentro de una transacción,
/// para que RLS aplique igual que en una solicitud.
/// </summary>
public sealed partial class EjecutorTrabajos(IServiceScopeFactory alcances, LimitadorIa limitador, ILogger<EjecutorTrabajos> registro)
{
    public const int Reintentos = 3;

    /// <summary>
    /// Genera la exportación. Si falla el último reintento, la marca como fallida para que el docente lo
    /// vea y pida otra. <paramref name="contexto"/> lo llena Hangfire; sin él (pruebas) se trata como último intento.
    /// </summary>
    [AutomaticRetry(Attempts = Reintentos)]
    public async Task GenerarExportacionAsync(
        Guid docenteId, Guid exportacionId, PerformContext? contexto, CancellationToken cancellationToken)
    {
        try
        {
            await GenerarAsync(docenteId, exportacionId, cancellationToken);
        }
        catch (Exception) when ((contexto?.GetJobParameter<int>("RetryCount") ?? Reintentos) >= Reintentos)
        {
            await MarcarFallidaAsync(docenteId, exportacionId, cancellationToken);
            throw;
        }
    }

    private async Task GenerarAsync(Guid docenteId, Guid exportacionId, CancellationToken cancellationToken)
    {
        await using var alcance = alcances.CreateAsyncScope();
        var sp = alcance.ServiceProvider;
        sp.GetRequiredService<IContextoUsuario>().AsumirDocente(docenteId);
        var db = sp.GetRequiredService<AulaListaDbContext>();
        await using var transaccion = await db.Database.BeginTransactionAsync(cancellationToken);
        await sp.GetRequiredService<GenerarExportacionTrabajo>().EjecutarAsync(docenteId, exportacionId, cancellationToken);
        await transaccion.CommitAsync(cancellationToken);
    }

    private async Task MarcarFallidaAsync(Guid docenteId, Guid exportacionId, CancellationToken cancellationToken)
    {
        await using var alcance = alcances.CreateAsyncScope();
        var sp = alcance.ServiceProvider;
        sp.GetRequiredService<IContextoUsuario>().AsumirDocente(docenteId);
        var db = sp.GetRequiredService<AulaListaDbContext>();
        await using var transaccion = await db.Database.BeginTransactionAsync(cancellationToken);
        var exportacion = await sp.GetRequiredService<IRepositorioDocentes>().ExportacionPorIdAsync(exportacionId, cancellationToken);
        if (exportacion is { Estado: Domain.Cuentas.EstadoExportacion.Pendiente })
        {
            exportacion.MarcarFallida(sp.GetRequiredService<TimeProvider>().GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaccion.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// RF-041: redacta las observaciones de un periodo con la IA, a lo más <see cref="OpcionesIa.SimultaneasPorDocente"/>
    /// a la vez por docente. Cada una se guarda al llegar para que la pantalla vea el avance. Los reintentos los hace
    /// el cliente de la IA; si algo más falla, la observación queda "No se pudo generar" y Hangfire no repite.
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    public async Task RedactarObservacionesAsync(Guid docenteId, Guid periodoId, Guid[] observacionIds, CancellationToken cancellationToken)
    {
        IReadOnlyList<EncargoObservacion> encargos;
        try
        {
            encargos = await EnAlcanceAsync(
                docenteId, sp => sp.GetRequiredService<RedactarObservacionesTrabajo>().PrepararAsync(periodoId, observacionIds, cancellationToken), cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            FalloRedaccion(registro, ex, periodoId);
            await MarcarObservacionesFallidasAsync(docenteId, observacionIds, cancellationToken);
            return;
        }

        var semaforo = limitador.De(docenteId);
        await Task.WhenAll(encargos.Select(async encargo =>
        {
            await semaforo.WaitAsync(cancellationToken);
            try
            {
                ObservacionRedactada redactada;
                await using (var alcance = alcances.CreateAsyncScope())
                {
                    redactada = await alcance.ServiceProvider.GetRequiredService<IRedactorObservaciones>()
                        .RedactarAsync(encargo.Entrada, cancellationToken);
                }

                var terminado = RedactarObservacionesTrabajo.Terminar(encargo, redactada);
                await EnAlcanceAsync(docenteId, async sp =>
                {
                    await sp.GetRequiredService<RedactarObservacionesTrabajo>().GuardarAsync(encargo, redactada, terminado, cancellationToken);
                    return true;
                }, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                FalloRedaccion(registro, ex, periodoId);
                await MarcarObservacionesFallidasAsync(docenteId, [encargo.ObservacionId], cancellationToken);
            }
            finally
            {
                semaforo.Release();
            }
        }));
    }

    private async Task MarcarObservacionesFallidasAsync(Guid docenteId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await EnAlcanceAsync(docenteId, async sp =>
        {
            await sp.GetRequiredService<RedactarObservacionesTrabajo>().MarcarFallidasAsync(ids, cancellationToken);
            return true;
        }, cancellationToken);

    /// <summary>Un alcance como el docente, dentro de una transacción, para que RLS aplique.</summary>
    private async Task<T> EnAlcanceAsync<T>(Guid docenteId, Func<IServiceProvider, Task<T>> trabajo, CancellationToken cancellationToken)
    {
        await using var alcance = alcances.CreateAsyncScope();
        var sp = alcance.ServiceProvider;
        sp.GetRequiredService<IContextoUsuario>().AsumirDocente(docenteId);
        var db = sp.GetRequiredService<AulaListaDbContext>();
        await using var transaccion = await db.Database.BeginTransactionAsync(cancellationToken);
        var resultado = await trabajo(sp);
        await transaccion.CommitAsync(cancellationToken);
        return resultado;
    }

    /// <summary>
    /// RF-050: dibuja y guarda los PDF de las boletas. Si falla el último reintento, las que quedaron sin PDF dicen
    /// "No se pudo preparar" y volver a generar lo intenta de nuevo.
    /// </summary>
    [AutomaticRetry(Attempts = Reintentos)]
    public async Task PrepararBoletasAsync(Guid docenteId, Guid[] boletaIds, PerformContext? contexto, CancellationToken cancellationToken)
    {
        try
        {
            await EnAlcanceAsync(docenteId, async sp =>
            {
                await sp.GetRequiredService<PrepararBoletasTrabajo>().EjecutarAsync(boletaIds, cancellationToken);
                return true;
            }, cancellationToken);
        }
        catch (Exception) when ((contexto?.GetJobParameter<int>("RetryCount") ?? Reintentos) >= Reintentos)
        {
            await EnAlcanceAsync(docenteId, async sp =>
            {
                await sp.GetRequiredService<PrepararBoletasTrabajo>().MarcarFallidasAsync(boletaIds, cancellationToken);
                return true;
            }, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// RF-071: envía el aviso. Sin reintentos automáticos para no duplicar push ni correos; lo que falló se cuenta
    /// en la bitácora sin datos de nadie.
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    public async Task EnviarAvisoAsync(Guid docenteId, Guid avisoId, CancellationToken cancellationToken)
    {
        var fallas = await EnAlcanceAsync(
            docenteId, sp => sp.GetRequiredService<EnviarAvisoTrabajo>().EjecutarAsync(avisoId, cancellationToken), cancellationToken);
        if (fallas > 0)
        {
            FallaronEnvios(registro, fallas, "aviso", avisoId);
        }
    }

    /// <summary>RN-054: avisa que las boletas publicadas ya están en el portal.</summary>
    [AutomaticRetry(Attempts = 0)]
    public async Task AvisarBoletasAsync(Guid docenteId, Guid periodoId, Guid[] boletaIds, CancellationToken cancellationToken)
    {
        var fallas = await EnAlcanceAsync(
            docenteId, sp => sp.GetRequiredService<AvisarBoletasTrabajo>().EjecutarAsync(periodoId, boletaIds, cancellationToken), cancellationToken);
        if (fallas > 0)
        {
            FallaronEnvios(registro, fallas, "periodo", periodoId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fallaron {Fallas} envíos de {Entidad} {Id}")]
    private static partial void FallaronEnvios(ILogger registro, int fallas, string entidad, Guid id);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falló la redacción de observaciones del periodo {PeriodoId}")]
    private static partial void FalloRedaccion(ILogger registro, Exception ex, Guid periodoId);

    [AutomaticRetry(Attempts = Reintentos)]
    public async Task RetencionAsync(CancellationToken cancellationToken)
    {
        await using var alcance = alcances.CreateAsyncScope();
        await alcance.ServiceProvider.GetRequiredService<RetencionCuentasTrabajo>().EjecutarAsync(cancellationToken);
    }
}

/// <summary>RNF-004 y reglas/05-ia: máximo de solicitudes simultáneas a la IA por docente, en todo el proceso.</summary>
public sealed class LimitadorIa(IOptions<OpcionesIa> opciones)
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _porDocente = new();

    public SemaphoreSlim De(Guid docenteId) =>
        _porDocente.GetOrAdd(docenteId, _ => new SemaphoreSlim(opciones.Value.SimultaneasPorDocente, opciones.Value.SimultaneasPorDocente));
}
