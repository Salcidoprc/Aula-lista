using System.Globalization;
using System.Text;
using AulaLista.Application.Grupos;
using AulaLista.Domain.Grupos;
using ClosedXML.Excel;

namespace AulaLista.Infrastructure.Grupos;

/// <summary>
/// Lee la primera hoja de un .xlsx o un .csv como texto (RF-013). Los CSV se leen como UTF-8 y, si no lo son,
/// como Windows-1252, que es lo que guarda Excel en México; el separador puede ser coma o punto y coma (ADR-0014).
/// </summary>
internal sealed class HojasDeCalculo : IHojasDeCalculo
{
    private const int ColumnasMaximas = 30;
    private static readonly string[] ColumnasPlantilla =
    [
        "Apellidos", "Nombres", "Tutor", "WhatsApp del tutor", "Correo del tutor", "Parentesco",
        "Tutor 2", "WhatsApp del tutor 2", "Correo del tutor 2", "Parentesco 2",
    ];

    static HojasDeCalculo() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public TablaCruda? Leer(Stream archivo, string origen)
    {
        try
        {
            // El flujo del formulario no admite todos los saltos que hace el lector de .xlsx.
            using var memoria = new MemoryStream();
            archivo.CopyTo(memoria);
            memoria.Position = 0;
            var filas = origen == OrigenImportacion.Excel ? LeerExcel(memoria) : LeerCsv(memoria);
            var conDatos = filas.SkipWhile(f => f.All(string.IsNullOrWhiteSpace)).ToList();
            if (conDatos.Count == 0)
            {
                return null;
            }

            var ancho = Math.Min(ColumnasMaximas, conDatos.Max(f => f.Count));
            IReadOnlyList<string> Ajustar(IReadOnlyList<string> f) =>
                Enumerable.Range(0, ancho).Select(i => i < f.Count ? f[i].Trim() : string.Empty).ToList();
            return new TablaCruda(Ajustar(conDatos[0]), conDatos.Skip(1).Select(Ajustar).ToList());
        }
        catch (Exception e) when (e is InvalidDataException or IOException or ArgumentException or InvalidOperationException
            or FormatException or NotSupportedException or System.Xml.XmlException
            or DocumentFormat.OpenXml.Packaging.OpenXmlPackageException)
        {
            return null;
        }
    }

    public byte[] Plantilla()
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Alumnos");
        for (var i = 0; i < ColumnasPlantilla.Length; i++)
        {
            hoja.Cell(1, i + 1).Value = ColumnasPlantilla[i];
        }

        hoja.Cell(2, 1).Value = "García López";
        hoja.Cell(2, 2).Value = "Ana Sofía";
        hoja.Cell(2, 3).Value = "María López Ruiz";
        hoja.Cell(2, 4).Value = "667 123 4567";
        hoja.Cell(2, 6).Value = "Mamá";
        hoja.Row(1).Style.Font.Bold = true;
        hoja.Row(2).Style.Font.Italic = true;
        hoja.Columns().AdjustToContents();
        hoja.SheetView.FreezeRows(1);

        using var salida = new MemoryStream();
        libro.SaveAs(salida);
        return salida.ToArray();
    }

    private static List<IReadOnlyList<string>> LeerExcel(Stream archivo)
    {
        using var libro = new XLWorkbook(archivo);
        var hoja = libro.Worksheets.FirstOrDefault(h => h.Visibility == XLWorksheetVisibility.Visible)
            ?? throw new InvalidDataException("El libro no tiene hojas visibles.");
        var usado = hoja.RangeUsed();
        if (usado is null)
        {
            return [];
        }

        var ultimaColumna = Math.Min(usado.LastColumn().ColumnNumber(), ColumnasMaximas);
        return hoja.Rows(1, usado.LastRow().RowNumber())
            .Select(fila => (IReadOnlyList<string>)Enumerable.Range(1, ultimaColumna)
                .Select(c => Texto(fila.Cell(c)))
                .ToList())
            .ToList();
    }

    /// <summary>Teléfonos guardados como número salen sin ".0" ni notación científica.</summary>
    private static string Texto(IXLCell celda) => celda.DataType switch
    {
        XLDataType.Number => celda.GetDouble().ToString("0.##########", CultureInfo.InvariantCulture),
        XLDataType.DateTime => celda.GetDateTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        _ => celda.GetFormattedString(),
    };

    private static List<IReadOnlyList<string>> LeerCsv(MemoryStream archivo)
    {
        var bytes = archivo.ToArray();
        string texto;
        try
        {
            texto = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            texto = Encoding.GetEncoding(1252).GetString(bytes);
        }

        texto = texto.TrimStart('﻿');
        var primeraLinea = texto.Split('\n', 2)[0];
        var separador = primeraLinea.Count(c => c == ';') > primeraLinea.Count(c => c == ',') ? ';' : ',';
        return Partir(texto, separador);
    }

    /// <summary>CSV con comillas dobles (RFC 4180), incluidas comillas escapadas y saltos de línea dentro de un campo.</summary>
    private static List<IReadOnlyList<string>> Partir(string texto, char separador)
    {
        var filas = new List<IReadOnlyList<string>>();
        var fila = new List<string>();
        var campo = new StringBuilder();
        var entreComillas = false;
        for (var i = 0; i < texto.Length; i++)
        {
            var c = texto[i];
            if (entreComillas)
            {
                if (c == '"' && i + 1 < texto.Length && texto[i + 1] == '"')
                {
                    campo.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    entreComillas = false;
                }
                else
                {
                    campo.Append(c);
                }
            }
            else if (c == '"')
            {
                entreComillas = true;
            }
            else if (c == separador)
            {
                fila.Add(campo.ToString());
                campo.Clear();
            }
            else if (c == '\n')
            {
                fila.Add(campo.ToString().TrimEnd('\r'));
                campo.Clear();
                filas.Add(fila);
                fila = [];
            }
            else
            {
                campo.Append(c);
            }
        }

        if (campo.Length > 0 || fila.Count > 0)
        {
            fila.Add(campo.ToString().TrimEnd('\r'));
            filas.Add(fila);
        }

        return filas;
    }
}
