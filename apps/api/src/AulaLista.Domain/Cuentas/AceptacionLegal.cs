using AulaLista.Domain.Comun;

namespace AulaLista.Domain.Cuentas;

/// <summary>Registro inmutable de que el docente aceptó una versión de un documento legal (RF-005, RF-006).</summary>
public sealed class AceptacionLegal : Entidad
{
    public AceptacionLegal(Guid docenteId, string documento, string version, DateTimeOffset ahora)
        : base(ahora)
    {
        if (!DocumentoLegal.Todos.Contains(documento))
        {
            throw new ArgumentException("Documento legal desconocido.", nameof(documento));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        DocenteId = docenteId;
        Documento = documento;
        Version = version;
        Fecha = ahora;
    }

    private AceptacionLegal()
    {
    }

    public Guid DocenteId { get; private set; }

    public string Documento { get; private set; } = string.Empty;

    public string Version { get; private set; } = string.Empty;

    public DateTimeOffset Fecha { get; private set; }
}

public static class DocumentoLegal
{
    public const string Terminos = "terminos";
    public const string Privacidad = "privacidad";
    public const string DeclaracionDatos = "declaracion_datos";

    public static readonly IReadOnlySet<string> Todos =
        new HashSet<string>(StringComparer.Ordinal) { Terminos, Privacidad, DeclaracionDatos };
}
