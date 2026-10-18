using AulaLista.Domain.Avisos;
using AulaLista.Domain.Boletas;
using AulaLista.Domain.Cierre;
using AulaLista.Domain.Cuentas;
using AulaLista.Domain.Evaluacion;
using AulaLista.Domain.Grupos;
using AulaLista.Domain.Planes;
using AulaLista.Domain.Tutores;

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
        .. ErroresCuentas.Todos.Select(e => e.Codigo),
        .. ErroresGrupos.Todos.Select(e => e.Codigo),
        .. ErroresEvaluacion.Todos.Select(e => e.Codigo),
        .. ErroresCierre.Todos.Select(e => e.Codigo),
        .. ErroresBoletas.Todos.Select(e => e.Codigo),
        .. ErroresTutores.Todos.Select(e => e.Codigo),
        .. ErroresAvisos.Todos.Select(e => e.Codigo),
        .. ErroresPlanes.Todos.Select(e => e.Codigo),
    ];
}
