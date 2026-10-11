using AulaLista.Application.Comun;
using AulaLista.Domain.Comun;

namespace AulaLista.Api.Comun;

internal static class ProblemDetailsConCodigo
{
    public const string PropiedadCodigo = "codigo";

    /// <summary>Agrega un <c>codigo</c> estable a toda respuesta de error, incluidas las del framework.</summary>
    public static IServiceCollection AddProblemDetailsConCodigo(this IServiceCollection services) =>
        services.AddProblemDetails(opciones => opciones.CustomizeProblemDetails = ctx =>
        {
            ctx.ProblemDetails.Extensions.TryAdd(PropiedadCodigo, ctx.ProblemDetails.Status switch
            {
                StatusCodes.Status401Unauthorized => CodigosError.NoAutenticado,
                StatusCodes.Status404NotFound => CodigosError.NoEncontrado,
                StatusCodes.Status400BadRequest => CodigosError.ValidacionFallida,
                _ => CodigosError.ErrorInterno,
            });
        });

    /// <summary>Convierte un error de negocio en Problem Details.</summary>
    public static IResult AProblema(this ErrorNegocio error, int status = StatusCodes.Status422UnprocessableEntity) =>
        Results.Problem(
            title: error.Mensaje,
            statusCode: status,
            extensions: new Dictionary<string, object?> { [PropiedadCodigo] = error.Codigo });
}
