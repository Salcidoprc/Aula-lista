using AulaLista.Application.Comun;

namespace AulaLista.Infrastructure.Persistencia;

internal sealed class UnidadDeTrabajo(AulaListaDbContext db) : IUnidadDeTrabajo
{
    public Task GuardarAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
