using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AulaLista.Application.Avisos;
using AulaLista.Domain.Avisos;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AulaLista.Infrastructure.Avisos;

/// <summary>
/// La notificación en el formato que entiende el service worker de Angular: al tocarla abre (o enfoca) el portal en
/// la URL del aviso.
/// </summary>
internal static class CargaPush
{
    public static string Json(MensajePush mensaje) => JsonSerializer.Serialize(new
    {
        notification = new
        {
            title = mensaje.Titulo,
            body = mensaje.Cuerpo,
            icon = "/icons/icon-192x192.png",
            badge = "/icons/icon-192x192.png",
            lang = "es-MX",
            data = new
            {
                url = mensaje.Url,
                onActionClick = new { @default = new { operation = "navigateLastFocusedOrOpen", url = mensaje.Url } },
            },
        },
    });
}

/// <summary>Web Push con VAPID (RF-064). 404 y 410 del servicio significan que el navegador quitó el permiso.</summary>
internal sealed partial class CanalPushVapid(HttpClient http, IOptions<OpcionesPush> opciones, ILogger<CanalPushVapid> registro) : ICanalPush
{
    private readonly PushServiceClient _cliente = new(http)
    {
        DefaultAuthentication = new VapidAuthentication(opciones.Value.ClavePublica!, opciones.Value.ClavePrivada!)
        {
            Subject = opciones.Value.Sujeto,
        },
    };

    public string ClavePublica => opciones.Value.ClavePublica!;

    public async Task<ResultadoPush> EnviarAsync(SuscripcionPush suscripcion, MensajePush mensaje, CancellationToken cancellationToken)
    {
        var destino = new PushSubscription
        {
            Endpoint = suscripcion.Endpoint,
            Keys = new Dictionary<string, string> { ["p256dh"] = suscripcion.P256dh, ["auth"] = suscripcion.Auth },
        };
        try
        {
            await _cliente.RequestPushMessageDeliveryAsync(
                destino, new PushMessage(CargaPush.Json(mensaje)) { TimeToLive = 60 * 60 * 24 * 7, Urgency = PushMessageUrgency.Normal },
                cancellationToken);
            return ResultadoPush.Enviado;
        }
        catch (PushServiceClientException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return ResultadoPush.Vencida;
        }
        catch (Exception ex) when (ex is PushServiceClientException or HttpRequestException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            // Sin el endpoint: identifica al navegador del tutor.
            FalloPush(registro, ex.GetType().Name);
            return ResultadoPush.Fallido;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No salió una notificación push ({Tipo})")]
    private static partial void FalloPush(ILogger registro, string tipo);
}

/// <summary>
/// Push simulado hasta tener claves VAPID: genera un par de claves al arrancar (para que el navegador pueda
/// suscribirse) y guarda en memoria lo que habría enviado. No registra nada en bitácoras.
/// </summary>
public sealed class CanalPushSimulado : ICanalPush
{
    private readonly ConcurrentQueue<(Guid TutorId, MensajePush Mensaje)> _enviados = new();

    public CanalPushSimulado()
    {
        using var clave = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var punto = clave.ExportParameters(false).Q;
        ClavePublica = Base64Url([0x04, .. punto.X!, .. punto.Y!]);
    }

    public string ClavePublica { get; }

    public IReadOnlyList<(Guid TutorId, MensajePush Mensaje)> Enviados => [.. _enviados];

    public Task<ResultadoPush> EnviarAsync(SuscripcionPush suscripcion, MensajePush mensaje, CancellationToken cancellationToken)
    {
        _enviados.Enqueue((suscripcion.TutorId, mensaje));
        return Task.FromResult(ResultadoPush.Enviado);
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>
/// Cloud API de Meta con plantillas de utilidad (RN-074). Solo viajan los parámetros de la plantilla: nombre del tutor,
/// grupo o unidad y el enlace. El teléfono nunca se registra.
/// </summary>
internal sealed partial class CanalWhatsappMeta(HttpClient http, IOptions<OpcionesWhatsapp> opciones, ILogger<CanalWhatsappMeta> registro)
    : ICanalWhatsapp
{
    public async Task<string?> EnviarPlantillaAsync(
        string telefonoE164, string plantilla, IReadOnlyList<string> parametros, CancellationToken cancellationToken)
    {
        var o = opciones.Value;
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, $"{o.VersionApi}/{o.IdNumero}/messages")
        {
            Content = JsonContent.Create(new
            {
                messaging_product = "whatsapp",
                to = telefonoE164.TrimStart('+'),
                type = "template",
                template = new
                {
                    name = plantilla,
                    language = new { code = o.Idioma },
                    components = new[]
                    {
                        new { type = "body", parameters = parametros.Select(p => new { type = "text", text = p }).ToArray() },
                    },
                },
            }),
        };
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.Token);
        try
        {
            using var respuesta = await http.SendAsync(solicitud, cancellationToken);
            if (!respuesta.IsSuccessStatusCode)
            {
                FalloWhatsapp(registro, plantilla, (int)respuesta.StatusCode);
                return null;
            }

            var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaMeta>(cancellationToken);
            return cuerpo?.Messages is [var primero, ..] ? primero.Id : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !cancellationToken.IsCancellationRequested)
        {
            FalloWhatsapp(registro, plantilla, 0);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Meta rechazó un mensaje de la plantilla {Plantilla} ({Estado})")]
    private static partial void FalloWhatsapp(ILogger registro, string plantilla, int estado);

    private sealed record RespuestaMeta([property: JsonPropertyName("messages")] IReadOnlyList<MensajeMeta>? Messages);

    private sealed record MensajeMeta([property: JsonPropertyName("id")] string Id);
}

/// <summary>WhatsApp simulado hasta tener credenciales de Meta: guarda en memoria lo que habría salido.</summary>
public sealed class CanalWhatsappSimulado : ICanalWhatsapp
{
    private readonly ConcurrentQueue<MensajeSimulado> _enviados = new();

    public IReadOnlyList<MensajeSimulado> Enviados => [.. _enviados];

    public Task<string?> EnviarPlantillaAsync(
        string telefonoE164, string plantilla, IReadOnlyList<string> parametros, CancellationToken cancellationToken)
    {
        var id = $"wamid.simulado.{Guid.NewGuid():N}";
        _enviados.Enqueue(new MensajeSimulado(id, telefonoE164, plantilla, parametros));
        return Task.FromResult<string?>(id);
    }
}

public sealed record MensajeSimulado(string IdMeta, string TelefonoE164, string Plantilla, IReadOnlyList<string> Parametros);
