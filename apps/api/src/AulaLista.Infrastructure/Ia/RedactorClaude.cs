using System.Diagnostics;
using Anthropic;
using Anthropic.Core;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using AulaLista.Application.Cierre;
using AulaLista.Domain.Cierre;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AulaLista.Infrastructure.Ia;

/// <summary>
/// Observaciones con la API de Claude y el SDK oficial (ADR-0004, reglas/05-ia). Modelo, esfuerzo y tope de salida
/// vienen de configuración; los reintentos con espera exponencial los hace el SDK. Se revisa <c>stop_reason</c> antes
/// de leer el texto: un rechazo o un corte por longitud es una falla controlada. El registro lleva modelo, tokens,
/// duración y resultado, nunca el contenido.
/// </summary>
internal sealed partial class RedactorClaude(AnthropicClient cliente, IOptions<OpcionesIa> opciones, ILogger<RedactorClaude> registro)
    : IRedactorObservaciones
{
    public async Task<ObservacionRedactada> RedactarAsync(EntradaObservacion entrada, CancellationToken cancellationToken)
    {
        var configuracion = opciones.Value;
        var sistema = new List<TextBlockParam>
        {
            new() { Text = PromptObservacion.Sistema, CacheControl = new CacheControlEphemeral() },
        };
        if (PromptObservacion.Ejemplos(entrada.Estilo) is { } ejemplos)
        {
            sistema.Add(new() { Text = ejemplos, CacheControl = new CacheControlEphemeral() });
        }

        var reloj = Stopwatch.StartNew();
        try
        {
            var respuesta = await cliente.Messages.Create(
                new MessageCreateParams
                {
                    Model = configuracion.Modelo,
                    MaxTokens = configuracion.MaximoTokensSalida,
                    OutputConfig = new OutputConfig { Effort = Esfuerzo(configuracion.Esfuerzo) },
                    System = sistema,
                    Messages = [new() { Role = Role.User, Content = PromptObservacion.Usuario(entrada) }],
                },
                cancellationToken);

            var entradaTokens = (int)(respuesta.Usage.InputTokens + (respuesta.Usage.CacheReadInputTokens ?? 0) + (respuesta.Usage.CacheCreationInputTokens ?? 0));
            var salidaTokens = (int)respuesta.Usage.OutputTokens;
            var cache = (int)(respuesta.Usage.CacheReadInputTokens ?? 0);
            var duracion = (int)reloj.ElapsedMilliseconds;

            string? fallo = null;
            string? texto = null;
            if (respuesta.StopReason == "refusal")
            {
                fallo = FalloGeneracion.Rechazo;
            }
            else if (respuesta.StopReason == "max_tokens")
            {
                fallo = FalloGeneracion.Longitud;
            }
            else
            {
                texto = string.Concat(respuesta.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
                fallo = string.IsNullOrWhiteSpace(texto) ? FalloGeneracion.Error : null;
            }

            Registrar(configuracion.Modelo, entradaTokens, salidaTokens, cache, duracion, fallo ?? "ok");
            return new ObservacionRedactada(fallo is null ? texto : null, fallo, configuracion.Modelo, PromptObservacion.Version, entradaTokens, salidaTokens, cache, duracion);
        }
        catch (Exception ex) when (ex is AnthropicApiException or AnthropicIOException or HttpRequestException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            var duracion = (int)reloj.ElapsedMilliseconds;
            Registrar(configuracion.Modelo, 0, 0, 0, duracion, $"{FalloGeneracion.Error}:{ex.GetType().Name}");
            return new ObservacionRedactada(null, FalloGeneracion.Error, configuracion.Modelo, PromptObservacion.Version, 0, 0, 0, duracion);
        }
    }

    private static ApiEnum<string, Effort> Esfuerzo(string esfuerzo) => esfuerzo switch
    {
        "medium" => Effort.Medium,
        "high" => Effort.High,
        _ => Effort.Low,
    };

    private void Registrar(string modelo, int tokensEntrada, int tokensSalida, int tokensCache, int duracionMs, string resultado) =>
        LlamadaRegistrada(registro, modelo, PromptObservacion.Version, tokensEntrada, tokensSalida, tokensCache, duracionMs, resultado);

    [LoggerMessage(Level = LogLevel.Information, Message = "Llamada a la IA: {Modelo} {VersionPrompt}, {TokensEntrada} de entrada ({TokensCache} de caché), {TokensSalida} de salida, {DuracionMs} ms, {Resultado}")]
    private static partial void LlamadaRegistrada(
        ILogger registro, string modelo, string versionPrompt, int tokensEntrada, int tokensSalida, int tokensCache, int duracionMs, string resultado);
}
