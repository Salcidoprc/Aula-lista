using AulaLista.Application.Planes;
using static AulaLista.Api.Comun.Respuestas;

namespace AulaLista.Api.Planes;

/// <summary>SPEC-08: planes, suscripción del docente, Checkout, portal y webhook de pagos.</summary>
internal static class PlanesEndpoints
{
    /// <summary>Un evento de Stripe pesa unos cuantos KB; más que esto no es de Stripe.</summary>
    private const int TamanoMaximoWebhook = 512 * 1024;

    public static RouteGroupBuilder MapPlanes(this RouteGroupBuilder api, IConfiguration configuracion)
    {
        api.MapGet("/planes", (ObtenerPlanesCasoUso caso) => TypedResults.Ok(caso.Ejecutar())).WithTags("Planes").WithName("ObtenerPlanes")
            .AllowAnonymous();

        var suscripcion = api.MapGroup("/docente/suscripcion").WithTags("Planes");
        suscripcion.MapGet("/", Obtener).WithName("ObtenerSuscripcion").Produces<SuscripcionDto>();
        suscripcion.MapPost("/checkout", Checkout).WithName("CrearCheckout")
            .Produces<CheckoutDto>().ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        suscripcion.MapPut("/grupos", CambiarGrupos).WithName("CambiarGruposPro")
            .Produces<SuscripcionDto>().ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        suscripcion.MapPost("/portal", Portal).WithName("PortalPagos")
            .Produces<PortalDto>().ProducesProblem(StatusCodes.Status409Conflict);

        api.MapPost("/webhooks/pagos", Webhook).WithTags("Webhooks").WithName("RecibirWebhookPagos").AllowAnonymous().ExcludeFromDescription();

        // La página "Pago de prueba" solo existe sin Stripe.
        if ((configuracion[$"{OpcionesPagos.Seccion}:{nameof(OpcionesPagos.Proveedor)}"] ?? OpcionesPagos.ProveedorSimulado)
            == OpcionesPagos.ProveedorSimulado)
        {
            var simulados = api.MapGroup("/pagos-simulados").WithTags("Planes");
            simulados.MapGet("/{id}", ObtenerSimulado).WithName("ObtenerPagoSimulado")
                .Produces<SesionSimuladaDto>().Produces(StatusCodes.Status404NotFound);
            simulados.MapPost("/{id}/pagar", (string id, PagoSimuladoCasoUso caso, CancellationToken ct) => TerminarAsync(id, true, caso, ct))
                .WithName("PagarPagoSimulado").Produces<SesionSimuladaDto>().ProducesProblem(StatusCodes.Status409Conflict);
            simulados.MapPost("/{id}/rechazar", (string id, PagoSimuladoCasoUso caso, CancellationToken ct) => TerminarAsync(id, false, caso, ct))
                .WithName("RechazarPagoSimulado").Produces<SesionSimuladaDto>().ProducesProblem(StatusCodes.Status409Conflict);
            simulados.MapPost("/renovacion", Renovar).WithName("RenovarPagoSimulado")
                .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status409Conflict);
        }

        return api;
    }

    private static async Task<IResult> Obtener(ObtenerSuscripcionCasoUso caso, CancellationToken ct) =>
        TypedResults.Ok(await caso.EjecutarAsync(ct));

    private static async Task<IResult> Checkout(CrearCheckout cuerpo, CrearCheckoutCasoUso caso, CancellationToken ct) =>
        Respuesta(await caso.EjecutarAsync(cuerpo, ct));

    private static async Task<IResult> CambiarGrupos(CambiarGruposPro cuerpo, CambiarGruposProCasoUso caso, CancellationToken ct) =>
        Respuesta(await caso.EjecutarAsync(cuerpo, ct));

    private static async Task<IResult> Portal(PortalPagosCasoUso caso, CancellationToken ct) => Respuesta(await caso.EjecutarAsync(ct));

    private static async Task<IResult> ObtenerSimulado(string id, PagoSimuladoCasoUso caso, CancellationToken ct) =>
        await caso.ObtenerAsync(id, ct) is { } sesion ? TypedResults.Ok(sesion) : TypedResults.NotFound();

    private static async Task<IResult> TerminarAsync(string id, bool pagar, PagoSimuladoCasoUso caso, CancellationToken ct) =>
        Respuesta(await caso.TerminarAsync(id, pagar, ct));

    private static async Task<IResult> Renovar(RenovacionSimulada cuerpo, PagoSimuladoCasoUso caso, CancellationToken ct) =>
        SinContenido(await caso.RenovarAsync(cuerpo, ct));

    /// <summary>La firma se calcula sobre el cuerpo tal cual llegó, así que se lee en bruto.</summary>
    private static async Task<IResult> Webhook(HttpRequest solicitud, WebhookPagosCasoUso caso, CancellationToken ct)
    {
        if (solicitud.ContentLength > TamanoMaximoWebhook)
        {
            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        using var memoria = new MemoryStream();
        await solicitud.Body.CopyToAsync(memoria, ct);
        if (memoria.Length > TamanoMaximoWebhook)
        {
            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var firma = solicitud.Headers[FirmaPagos.Encabezado].ToString();
        return await caso.EjecutarAsync(memoria.ToArray(), firma, ct) is null ? TypedResults.Unauthorized() : TypedResults.Ok();
    }
}
