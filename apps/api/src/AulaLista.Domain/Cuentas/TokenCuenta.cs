using AulaLista.Domain.Comun;

namespace AulaLista.Domain.Cuentas;

/// <summary>Token de un solo uso enviado por correo. Solo se guarda su hash SHA-256.</summary>
public sealed class TokenCuenta : Entidad
{
    public TokenCuenta(Guid cuentaId, string proposito, string hash, DateTimeOffset expiraEn, DateTimeOffset ahora)
        : base(ahora)
    {
        CuentaId = cuentaId;
        Proposito = proposito;
        Hash = hash;
        ExpiraEn = expiraEn;
    }

    private TokenCuenta()
    {
    }

    public Guid CuentaId { get; private set; }

    public string Proposito { get; private set; } = string.Empty;

    public string Hash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiraEn { get; private set; }

    public DateTimeOffset? UsadoEn { get; private set; }

    public Resultado Usar(DateTimeOffset ahora)
    {
        if (UsadoEn is not null)
        {
            return Resultado.Fallo(ErroresCuentas.TokenInvalido);
        }

        if (ahora >= ExpiraEn)
        {
            return Resultado.Fallo(ErroresCuentas.TokenVencido);
        }

        UsadoEn = ahora;
        MarcarActualizada(ahora);
        return Resultado.Exito();
    }
}

public static class PropositoToken
{
    public const string VerificarCorreo = "verificar_correo";
    public const string RestablecerContrasena = "restablecer_contrasena";
}
