using Aulisima.Application.Comun;
using Aulisima.Domain.Boletas;
using Aulisima.Domain.Cierre;
using Aulisima.Domain.Comun;
using Aulisima.Domain.Cuentas;
using Aulisima.Domain.Evaluacion;
using Aulisima.Domain.Grupos;
using Aulisima.Domain.Operacion;
using Aulisima.Domain.Planes;
using Aulisima.Domain.Tutores;
using FluentValidation;

namespace Aulisima.Api.Comun;

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
                || error == ErroresTutores.EnlaceInvalido
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
            _ when error == ErroresCierre.RequierePro || error == ErroresPlanes.BoletasRequierenPro
                || error == ErroresPlanes.WhatsappRequierePro || error == ErroresPlanes.ExcelRequierePro
                || error == ErroresPlanes.AvisosRequierenPro
                => StatusCodes.Status403Forbidden,
            _ when error == ErroresPlanes.PruebaUsada || error == ErroresPlanes.YaTieneTarjeta || error == ErroresPlanes.SinTarjeta
                || error == ErroresPlanes.SinCambios || error == ErroresPlanes.PrepagoVigente || error == ErroresPlanes.AgregarRequierePago
                || error == ErroresPlanes.PagoSimuladoTerminado
                => StatusCodes.Status409Conflict,
            _ when error == ErroresOperacion.TipoRubroRepetido || error == ErroresOperacion.ComportamientoEnUso
                => StatusCodes.Status409Conflict,
            _ when error == ErroresCatalogo.NombreRepetido || error == ErroresCatalogo.OtroActivo || error == ErroresCatalogo.NoEsBorrador
                || error == ErroresCatalogo.YaArchivado || error == ErroresCatalogo.SinCambiosPrecio
                => StatusCodes.Status409Conflict,
            _ when error == ErroresCatalogo.SinPlanALaVenta => StatusCodes.Status503ServiceUnavailable,
            _ when error == ErroresCierre.LimiteIa || error == ErroresGrupos.LimiteLecturas => StatusCodes.Status429TooManyRequests,
            _ when error == ErroresCierre.PeriodoSinCierre || error == ErroresCierre.ObservacionAprobada
                || error == ErroresCierre.ObservacionPublicada || error == ErroresCierre.EnGeneracion
                || error == ErroresCierre.LimiteRegeneraciones
                => StatusCodes.Status409Conflict,
            _ when error == ErroresBoletas.BoletaPublicada || error == ErroresBoletas.BoletaNoCorregible
                || error == ErroresBoletas.SinCambios || error == ErroresBoletas.BoletaPreparando
                || error == ErroresBoletas.ObservacionSinAprobar || error == ErroresBoletas.SinBoletasPorPublicar
                || error == ErroresBoletas.BoletasDesactualizadas || error == ErroresBoletas.SinBoletas
                => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity,
        };
        return error.AProblema(estado);
    }
}
