using AulaLista.Api.Comun;
using AulaLista.Api.Tutores;
using AulaLista.Application.Avisos;
using AulaLista.Application.Tutores;
using Microsoft.AspNetCore.Mvc;
using static AulaLista.Api.Comun.Respuestas;

namespace AulaLista.Api.Avisos;

/// <summary>SPEC-07: avisos del docente, lo que el tutor ve y registra en el portal, y el webhook de WhatsApp.</summary>
internal static class AvisosEndpoints
{
    /// <summary>Un webhook de Meta trae unos cuantos estados; más que esto no es de Meta.</summary>
    private const int TamanoMaximoWebhook = 256 * 1024;

    public static RouteGroupBuilder MapAvisos(this RouteGroupBuilder api)
    {
        var grupos = api.MapGroup("/grupos").WithTags("Avisos");
        grupos.MapGet("/{id:guid}/avisos", Listar).WithName("ListarAvisos")
            .Produces<AvisosDto>().Produces(StatusCodes.Status404NotFound);
        grupos.MapGet("/{id:guid}/avisos/destinatarios", Destinatarios).WithName("ListarDestinatariosAviso")
            .Produces<DestinatariosDto>().Produces(StatusCodes.Status404NotFound);
        grupos.MapPost("/{id:guid}/avisos", Crear).WithName("CrearAviso")
            .Produces<AvisoDto>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var avisos = api.MapGroup("/avisos").WithTags("Avisos");
        avisos.MapGet("/{id:guid}", Detalle).WithName("ObtenerAviso")
            .Produces<AvisoDto>().Produces(StatusCodes.Status404NotFound);
        avisos.MapDelete("/{id:guid}", Borrar).WithName("BorrarAviso")
            .Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound);
        avisos.MapPost("/{id:guid}/entregas/{tutorId:guid}/compartir", CompartirAviso).WithName("CompartirAviso")
            .Produces<EnlaceDto>().Produces(StatusCodes.Status404NotFound);

        api.MapGroup("/boletas").WithTags("Boletas")
            .MapPost("/{id:guid}/tutores/{tutorId:guid}/compartir", CompartirBoleta).WithName("CompartirBoleta")
            .Produces<EnlaceDto>().Produces(StatusCodes.Status404NotFound);

        var portal = api.MapGroup("/tutor").WithTags("Portal del tutor").RequireAuthorization(AutenticacionTutor.Politica);
        portal.MapGet("/alumnos/{id:guid}/avisos", AvisosDelHijo).WithName("ListarAvisosHijoTutor")
            .Produces<IReadOnlyList<AvisoTutorDto>>().Produces(StatusCodes.Status404NotFound);
        portal.MapPost("/alumnos/{id:guid}/avisos/leidos", Leer).WithName("LeerAvisosHijoTutor")
            .Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound);
        portal.MapGet("/contacto", Contacto).WithName("ObtenerContactoTutor").Produces<ContactoTutorDto>();
        portal.MapPut("/contacto", GuardarCorreo).WithName("GuardarCorreoTutor")
            .Produces<ContactoTutorDto>().ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        portal.MapPost("/push", Suscribir).WithName("SuscribirPushTutor")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        portal.MapDelete("/push", Desuscribir).WithName("DesuscribirPushTutor").Produces(StatusCodes.Status204NoContent);

        var webhook = api.MapGroup("/webhooks").WithTags("Webhooks").AllowAnonymous();
        webhook.MapGet("/whatsapp", VerificarWebhook).WithName("VerificarWebhookWhatsapp").ExcludeFromDescription();
        webhook.MapPost("/whatsapp", RecibirWebhook).WithName("RecibirWebhookWhatsapp").ExcludeFromDescription();
        return api;
    }

    private static async Task<IResult> Listar(Guid id, AvisosDelGrupoCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, ct) is { } avisos ? TypedResults.Ok(avisos) : TypedResults.NotFound();

    private static async Task<IResult> Destinatarios(Guid id, DestinatariosAvisoCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, ct) is { } destinatarios ? TypedResults.Ok(destinatarios) : TypedResults.NotFound();

    private static async Task<IResult> Crear(Guid id, CrearAviso cuerpo, CrearAvisoCasoUso caso, CancellationToken ct) =>
        Creado(await caso.EjecutarAsync(id, cuerpo, ct), a => $"/api/v1/avisos/{a.Id}");

    private static async Task<IResult> Detalle(Guid id, DetalleAvisoCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, ct) is { } aviso ? TypedResults.Ok(aviso) : TypedResults.NotFound();

    private static async Task<IResult> Borrar(Guid id, BorrarAvisoCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, ct) ? TypedResults.NoContent() : TypedResults.NotFound();

    private static async Task<IResult> CompartirAviso(Guid id, Guid tutorId, CompartirAvisoCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, tutorId, ct) is { } enlace ? TypedResults.Ok(enlace) : TypedResults.NotFound();

    private static async Task<IResult> CompartirBoleta(Guid id, Guid tutorId, CompartirBoletaCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, tutorId, ct) is { } enlace ? TypedResults.Ok(enlace) : TypedResults.NotFound();

    private static async Task<IResult> AvisosDelHijo(Guid id, AvisosDelHijoCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, ct) is { } avisos ? TypedResults.Ok(avisos) : TypedResults.NotFound();

    private static async Task<IResult> Leer(Guid id, LeerAvisosDelHijoCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(id, ct) ? TypedResults.NoContent() : TypedResults.NotFound();

    private static async Task<IResult> Contacto(ContactoTutorCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(ct) is { } contacto ? TypedResults.Ok(contacto) : TypedResults.NotFound();

    private static async Task<IResult> GuardarCorreo(GuardarCorreoTutor cuerpo, GuardarCorreoTutorCasoUso caso, CancellationToken ct) =>
        Respuesta(await caso.EjecutarAsync(cuerpo, ct));

    private static async Task<IResult> Suscribir(SuscribirPush cuerpo, SuscribirPushCasoUso caso, CancellationToken ct) =>
        SinContenido(await caso.EjecutarAsync(cuerpo, ct));

    private static async Task<IResult> Desuscribir([FromBody] DesuscribirPush cuerpo, DesuscribirPushCasoUso caso, CancellationToken ct) =>
        await caso.EjecutarAsync(cuerpo, ct) ? TypedResults.NoContent() : TypedResults.NotFound();

    /// <summary>Meta confirma el webhook con <c>hub.mode</c>, <c>hub.verify_token</c> y <c>hub.challenge</c>.</summary>
    private static IResult VerificarWebhook(HttpRequest solicitud, WebhookWhatsappCasoUso caso) =>
        caso.Verificar(solicitud.Query["hub.mode"], solicitud.Query["hub.verify_token"], solicitud.Query["hub.challenge"]) is { } reto
            ? TypedResults.Text(reto)
            : TypedResults.Unauthorized();

    /// <summary>La firma se calcula sobre el cuerpo tal cual llegó, así que se lee en bruto.</summary>
    private static async Task<IResult> RecibirWebhook(HttpRequest solicitud, WebhookWhatsappCasoUso caso, CancellationToken ct)
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

        var firma = solicitud.Headers["X-Hub-Signature-256"].ToString();
        return await caso.EjecutarAsync(memoria.ToArray(), firma, ct) is null ? TypedResults.Unauthorized() : TypedResults.Ok();
    }
}
