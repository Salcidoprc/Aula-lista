using Aulisima.Application.Boletas;
using Aulisima.Domain.Boletas;
using Aulisima.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Aulisima.Infrastructure.Boletas;

/// <summary>Boletas con los filtros globales por docente y RLS.</summary>
internal sealed class RepositorioBoletas(AulisimaDbContext db) : IRepositorioBoletas
{
    public async Task<IReadOnlyList<Boleta>> BoletasAsync(Guid periodoId, CancellationToken cancellationToken) =>
        await db.Boletas.Where(b => b.PeriodoId == periodoId).ToListAsync(cancellationToken);

    public Task<Boleta?> BoletaAsync(Guid id, CancellationToken cancellationToken) =>
        db.Boletas.SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Boleta>> BoletasPorIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await db.Boletas.Where(b => ids.Contains(b.Id)).ToListAsync(cancellationToken);

    public void Agregar(Boleta boleta) => db.Boletas.Add(boleta);
}
