using AulaLista.Domain.Comun;

namespace AulaLista.Domain.Cuentas;

/// <summary>
/// Token de actualización rotativo. Cada rotación crea una sesión nueva de la misma familia
/// y marca la anterior como usada; usar una ya usada indica robo (SPEC-01, Sesión).
/// </summary>
public sealed class Sesion : Entidad
{
    public Sesion(
        Guid cuentaId,
        Guid familiaId,
        string hash,
        string metodo,
        DateTimeOffset autenticadaEn,
        DateTimeOffset expiraEn,
        DateTimeOffset ahora)
        : base(ahora)
    {
        CuentaId = cuentaId;
        FamiliaId = familiaId;
        Hash = hash;
        Metodo = metodo;
        AutenticadaEn = autenticadaEn;
        ExpiraEn = expiraEn;
    }

    private Sesion()
    {
    }

    public Guid CuentaId { get; private set; }

    public Guid FamiliaId { get; private set; }

    public string Hash { get; private set; } = string.Empty;

    /// <summary>Cómo se autenticó la familia: <c>contrasena</c> o <c>google</c>.</summary>
    public string Metodo { get; private set; } = string.Empty;

    public DateTimeOffset AutenticadaEn { get; private set; }

    public DateTimeOffset ExpiraEn { get; private set; }

    public DateTimeOffset? UsadaEn { get; private set; }

    public DateTimeOffset? RevocadaEn { get; private set; }

    public bool EstaVigente(DateTimeOffset ahora) => UsadaEn is null && RevocadaEn is null && ahora < ExpiraEn;

    public void MarcarUsada(DateTimeOffset ahora)
    {
        UsadaEn = ahora;
        MarcarActualizada(ahora);
    }

    public void Revocar(DateTimeOffset ahora)
    {
        RevocadaEn ??= ahora;
        MarcarActualizada(ahora);
    }
}

public static class MetodoAutenticacion
{
    public const string Contrasena = "contrasena";
    public const string Google = "google";
}
