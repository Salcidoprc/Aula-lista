using System.ComponentModel.DataAnnotations;

namespace AulaLista.Application.Cuentas;

/// <summary>Vigencias, límites y versiones legales vigentes. Viven en configuración (reglas/01).</summary>
public sealed class OpcionesCuentas
{
    public const string Seccion = "Cuentas";

    [Required]
    [Url]
    public string UrlWeb { get; init; } = string.Empty;

    [Range(1, 60)]
    public int MinutosTokenAcceso { get; init; } = 15;

    [Range(1, 90)]
    public int DiasSesion { get; init; } = 30;

    [Range(1, 1000)]
    public int IntentosInicioSesionPorMinuto { get; init; } = 5;

    [Range(1, 20)]
    public int ReenviosVerificacionPorHora { get; init; } = 3;

    [Range(1, 30)]
    public int MinutosReautenticacionGoogle { get; init; } = 10;

    [Range(1, 168)]
    public int HorasTokenVerificacion { get; init; } = 24;

    [Range(5, 1440)]
    public int MinutosTokenRecuperacion { get; init; } = 60;

    [Range(1, 20)]
    public int RecuperacionesPorHora { get; init; } = 3;

    [Range(1, 168)]
    public int HorasExportacion { get; init; } = 24;

    /// <summary>RN-006: días entre la solicitud de eliminación y el borrado definitivo.</summary>
    [Range(1, 90)]
    public int DiasParaBorrarCuenta { get; init; } = 30;

    [Required]
    public VersionesLegales VersionesLegales { get; init; } = new();
}

public sealed class VersionesLegales
{
    [Required]
    public string Terminos { get; init; } = string.Empty;

    [Required]
    public string Privacidad { get; init; } = string.Empty;

    [Required]
    public string DeclaracionDatos { get; init; } = string.Empty;

    public string? Vigente(string documento) => documento switch
    {
        Domain.Cuentas.DocumentoLegal.Terminos => Terminos,
        Domain.Cuentas.DocumentoLegal.Privacidad => Privacidad,
        Domain.Cuentas.DocumentoLegal.DeclaracionDatos => DeclaracionDatos,
        _ => null,
    };
}
