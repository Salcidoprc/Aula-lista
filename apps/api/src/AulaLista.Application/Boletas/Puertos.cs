using AulaLista.Domain.Boletas;

namespace AulaLista.Application.Boletas;

/// <summary>Boletas y sus versiones: tabla con RLS por docente (SPEC-05).</summary>
public interface IRepositorioBoletas
{
    /// <summary>Todas las versiones de las boletas del periodo.</summary>
    Task<IReadOnlyList<Boleta>> BoletasAsync(Guid periodoId, CancellationToken cancellationToken);

    Task<Boleta?> BoletaAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Boleta>> BoletasPorIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    void Agregar(Boleta boleta);
}

/// <summary>Una boleta lista para dibujarse: la foto más lo que va en el pie.</summary>
public sealed record BoletaParaPdf(DatosBoleta Datos, int Version, string Codigo, DateOnly Emitida, DateOnly? Corregida);

/// <summary>Una fila de la exportación a Excel (RN-056).</summary>
public sealed record FilaExportacion(int NumeroLista, string Alumno, IReadOnlyList<decimal?> Rubros, decimal? Calificacion, string? Observacion);

/// <summary>Las calificaciones del periodo para llenar formatos oficiales (RF-055).</summary>
public sealed record ExportacionPeriodo(
    string Titulo, IReadOnlyList<RubroBoleta> Rubros, int DecimalesFinal, IReadOnlyList<FilaExportacion> Filas);

/// <summary>Dibuja las boletas en PDF y la exportación en Excel (02-arquitectura, Integraciones: QuestPDF).</summary>
public interface IGeneradorBoletas
{
    /// <summary>Una o varias boletas, una por página carta, en ese orden.</summary>
    byte[] Pdf(IReadOnlyList<BoletaParaPdf> boletas);

    byte[] Excel(ExportacionPeriodo exportacion);
}
