using System.Globalization;
using AulaLista.Application.Comun;
using AulaLista.Application.Cuentas;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AulaLista.Infrastructure.Cuentas;

/// <summary>Excel y PDF con los datos del docente (RF-008). Cada hito agrega sus hojas y secciones.</summary>
internal sealed class GeneradorExportacion(IAlmacenArchivos almacen) : IGeneradorExportacion
{
    private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");

    static GeneradorExportacion() => QuestPDF.Settings.License = LicenseType.Community;

    public async Task<(string RutaExcel, string RutaPdf)> GenerarAsync(DatosExportacion datos, CancellationToken cancellationToken)
    {
        var carpeta = Path.Combine(RetencionCuentasTrabajo.CarpetaExportaciones, datos.ExportacionId.ToString());
        var excel = await almacen.GuardarAsync(carpeta, "mis-datos.xlsx", Excel(datos), cancellationToken);
        var pdf = await almacen.GuardarAsync(carpeta, "mis-datos.pdf", Pdf(datos), cancellationToken);
        return (excel, pdf);
    }

    private static IEnumerable<(string Campo, string Valor)> Perfil(DatosExportacion d) =>
    [
        ("Correo", d.Correo),
        ("Nombre", d.Perfil.Nombre ?? string.Empty),
        ("Nivel", d.Perfil.Nivel ?? string.Empty),
        ("Escuela", d.Perfil.EscuelaNombre ?? string.Empty),
        ("CCT", d.Perfil.EscuelaCct ?? string.Empty),
        ("Estado", d.Perfil.EntidadFederativa ?? string.Empty),
        ("Zona horaria", d.Perfil.ZonaHoraria),
        ("Cuenta creada", Fecha(d.Perfil.CreatedAt)),
    ];

    private static string Fecha(DateTimeOffset fecha) => fecha.ToString("dd/MM/yyyy HH:mm 'UTC'", Mx);

    private static byte[] Excel(DatosExportacion d)
    {
        using var libro = new XLWorkbook();
        var perfil = libro.Worksheets.Add("Perfil");
        perfil.Cell(1, 1).Value = "Campo";
        perfil.Cell(1, 2).Value = "Valor";
        var fila = 2;
        foreach (var (campo, valor) in Perfil(d))
        {
            perfil.Cell(fila, 1).Value = campo;
            perfil.Cell(fila, 2).Value = valor;
            fila++;
        }

        var aceptaciones = libro.Worksheets.Add("Aceptaciones legales");
        aceptaciones.Cell(1, 1).Value = "Documento";
        aceptaciones.Cell(1, 2).Value = "Versión";
        aceptaciones.Cell(1, 3).Value = "Fecha";
        fila = 2;
        foreach (var a in d.Aceptaciones.OrderBy(a => a.Fecha))
        {
            aceptaciones.Cell(fila, 1).Value = a.Documento;
            aceptaciones.Cell(fila, 2).Value = a.Version;
            aceptaciones.Cell(fila, 3).Value = Fecha(a.Fecha);
            fila++;
        }

        foreach (var hoja in libro.Worksheets)
        {
            hoja.Row(1).Style.Font.Bold = true;
            hoja.Columns().AdjustToContents();
        }

        using var salida = new MemoryStream();
        libro.SaveAs(salida);
        return salida.ToArray();
    }

    private static byte[] Pdf(DatosExportacion d) =>
        Document.Create(doc => doc.Page(pagina =>
        {
            pagina.Size(PageSizes.Letter);
            pagina.Margin(40);
            pagina.DefaultTextStyle(t => t.FontSize(11));
            pagina.Header().Text("Aula Lista: mis datos").FontSize(18).Bold();
            pagina.Content().PaddingVertical(16).Column(col =>
            {
                col.Spacing(6);
                col.Item().Text($"Generado el {Fecha(d.GeneradaEn)}").FontColor(Colors.Grey.Darken1);
                col.Item().PaddingTop(8).Text("Perfil").FontSize(14).Bold();
                foreach (var (campo, valor) in Perfil(d))
                {
                    col.Item().Text(t =>
                    {
                        t.Span($"{campo}: ").Bold();
                        t.Span(valor);
                    });
                }

                col.Item().PaddingTop(8).Text("Aceptaciones legales").FontSize(14).Bold();
                foreach (var a in d.Aceptaciones.OrderBy(a => a.Fecha))
                {
                    col.Item().Text($"{a.Documento}, versión {a.Version}, {Fecha(a.Fecha)}");
                }
            });
            pagina.Footer().AlignCenter().Text(t =>
            {
                t.Span("Página ");
                t.CurrentPageNumber();
            });
        })).GeneratePdf();
}
