using AulaLista.Application.Comun;
using AulaLista.Application.Cuentas;
using AulaLista.Infrastructure.Persistencia;
using Hangfire;
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
            e => e.GenerarExportacionAsync(docenteId, exportacionId, CancellationToken.None)));

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
    [AutomaticRetry(Attempts = 3)]
    public async Task GenerarExportacionAsync(Guid docenteId, Guid exportacionId, CancellationToken cancellationToken)
    {
        await using var alcance = alcances.CreateAsyncScope();
        var sp = alcance.ServiceProvider;
        sp.GetRequiredService<IContextoUsuario>().AsumirDocente(docenteId);
        var db = sp.GetRequiredService<AulaListaDbContext>();
        await using var transaccion = await db.Database.BeginTransactionAsync(cancellationToken);
        await sp.GetRequiredService<GenerarExportacionTrabajo>().EjecutarAsync(docenteId, exportacionId, cancellationToken);
        await transaccion.CommitAsync(cancellationToken);
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task RetencionAsync(CancellationToken cancellationToken)
    {
        await using var alcance = alcances.CreateAsyncScope();
        await alcance.ServiceProvider.GetRequiredService<RetencionCuentasTrabajo>().EjecutarAsync(cancellationToken);
    }
}
