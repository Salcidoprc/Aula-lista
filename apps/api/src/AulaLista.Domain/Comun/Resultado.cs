namespace AulaLista.Domain.Comun;

/// <summary>Resultado de una operación que puede fallar por una regla de negocio.</summary>
public class Resultado
{
    protected Resultado(bool exito, ErrorNegocio error)
    {
        if (exito == (error != ErrorNegocio.Ninguno))
        {
            throw new ArgumentException("Un resultado exitoso no lleva error y uno fallido sí.", nameof(error));
        }

        EsExito = exito;
        Error = error;
    }

    public bool EsExito { get; }

    public bool EsFallo => !EsExito;

    public ErrorNegocio Error { get; }

    public static Resultado Exito() => new(true, ErrorNegocio.Ninguno);

    public static Resultado Fallo(ErrorNegocio error) => new(false, error);

    public static Resultado<T> Exito<T>(T valor) => new(valor, true, ErrorNegocio.Ninguno);

    public static Resultado<T> Fallo<T>(ErrorNegocio error) => new(default, false, error);
}

public sealed class Resultado<T> : Resultado
{
    private readonly T? _valor;

    internal Resultado(T? valor, bool exito, ErrorNegocio error)
        : base(exito, error) => _valor = valor;

    public T Valor => EsExito
        ? _valor!
        : throw new InvalidOperationException("No hay valor en un resultado fallido.");
}
