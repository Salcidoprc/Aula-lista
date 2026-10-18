using AulaLista.Application.Comun;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AulaLista.Infrastructure.Persistencia;

internal sealed class UnidadDeTrabajo(AulaListaDbContext db, VariablesRlsInterceptor variables) : IUnidadDeTrabajo
{
    public Task GuardarAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public async Task<T> EnTransaccionAsync<T>(Func<Task<T>> trabajo, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is { } actual)
        {
            // La transacción de la solicitud fijó las variables antes de asumir al usuario: se vuelven a fijar.
            await variables.FijarAsync(db.Database.GetDbConnection(), actual.GetDbTransaction(), cancellationToken);
            var dentro = await trabajo();
            await db.SaveChangesAsync(cancellationToken);
            return dentro;
        }

        await using var transaccion = await db.Database.BeginTransactionAsync(cancellationToken);
        var resultado = await trabajo();
        await db.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);
        return resultado;
    }
}
