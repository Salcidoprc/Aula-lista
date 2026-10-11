using AulaLista.Application.Comun;
using AulaLista.Application.Cuentas;
using AulaLista.Infrastructure.Persistencia;
using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.DependencyInjection;

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
public sealed class EjecutorTrabajos(IServiceScopeFactory alcances)
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

    [AutomaticRetry(Attempts = Reintentos)]
    public async Task RetencionAsync(CancellationToken cancellationToken)
    {
        await using var alcance = alcances.CreateAsyncScope();
        await alcance.ServiceProvider.GetRequiredService<RetencionCuentasTrabajo>().EjecutarAsync(cancellationToken);
    }
}
