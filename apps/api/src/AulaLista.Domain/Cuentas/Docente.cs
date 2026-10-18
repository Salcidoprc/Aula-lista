using AulaLista.Domain.Comun;

namespace AulaLista.Domain.Cuentas;

/// <summary>Perfil del docente (RF-004). Comparte id con su <see cref="Cuenta"/>.</summary>
public sealed class Docente
{
    public const string ZonaHorariaPorDefecto = "America/Mexico_City";

    public Docente(Guid id, DateTimeOffset ahora)
    {
        Id = id;
        CreatedAt = ahora;
        UpdatedAt = ahora;
    }

    private Docente()
    {
    }

    public Guid Id { get; private set; }

    public string? Nombre { get; private set; }

    public string? Nivel { get; private set; }

    public string? EscuelaNombre { get; private set; }

    public string? EscuelaCct { get; private set; }

    public string? EntidadFederativa { get; private set; }

    public string ZonaHoraria { get; private set; } = ZonaHorariaPorDefecto;

    public bool PruebaProUsada { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>RN-081: la prueba Pro se usa una sola vez.</summary>
    public void MarcarPruebaUsada(DateTimeOffset ahora)
    {
        PruebaProUsada = true;
        UpdatedAt = ahora;
    }

    public bool PerfilCompleto =>
        !string.IsNullOrWhiteSpace(Nombre)
        && Nivel is not null
        && !string.IsNullOrWhiteSpace(EscuelaNombre)
        && EntidadFederativa is not null;

    public Resultado ActualizarPerfil(
        string nombre,
        string nivel,
        string escuelaNombre,
        string? escuelaCct,
        string entidadFederativa,
        DateTimeOffset ahora)
    {
        if (!NivelEducativo.Todos.Contains(nivel))
        {
            return Resultado.Fallo(ErroresCuentas.NivelInvalido);
        }

        if (!EntidadesFederativas.Todas.Contains(entidadFederativa))
        {
            return Resultado.Fallo(ErroresCuentas.EntidadFederativaInvalida);
        }

        var cct = string.IsNullOrWhiteSpace(escuelaCct) ? null : escuelaCct.Trim().ToUpperInvariant();
        if (cct is not null && !ClaveCentroTrabajo.EsValida(cct))
        {
            return Resultado.Fallo(ErroresCuentas.CctInvalida);
        }

        Nombre = nombre.Trim();
        Nivel = nivel;
        EscuelaNombre = escuelaNombre.Trim();
        EscuelaCct = cct;
        EntidadFederativa = entidadFederativa;
        UpdatedAt = ahora;
        return Resultado.Exito();
    }
}

public static class NivelEducativo
{
    public const string Primaria = "primaria";
    public const string Secundaria = "secundaria";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal) { Primaria, Secundaria };
}

/// <summary>Clave de Centro de Trabajo de la SEP: 2 dígitos, 3 letras, 4 dígitos y 1 letra (ej. 25DPR0123X).</summary>
public static class ClaveCentroTrabajo
{
    public static bool EsValida(string cct) =>
        cct.Length == 10
        && char.IsAsciiDigit(cct[0]) && char.IsAsciiDigit(cct[1])
        && cct[2..5].All(char.IsAsciiLetterUpper)
        && cct[5..9].All(char.IsAsciiDigit)
        && char.IsAsciiLetterUpper(cct[9]);
}

/// <summary>Las 32 entidades federativas, en claves estables para la API.</summary>
public static class EntidadesFederativas
{
    public static readonly IReadOnlySet<string> Todas = new HashSet<string>(StringComparer.Ordinal)
    {
        "aguascalientes", "baja_california", "baja_california_sur", "campeche", "chiapas", "chihuahua",
        "ciudad_de_mexico", "coahuila", "colima", "durango", "estado_de_mexico", "guanajuato", "guerrero",
        "hidalgo", "jalisco", "michoacan", "morelos", "nayarit", "nuevo_leon", "oaxaca", "puebla",
        "queretaro", "quintana_roo", "san_luis_potosi", "sinaloa", "sonora", "tabasco", "tamaulipas",
        "tlaxcala", "veracruz", "yucatan", "zacatecas",
    };
}
