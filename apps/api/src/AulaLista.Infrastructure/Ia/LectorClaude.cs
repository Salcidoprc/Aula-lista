using System.Diagnostics;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using AulaLista.Application.Cierre;
using AulaLista.Application.Grupos;
using AulaLista.Domain.Cierre;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AulaLista.Infrastructure.Ia;

/// <summary>
/// Lee los nombres de una foto de lista con la API de Claude (SPEC-02, RF-015; reglas/05-ia). Es la única llamada que
/// lleva nombres reales: la foto los trae y no hay forma de seudonimizarla (reglas/04-seguridad). La imagen viaja solo
/// en esta petición; no se guarda ni se registra, y el registro lleva tokens, duración y resultado, nunca los nombres.
/// </summary>
internal sealed partial class LectorClaude(AnthropicClient cliente, IOptions<OpcionesIa> opciones, ILogger<LectorClaude> registro)
    : ILectorListas
{
    public async Task<ListaLeida> LeerAsync(ImagenLista imagen, CancellationToken cancellationToken)
    {
        var configuracion = opciones.Value;
        var reloj = Stopwatch.StartNew();
        try
        {
            var foto = new ImageBlockParam(new Base64ImageSource
            {
                Data = Convert.ToBase64String(imagen.Datos.Span),
                MediaType = imagen.TipoMedio == ImagenLista.Png ? MediaType.ImagePng : MediaType.ImageJpeg,
            });
            var respuesta = await cliente.Messages.Create(
                new MessageCreateParams
                {
                    Model = configuracion.Modelo,
                    MaxTokens = configuracion.MaximoTokensSalidaLista,
                    OutputConfig = new OutputConfig { Effort = Effort.Low },
                    System = new List<TextBlockParam> { new() { Text = PromptLista.Sistema, CacheControl = new CacheControlEphemeral() } },
                    Messages = [new() { Role = Role.User, Content = new List<ContentBlockParam> { foto, new TextBlockParam(PromptLista.Usuario) } }],
                },
                cancellationToken);

            var entrada = (int)(respuesta.Usage.InputTokens + (respuesta.Usage.CacheReadInputTokens ?? 0) + (respuesta.Usage.CacheCreationInputTokens ?? 0));
            var salida = (int)respuesta.Usage.OutputTokens;
            var duracion = (int)reloj.ElapsedMilliseconds;

            IReadOnlyList<string> nombres = [];
            string? fallo = respuesta.StopReason == "refusal" ? FalloGeneracion.Rechazo
                : respuesta.StopReason == "max_tokens" ? FalloGeneracion.Longitud
                : null;
            if (fallo is null)
            {
                nombres = PromptLista.Nombres(string.Concat(respuesta.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text)));
            }

            LecturaRegistrada(registro, configuracion.Modelo, PromptLista.Version, entrada, salida, nombres.Count, duracion, fallo ?? "ok");
            return new ListaLeida(nombres, fallo, configuracion.Modelo, PromptLista.Version, entrada, salida, duracion);
        }
        catch (Exception ex) when (ex is AnthropicApiException or AnthropicIOException or HttpRequestException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            var duracion = (int)reloj.ElapsedMilliseconds;
            var resultado = $"{FalloGeneracion.Error}:{ex.GetType().Name}";
            LecturaRegistrada(registro, configuracion.Modelo, PromptLista.Version, 0, 0, 0, duracion, resultado);
            return new ListaLeida([], FalloGeneracion.Error, configuracion.Modelo, PromptLista.Version, 0, 0, duracion);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Lectura de lista con la IA: {Modelo} {VersionPrompt}, {TokensEntrada} de entrada, {TokensSalida} de salida, {Renglones} renglones, {DuracionMs} ms, {Resultado}")]
    private static partial void LecturaRegistrada(
        ILogger registro, string modelo, string versionPrompt, int tokensEntrada, int tokensSalida, int renglones, int duracionMs, string resultado);
}
