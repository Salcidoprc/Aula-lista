namespace AulaLista.Application.Comun;

/// <summary>
/// Catálogo único de códigos de error expuestos por la API (propiedad <c>codigo</c> de Problem Details).
/// Cada módulo agrega aquí sus códigos; el OpenAPI los lista.
/// </summary>
public static class CodigosError
{
    public const string NoEncontrado = "no_encontrado";
    public const string NoAutenticado = "no_autenticado";
    public const string ValidacionFallida = "validacion_fallida";
    public const string ErrorInterno = "error_interno";

    public static readonly IReadOnlyList<string> Todos =
    [
        NoEncontrado,
        NoAutenticado,
        ValidacionFallida,
        ErrorInterno,
    ];
}
