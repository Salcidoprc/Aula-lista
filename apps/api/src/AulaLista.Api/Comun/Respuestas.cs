using AulaLista.Domain.Comun;

namespace AulaLista.Api.Comun;

/// <summary>Convierte el resultado de un caso de uso en HTTP: nulo es 404 (también un recurso ajeno) y un error, su estado.</summary>
internal static class Respuestas
{
    public static IResult Respuesta<T>(Resultado<T>? resultado) => resultado switch
    {
        null => TypedResults.NotFound(),
        { EsExito: true } => TypedResults.Ok(resultado.Valor),
        _ => resultado.Error.AProblemaHttp(),
    };

    public static IResult Creado<T>(Resultado<T>? resultado, Func<T, string> ubicacion) => resultado switch
    {
        null => TypedResults.NotFound(),
        { EsExito: true } => TypedResults.Created(ubicacion(resultado.Valor), resultado.Valor),
        _ => resultado.Error.AProblemaHttp(),
    };

    public static IResult SinContenido(Resultado? resultado) => resultado switch
    {
        null => TypedResults.NotFound(),
        { EsExito: true } => TypedResults.NoContent(),
        _ => resultado.Error.AProblemaHttp(),
    };
}
