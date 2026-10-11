using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using AulaLista.Application.Comun;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AulaLista.Infrastructure.Comun;

public sealed class OpcionesCorreo
{
    public const string Seccion = "Correo";
    public const string ProveedorBrevo = "brevo";
    public const string ProveedorBuzon = "buzon";

    /// <summary><c>brevo</c> en staging y producción; <c>buzon</c> (en memoria) en local y pruebas.</summary>
    [Required]
    public string Proveedor { get; init; } = ProveedorBuzon;

    [Required]
    [EmailAddress]
    public string Remitente { get; init; } = "no-responder@aulalista.mx";

    [Required]
    public string NombreRemitente { get; init; } = "Aula Lista";

    /// <summary>Solo por variable de entorno (Correo__BrevoApiKey).</summary>
    public string? BrevoApiKey { get; init; }
}

/// <summary>Envío por la API transaccional de Brevo (ADR-0008). No registra destinatarios ni contenido.</summary>
internal sealed partial class EnvioCorreoBrevo(HttpClient http, IOptions<OpcionesCorreo> opciones, ILogger<EnvioCorreoBrevo> log)
    : IEnvioCorreo
{
    public async Task EnviarAsync(CorreoSaliente correo, CancellationToken cancellationToken)
    {
        var o = opciones.Value;
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email")
        {
            Content = JsonContent.Create(new
            {
                sender = new { email = o.Remitente, name = o.NombreRemitente },
                to = new[] { new { email = correo.Para } },
                subject = correo.Asunto,
                htmlContent = correo.Html,
                textContent = correo.Texto,
            }),
        };
        solicitud.Headers.Add("api-key", o.BrevoApiKey);
        using var respuesta = await http.SendAsync(solicitud, cancellationToken);
        if (!respuesta.IsSuccessStatusCode)
        {
            RegistrarFallo(log, (int)respuesta.StatusCode);
            respuesta.EnsureSuccessStatusCode();
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Brevo rechazó un correo con estado {Estado}")]
    private static partial void RegistrarFallo(ILogger logger, int estado);
}

/// <summary>Buzón en memoria para local, end-to-end y pruebas. Nunca se registra en producción.</summary>
public sealed class BuzonCorreo : IEnvioCorreo
{
    private const int Maximo = 200;
    private readonly ConcurrentQueue<CorreoSaliente> _correos = new();

    public IReadOnlyList<CorreoSaliente> Correos => [.. _correos];

    public Task EnviarAsync(CorreoSaliente correo, CancellationToken cancellationToken)
    {
        _correos.Enqueue(correo);
        while (_correos.Count > Maximo && _correos.TryDequeue(out _))
        {
        }

        return Task.CompletedTask;
    }
}
