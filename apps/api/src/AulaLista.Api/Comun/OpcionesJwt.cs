using System.ComponentModel.DataAnnotations;

namespace AulaLista.Api.Comun;

/// <summary>Configuración de los tokens de acceso. La clave llega solo por variable de entorno (Jwt__ClaveFirma).</summary>
public sealed class OpcionesJwt
{
    public const string Seccion = "Jwt";

    [Required]
    public string Emisor { get; init; } = string.Empty;

    [Required]
    public string Audiencia { get; init; } = string.Empty;

    [Required]
    [MinLength(32)]
    public string ClaveFirma { get; init; } = string.Empty;
}
