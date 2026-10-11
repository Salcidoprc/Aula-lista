using AulaLista.Infrastructure.Persistencia;
using AulaLista.Infrastructure.Trabajos;

namespace AulaLista.Api.Comun;

/// <summary>
/// Envuelve cada solicitud autenticada a <c>/api</c> en una transacción para que las variables
/// de Row Level Security vivan solo durante esa solicitud (RNF-001).
/// </summary>
internal sealed class TransaccionPorSolicitud(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AulaListaDbContext db, IDespachoTrabajos despacho)
    {
        if (!context.Request.Path.StartsWithSegments("/api") || context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            despacho.Despachar();
            return;
        }

        await using var transaccion = await db.Database.BeginTransactionAsync(context.RequestAborted);
        await next(context);
        if (context.Response.StatusCode < 400)
        {
            await transaccion.CommitAsync(context.RequestAborted);
            despacho.Despachar();
        }
    }
}
