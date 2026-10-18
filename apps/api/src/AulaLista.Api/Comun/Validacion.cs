using AulaLista.Application.Comun;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Cuentas;
using AulaLista.Domain.Evaluacion;
using AulaLista.Domain.Grupos;
using FluentValidation;

namespace AulaLista.Api.Comun;

internal static class Validacion
{
    /// <summary>Valida la entrada en el borde (reglas/01) y responde 400 con <c>validacion_fallida</c>.</summary>
    public static async Task<IResult?> ValidarAsync<T>(this IValidator<T> validador, T entrada, CancellationToken ct)
    {
        var resultado = await validador.ValidateAsync(entrada, ct);
        if (resultado.IsValid)
        {
            return null;
        }

        return TypedResults.ValidationProblem(
            resultado.ToDictionary(),
            title: "Hay datos que corregir.",
            extensions: new Dictionary<string, object?> { [ProblemDetailsConCodigo.PropiedadCodigo] = CodigosError.ValidacionFallida });
    }

    /// <summary>Estado HTTP de cada error de negocio; el resto responde 422.</summary>
    public static IResult AProblemaHttp(this ErrorNegocio error)
    {
        var estado = error.Codigo switch
        {
            _ when error == ErroresCuentas.CredencialesInvalidas || error == ErroresCuentas.SesionInvalida
                => StatusCodes.Status401Unauthorized,
            _ when error == ErroresCuentas.DemasiadosIntentos => StatusCodes.Status429TooManyRequests,
            _ when error == ErroresCuentas.CorreoRegistrado => StatusCodes.Status409Conflict,
            _ when error == ErroresCuentas.ReautenticacionRequerida || error == ErroresGrupos.CorreoNoVerificado
                => StatusCodes.Status403Forbidden,
            _ when error == ErroresCuentas.ExportacionNoDisponible => StatusCodes.Status404NotFound,
            _ when error == ErroresGrupos.CicloDuplicado || error == ErroresGrupos.GrupoDuplicado
                || error == ErroresGrupos.TutorYaVinculado || error == ErroresGrupos.ImportacionConfirmada
                || error == ErroresGrupos.GrupoArchivado || error == ErroresGrupos.UnidadCerrada
                || error == ErroresGrupos.UnidadConDatos || error == ErroresGrupos.TomasFueraDeUnidad
                => StatusCodes.Status409Conflict,
            _ when error == ErroresEvaluacion.BorradoRequiereConfirmacion || error == ErroresEvaluacion.PeriodoEnCierre
                || error == ErroresEvaluacion.PeriodoCerrado || error == ErroresEvaluacion.PeriodoConRubros
                || error == ErroresEvaluacion.GrupoConCalificaciones || error == ErroresEvaluacion.FormaConCalificaciones
                => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity,
        };
        return error.AProblema(estado);
    }
}
