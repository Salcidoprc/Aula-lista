using AulaLista.Application.Comun;
using AulaLista.Domain.Cuentas;
using Microsoft.Extensions.Options;

namespace AulaLista.Application.Cuentas;

/// <summary>Resultado de iniciar o renovar una sesión: token de acceso y token de actualización.</summary>
public sealed record SesionEmitida(
    Guid DocenteId,
    string TokenAcceso,
    DateTimeOffset AccesoExpiraEn,
    string TokenActualizacion,
    DateTimeOffset ActualizacionExpiraEn);

/// <summary>Crea la sesión (token de actualización) y el token de acceso de una cuenta.</summary>
public sealed class EmisorSesiones(
    IRepositorioCuentas cuentas,
    IEmisorTokensAcceso emisorAcceso,
    IOptions<OpcionesCuentas> opciones,
    TimeProvider reloj)
{
    public SesionEmitida Nueva(Cuenta cuenta, string metodo)
    {
        var ahora = reloj.GetUtcNow();
        return Emitir(cuenta.Id, Guid.CreateVersion7(ahora), metodo, ahora, ahora);
    }

    public SesionEmitida Rotar(Sesion anterior)
    {
        var ahora = reloj.GetUtcNow();
        anterior.MarcarUsada(ahora);
        return Emitir(anterior.CuentaId, anterior.FamiliaId, anterior.Metodo, anterior.AutenticadaEn, ahora);
    }

    private SesionEmitida Emitir(Guid cuentaId, Guid familiaId, string metodo, DateTimeOffset autenticadaEn, DateTimeOffset ahora)
    {
        // La sesión no expira mientras el docente use la app al menos una vez cada 30 días (SPEC-01, Sesión).
        var expira = ahora.AddDays(opciones.Value.DiasSesion);
        var (token, hash) = TokensSeguros.Generar();
        cuentas.AgregarSesion(new Sesion(cuentaId, familiaId, hash, metodo, autenticadaEn, expira, ahora));
        var acceso = emisorAcceso.Emitir(cuentaId, familiaId);
        return new SesionEmitida(cuentaId, acceso.Token, acceso.ExpiraEn, token, expira);
    }
}
