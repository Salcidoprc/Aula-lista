namespace AulaLista.Application.Comun;

/// <summary>Correo transaccional (ADR-0008). El destinatario nunca se registra en logs.</summary>
public interface IEnvioCorreo
{
    Task EnviarAsync(CorreoSaliente correo, CancellationToken cancellationToken);
}

public sealed record CorreoSaliente(string Para, string Asunto, string Html, string Texto);
