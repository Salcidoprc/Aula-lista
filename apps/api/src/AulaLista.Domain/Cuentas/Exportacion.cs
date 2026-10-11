using AulaLista.Domain.Comun;

namespace AulaLista.Domain.Cuentas;

/// <summary>Exportación de los datos del docente (RF-007). Los archivos valen lo que diga la configuración (24 horas).</summary>
public sealed class Exportacion : Entidad
{
    public Exportacion(Guid docenteId, DateTimeOffset ahora)
        : base(ahora)
    {
        DocenteId = docenteId;
        Estado = EstadoExportacion.Pendiente;
    }

    private Exportacion()
    {
    }

    public Guid DocenteId { get; private set; }

    public string Estado { get; private set; } = EstadoExportacion.Pendiente;

    public string? RutaExcel { get; private set; }

    public string? RutaPdf { get; private set; }

    public DateTimeOffset? ExpiraEn { get; private set; }

    public bool EstaDisponible(DateTimeOffset ahora) =>
        Estado == EstadoExportacion.Lista && ExpiraEn is { } expira && ahora < expira;

    public void MarcarLista(string rutaExcel, string rutaPdf, DateTimeOffset ahora, TimeSpan vigencia)
    {
        RutaExcel = rutaExcel;
        RutaPdf = rutaPdf;
        Estado = EstadoExportacion.Lista;
        ExpiraEn = ahora + vigencia;
        MarcarActualizada(ahora);
    }

    public void MarcarFallida(DateTimeOffset ahora)
    {
        Estado = EstadoExportacion.Fallida;
        MarcarActualizada(ahora);
    }
}

public static class EstadoExportacion
{
    public const string Pendiente = "pendiente";
    public const string Lista = "lista";
    public const string Fallida = "fallida";
}
