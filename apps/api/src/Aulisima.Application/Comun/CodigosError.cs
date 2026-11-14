using Aulisima.Domain.Avisos;
using Aulisima.Domain.Boletas;
using Aulisima.Domain.Cierre;
using Aulisima.Domain.Cuentas;
using Aulisima.Domain.Evaluacion;
using Aulisima.Domain.Grupos;
using Aulisima.Domain.Operacion;
using Aulisima.Domain.Planes;
using Aulisima.Domain.Tutores;

namespace Aulisima.Application.Comun;

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
        .. ErroresCatalogo.Todos.Select(e => e.Codigo),
        .. ErroresOperacion.Todos.Select(e => e.Codigo),
    ];
}
