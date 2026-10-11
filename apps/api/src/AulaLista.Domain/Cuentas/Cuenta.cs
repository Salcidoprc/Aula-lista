using AulaLista.Domain.Comun;

namespace AulaLista.Domain.Cuentas;

/// <summary>
/// Credenciales de acceso del docente. El <see cref="Entidad.Id"/> es también el id del docente.
/// Se consulta antes de saber quién es el usuario, por eso no lleva RLS (ADR-0013).
/// </summary>
public sealed class Cuenta : Entidad
{
    private Cuenta(string correo, DateTimeOffset ahora)
        : base(ahora)
    {
        Correo = NormalizarCorreo(correo);
        Estado = EstadoCuenta.Activa;
    }

    private Cuenta()
    {
    }

    public string Correo { get; private set; } = string.Empty;

    public string? HashContrasena { get; private set; }

    public DateTimeOffset? CorreoVerificadoEn { get; private set; }

    public string? GoogleSub { get; private set; }

    public string Estado { get; private set; } = EstadoCuenta.Activa;

    public DateTimeOffset? EliminacionSolicitadaEn { get; private set; }

    public bool EstaActiva => Estado == EstadoCuenta.Activa;

    public bool CorreoVerificado => CorreoVerificadoEn is not null;

    public static Cuenta ConContrasena(string correo, string hashContrasena, DateTimeOffset ahora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hashContrasena);
        return new Cuenta(correo, ahora) { HashContrasena = hashContrasena };
    }

    /// <summary>Google ya verificó el correo, así que la cuenta nace verificada.</summary>
    public static Cuenta ConGoogle(string correo, string googleSub, DateTimeOffset ahora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(googleSub);
        return new Cuenta(correo, ahora) { GoogleSub = googleSub, CorreoVerificadoEn = ahora };
    }

    public static string NormalizarCorreo(string correo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correo);
        return correo.Trim().ToLowerInvariant();
    }

    public void VerificarCorreo(DateTimeOffset ahora)
    {
        CorreoVerificadoEn ??= ahora;
        MarcarActualizada(ahora);
    }

    public void CambiarContrasena(string hashContrasena, DateTimeOffset ahora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hashContrasena);
        HashContrasena = hashContrasena;
        MarcarActualizada(ahora);
    }

    // RN-005: Google y correo con la misma dirección son la misma cuenta.
    public void VincularGoogle(string googleSub, DateTimeOffset ahora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(googleSub);
        GoogleSub = googleSub;
        VerificarCorreo(ahora);
    }

    // RN-006: se desactiva de inmediato; los datos se borran a los 30 días.
    public void SolicitarEliminacion(DateTimeOffset ahora)
    {
        Estado = EstadoCuenta.EliminacionSolicitada;
        EliminacionSolicitadaEn = ahora;
        MarcarActualizada(ahora);
    }
}

public static class EstadoCuenta
{
    public const string Activa = "activa";
    public const string EliminacionSolicitada = "eliminacion_solicitada";
}
