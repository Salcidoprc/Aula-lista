using System.Globalization;
using AulaLista.Application.Boletas;
using AulaLista.Domain.Boletas;
using ClosedXML.Excel;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AulaLista.Infrastructure.Boletas;

/// <summary>
/// SPEC-05, "Contenido del PDF": carta, legible impresa en blanco y negro (solo tinta y grises, la jerarquía va en el
/// peso y los filetes). Lexend para el texto y "Aula Cifras" (los dígitos tabulares de Figtree) para los números,
/// como en la app; ambas OFL, embebidas.
/// </summary>
internal sealed class GeneradorBoletas : IGeneradorBoletas
{
    private const string Texto = "Lexend";
    private const string Cifras = "Aula Cifras";
    private const string Leyenda = "Reporte de evaluación del docente. No es un documento oficial.";

    private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");
    private static readonly Color Tinta = Color.FromHex("#1E2430");
    private static readonly Color Suave = Color.FromHex("#4A505C");
    private static readonly Color Filete = Color.FromHex("#9AA0AA");

    static GeneradorBoletas()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var ensamblado = typeof(GeneradorBoletas).Assembly;
        foreach (var fuente in new[] { "Lexend-400", "Lexend-600", "Lexend-700", "AulaCifras-500", "AulaCifras-700" })
        {
            using var flujo = ensamblado.GetManifestResourceStream($"AulaLista.Infrastructure.Boletas.Fuentes.{fuente}.ttf")
                ?? throw new InvalidOperationException($"Falta la fuente {fuente}.");
            FontManager.RegisterFontFromStream(flujo);
        }
    }

    public byte[] Pdf(IReadOnlyList<BoletaParaPdf> boletas) =>
        Document.Create(documento =>
        {
            foreach (var boleta in boletas)
            {
                documento.Page(pagina => Pagina(pagina, boleta));
            }
        })
        .WithMetadata(new DocumentMetadata { Title = "Reporte de evaluación", Creator = "Aula Lista", Producer = "Aula Lista" })
        .GeneratePdf();

    public byte[] Excel(ExportacionPeriodo exportacion)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Calificaciones");
        string[] encabezados =
        [
            "Número", "Alumno", .. exportacion.Rubros.Select(r => $"{r.Nombre} ({r.Peso.ToString("0.##", Mx)}%)"), "Final", "Observación",
        ];
        for (var c = 0; c < encabezados.Length; c++)
        {
            hoja.Cell(1, c + 1).Value = encabezados[c];
        }

        var formatoFinal = exportacion.DecimalesFinal == 0 ? "0" : "0." + new string('0', exportacion.DecimalesFinal);
        var fila = 2;
        foreach (var f in exportacion.Filas)
        {
            hoja.Cell(fila, 1).Value = f.NumeroLista;
            hoja.Cell(fila, 2).Value = f.Alumno;
            var columna = 3;
            foreach (var promedio in f.Rubros)
            {
                Numero(hoja.Cell(fila, columna++), promedio, "0.00");
            }

            Numero(hoja.Cell(fila, columna++), f.Calificacion, formatoFinal);
            hoja.Cell(fila, columna).Value = f.Observacion ?? string.Empty;
            hoja.Cell(fila, columna).Style.Alignment.WrapText = true;
            fila++;
        }

        hoja.Row(1).Style.Font.Bold = true;
        hoja.SheetView.FreezeRows(1);
        hoja.Columns(1, encabezados.Length - 1).AdjustToContents();
        hoja.Column(encabezados.Length).Width = 80;
        libro.Properties.Title = exportacion.Titulo;
        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        return memoria.ToArray();
    }

    private static void Numero(IXLCell celda, decimal? valor, string formato)
    {
        if (valor is { } v)
        {
            celda.Value = v;
            celda.Style.NumberFormat.Format = formato;
        }
    }

    private static string Cifra(decimal? valor, int decimales) =>
        valor is { } v ? v.ToString("F" + decimales.ToString(CultureInfo.InvariantCulture), Mx) : "—";

    private static string Fecha(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", Mx);

    private static void Pagina(PageDescriptor pagina, BoletaParaPdf boleta)
    {
        var d = boleta.Datos;
        pagina.Size(PageSizes.Letter);
        pagina.MarginHorizontal(20, Unit.Millimetre);
        pagina.MarginVertical(18, Unit.Millimetre);
        pagina.PageColor(Colors.White);
        pagina.DefaultTextStyle(t => t.FontFamily(Texto, Cifras).FontSize(10).FontColor(Tinta).LineHeight(1.35f));

        pagina.Header().Column(c =>
        {
            c.Item().Row(r =>
            {
                r.RelativeItem().Column(escuela =>
                {
                    escuela.Item().Text(d.Escuela ?? " ").FontSize(12).SemiBold();
                    if (d.Cct is { } cct)
                    {
                        escuela.Item().Text(t =>
                        {
                            t.Span("CCT ").FontColor(Suave).FontSize(9);
                            t.Span(cct).FontSize(9).LetterSpacing(0.04f);
                        });
                    }
                });
                r.ConstantItem(70, Unit.Millimetre).AlignRight().Column(titulo =>
                {
                    titulo.Item().AlignRight().Text("Reporte de evaluación").FontSize(14).Bold();
                    titulo.Item().AlignRight().Text($"Ciclo {d.Ciclo} · {d.Unidad}").FontSize(9).FontColor(Suave);
                });
            });
            c.Item().PaddingTop(8).LineHorizontal(2).LineColor(Tinta);
        });

        pagina.Content().PaddingTop(16).Column(c =>
        {
            c.Spacing(18);
            c.Item().Table(t =>
            {
                t.ColumnsDefinition(col =>
                {
                    col.RelativeColumn(3);
                    col.RelativeColumn(2);
                });
                Dato(t.Cell(), "Alumno", d.Alumno, grande: true);
                Dato(t.Cell(), "Número de lista", d.NumeroLista.ToString(Mx));
                Dato(t.Cell(), "Grado y grupo", $"{d.Grado}° {d.Grupo} de {(d.Nivel == "secundaria" ? "secundaria" : "primaria")}");
                Dato(t.Cell(), "Materia", d.Materia ?? "Todas las del grado");
                Dato(t.Cell(), "Docente", string.IsNullOrWhiteSpace(d.Docente) ? "—" : d.Docente);
                Dato(t.Cell(), "Ciclo escolar", d.Ciclo);
            });

            c.Item().Row(r =>
            {
                r.Spacing(14, Unit.Millimetre);
                r.RelativeItem().Table(t =>
                {
                    t.ColumnsDefinition(col =>
                    {
                        col.RelativeColumn(5);
                        col.RelativeColumn(2);
                        col.RelativeColumn(2);
                    });
                    t.Header(h =>
                    {
                        Encabezado(h.Cell(), "Rubro", derecha: false);
                        Encabezado(h.Cell(), "Peso", derecha: true);
                        Encabezado(h.Cell(), "Promedio", derecha: true);
                    });
                    foreach (var rubro in d.Rubros)
                    {
                        Celda(t.Cell()).Text(rubro.Nombre);
                        Celda(t.Cell()).AlignRight().Text($"{rubro.Peso.ToString("0.##", Mx)}%").FontFamily(Cifras, Texto);
                        Celda(t.Cell()).AlignRight().Text(Cifra(rubro.Promedio, 2)).FontFamily(Cifras, Texto).SemiBold();
                    }
                });
                r.ConstantItem(46, Unit.Millimetre).Border(1.5f).BorderColor(Tinta).Padding(10).Column(final =>
                {
                    final.Item().Text("Calificación final").FontSize(9).SemiBold();
                    final.Item().PaddingTop(4).AlignRight()
                        .Text(Cifra(d.Calificacion, d.DecimalesFinal)).FontFamily(Cifras, Texto).FontSize(30).Bold().LineHeight(1);
                });
            });

            c.Item().Column(o =>
            {
                o.Item().PaddingBottom(4).BorderBottom(0.5f).BorderColor(Filete).Text("Observación del docente").FontSize(10).Bold();
                o.Item().PaddingTop(6).Text(d.Observacion).FontSize(11).LineHeight(1.5f);
            });
        });

        pagina.Footer().Column(c =>
        {
            c.Item().LineHorizontal(0.5f).LineColor(Filete);
            c.Item().PaddingTop(6).Row(r =>
            {
                r.RelativeItem().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8.5f).FontColor(Suave));
                    t.Span($"Emitida el {Fecha(boleta.Emitida)} · Versión {boleta.Version}");
                    if (boleta.Corregida is { } corregida)
                    {
                        t.Span($" · Versión corregida el {Fecha(corregida)}").FontColor(Tinta).SemiBold();
                    }
                });
                r.AutoItem().Text(t =>
                {
                    t.Span("Código de verificación ").FontSize(8.5f).FontColor(Suave);
                    t.Span(boleta.Codigo).FontSize(8.5f).SemiBold().LetterSpacing(0.08f);
                });
            });
            c.Item().PaddingTop(2).Text(Leyenda).FontSize(8.5f).FontColor(Suave);
        });
    }

    private static void Dato(IContainer celda, string etiqueta, string valor, bool grande = false) =>
        celda.PaddingBottom(8).Column(c =>
        {
            c.Item().Text(etiqueta).FontSize(8).FontColor(Suave);
            c.Item().Text(valor).FontSize(grande ? 13 : 10.5f).SemiBold();
        });

    private static void Encabezado(IContainer celda, string texto, bool derecha)
    {
        var caja = celda.BorderBottom(1).BorderColor(Tinta).PaddingBottom(4);
        (derecha ? caja.AlignRight() : caja).Text(texto).FontSize(8.5f).SemiBold().FontColor(Suave);
    }

    private static IContainer Celda(IContainer celda) => celda.BorderBottom(0.5f).BorderColor(Filete).PaddingVertical(5);
}
