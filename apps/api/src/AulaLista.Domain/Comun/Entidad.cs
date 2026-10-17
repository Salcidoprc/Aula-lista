namespace AulaLista.Domain.Comun;

/// <summary>Base de toda entidad persistida: id UUID v7 generado en la aplicación y marcas de tiempo en UTC.</summary>
public abstract class Entidad
{
    protected Entidad(DateTimeOffset ahora)
    {
        Id = Guid.CreateVersion7(ahora);
        CreatedAt = ahora;
        UpdatedAt = ahora;
    }

    /// <summary>Para registros con id fijo, como el catálogo sembrado o una clave de idempotencia del cliente.</summary>
    protected Entidad(Guid id, DateTimeOffset ahora)
    {
        Id = id;
        CreatedAt = ahora;
        UpdatedAt = ahora;
    }

    /// <summary>Constructor para EF Core.</summary>
    protected Entidad()
    {
    }

    public Guid Id { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; protected set; }

    protected void MarcarActualizada(DateTimeOffset ahora) => UpdatedAt = ahora;
}
