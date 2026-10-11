namespace AulaLista.Domain.Comun;

/// <summary>Error de negocio esperado. El código es estable, en español y snake_case.</summary>
public sealed record ErrorNegocio(string Codigo, string Mensaje)
{
    public static readonly ErrorNegocio Ninguno = new(string.Empty, string.Empty);
}
