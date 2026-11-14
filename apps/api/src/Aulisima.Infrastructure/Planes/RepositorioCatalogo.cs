using Aulisima.Application.Planes;
using Aulisima.Domain.Planes;
using Aulisima.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Aulisima.Infrastructure.Planes;

/// <summary>El catálogo de productos (ADR-0017): lo lee cualquiera; RLS deja escribir solo con <c>app.admin_id</c>.</summary>
internal sealed class RepositorioCatalogo(AulisimaDbContext db) : IRepositorioCatalogo
{
    public async Task<IReadOnlyList<Producto>> ProductosAsync(CancellationToken cancellationToken) =>
        await db.Productos.Include(p => p.Precios).AsSplitQuery().ToListAsync(cancellationToken);

    public Task<Producto?> ProductoAsync(Guid id, CancellationToken cancellationToken) =>
        db.Productos.Include(p => p.Precios).SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public void Agregar(Producto producto) => db.Productos.Add(producto);

    public void Agregar(PrecioProducto precio)
    {
        if (db.Entry(precio).State == EntityState.Detached)
        {
            db.PreciosProducto.Add(precio);
        }
    }

    public async Task<IReadOnlyList<FilaPrecioSuscripcion>> PreciosSuscripcionAsync(CancellationToken cancellationToken) =>
        await db.Database.SqlQuery<FilaPrecioSuscripcion>(
                $"SELECT docente_id AS \"DocenteId\", precio_id AS \"PrecioId\", estado AS \"Estado\" FROM admin_precios_suscripcion()")
            .ToListAsync(cancellationToken);
}
