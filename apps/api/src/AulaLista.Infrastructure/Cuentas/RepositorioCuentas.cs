using AulaLista.Application.Cuentas;
using AulaLista.Domain.Cuentas;
using AulaLista.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AulaLista.Infrastructure.Cuentas;

internal sealed class RepositorioCuentas(AulaListaDbContext db) : IRepositorioCuentas
{
    public Task<Cuenta?> PorCorreoAsync(string correo, CancellationToken cancellationToken) =>
        db.Cuentas.SingleOrDefaultAsync(c => c.Correo == correo, cancellationToken);

    public Task<Cuenta?> PorIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Cuentas.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Cuenta?> PorGoogleSubAsync(string googleSub, CancellationToken cancellationToken) =>
        db.Cuentas.SingleOrDefaultAsync(c => c.GoogleSub == googleSub, cancellationToken);

    public void Agregar(Cuenta cuenta) => db.Cuentas.Add(cuenta);

    public void AgregarToken(TokenCuenta token) => db.TokensCuenta.Add(token);

    public Task<TokenCuenta?> TokenPorHashAsync(string hash, string proposito, CancellationToken cancellationToken) =>
        db.TokensCuenta.SingleOrDefaultAsync(t => t.Hash == hash && t.Proposito == proposito, cancellationToken);

    public Task<int> ContarTokensDesdeAsync(Guid cuentaId, string proposito, DateTimeOffset desde, CancellationToken cancellationToken) =>
        db.TokensCuenta.CountAsync(t => t.CuentaId == cuentaId && t.Proposito == proposito && t.CreatedAt >= desde, cancellationToken);

    public void AgregarSesion(Sesion sesion) => db.Sesiones.Add(sesion);

    public Task<Sesion?> SesionPorHashAsync(string hash, CancellationToken cancellationToken) =>
        db.Sesiones.SingleOrDefaultAsync(s => s.Hash == hash, cancellationToken);

    public Task<Sesion?> SesionVigenteDeFamiliaAsync(Guid familiaId, DateTimeOffset ahora, CancellationToken cancellationToken) =>
        db.Sesiones.SingleOrDefaultAsync(
            s => s.FamiliaId == familiaId && s.UsadaEn == null && s.RevocadaEn == null && s.ExpiraEn > ahora,
            cancellationToken);

    public Task RevocarSesionesAsync(Guid cuentaId, DateTimeOffset ahora, CancellationToken cancellationToken) =>
        db.Sesiones
            .Where(s => s.CuentaId == cuentaId && s.RevocadaEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevocadaEn, ahora).SetProperty(x => x.UpdatedAt, ahora), cancellationToken);
}

internal sealed class RepositorioDocentes(AulaListaDbContext db) : IRepositorioDocentes
{
    public Task<Docente?> PorIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Docentes.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

    public void Agregar(Docente docente) => db.Docentes.Add(docente);

    public void AgregarAceptacion(AceptacionLegal aceptacion) => db.AceptacionesLegales.Add(aceptacion);

    public async Task<IReadOnlyList<AceptacionLegal>> AceptacionesAsync(Guid docenteId, CancellationToken cancellationToken) =>
        await db.AceptacionesLegales.AsNoTracking().Where(a => a.DocenteId == docenteId).ToListAsync(cancellationToken);

    public void AgregarExportacion(Exportacion exportacion) => db.Exportaciones.Add(exportacion);

    public Task<Exportacion?> ExportacionPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Exportaciones.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
}

/// <summary>Usa la función <c>eliminar_cuentas_vencidas</c>, que corre con permisos del dueño del esquema.</summary>
internal sealed class RetencionCuentas(AulaListaDbContext db) : IRetencionCuentas
{
    public async Task<int> EliminarVencidasAsync(DateTimeOffset limite, CancellationToken cancellationToken) =>
        await db.Database
            .SqlQuery<int>($"SELECT eliminar_cuentas_vencidas({limite}) AS \"Value\"")
            .SingleAsync(cancellationToken);
}
