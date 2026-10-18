using System.Globalization;
using System.Text.Json;
using AulaLista.Domain.Evaluacion;

namespace AulaLista.Pruebas.Unit.Evaluacion;

/// <summary>SPEC-03, cálculo de calificaciones con los vectores compartidos con el frontend (RN-036).</summary>
public sealed class CalculoPruebas
{
    private static readonly Lazy<JsonElement> Vectores = new(() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "vectores-calculo.json"))).RootElement);

    public static TheoryData<string> Casos()
    {
        var datos = new TheoryData<string>();
        foreach (var caso in Vectores.Value.GetProperty("casos").EnumerateArray())
        {
            datos.Add(caso.GetProperty("nombre").GetString()!);
        }

        return datos;
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void RN036_VectoresCompartidos_DanElMismoResultado(string nombre)
    {
        var caso = Vectores.Value.GetProperty("casos").EnumerateArray().Single(c => c.GetProperty("nombre").GetString() == nombre);
        var ids = new Dictionary<string, Guid>();
        Guid Id(string clave) => ids.TryGetValue(clave, out var id) ? id : ids[clave] = Guid.NewGuid();

        var configuracion = caso.GetProperty("configuracion");
        var rubros = caso.GetProperty("rubros").EnumerateArray().Select(r =>
        {
            var criterios = r.TryGetProperty("criterios", out var cs)
                ? cs.EnumerateArray().Select(c => new CriterioCalculo(
                    Id(c.GetProperty("id").GetString()!), Numero(c, "peso")!.Value, c.GetProperty("forma").GetString()!, Numero(c, "califMinima")!.Value)).ToList()
                : [];
            var celdas = r.GetProperty("celdas").EnumerateArray().Select(c => new CeldaCalculo(
                Numero(c, "valor"),
                Texto(c, "marca"),
                c.TryGetProperty("criterios", out var ccs)
                    ? ccs.EnumerateArray().Select(cc => new CeldaCriterio(Id(cc.GetProperty("id").GetString()!), Numero(cc, "valor"), Texto(cc, "marca"))).ToList()
                    : [])).ToList();
            var comportamiento = r.TryGetProperty("comportamiento", out var cp) ? cp.GetString()! : Comportamiento.Actividades;
            var tomas = r.TryGetProperty("tomas", out var ts) ? ts.EnumerateArray().Select(t => t.GetString()!).ToList() : null;
            return new RubroCalculo(
                Id(r.GetProperty("id").GetString()!), Numero(r, "peso")!.Value, comportamiento, r.GetProperty("forma").GetString()!,
                Numero(r, "califMinima")!.Value, criterios, celdas, tomas);
        }).ToList();

        var resultado = Calculo.Periodo(rubros, new ConfiguracionCalculo(
            Numero(configuracion, "califMinima")!.Value, configuracion.GetProperty("decimalesFinal").GetInt32(), configuracion.GetProperty("redondeo").GetString()!,
            Numero(configuracion, "valorRetardo") ?? 1m));

        var esperado = caso.GetProperty("esperado");
        foreach (var rubro in esperado.GetProperty("rubros").EnumerateObject())
        {
            var promedio = resultado.Rubros.Single(r => r.RubroId == Id(rubro.Name)).Promedio;
            Assert.Equal(Numero(rubro.Value), promedio is { } p ? Calculo.Redondear(p, 2, "cercano") : null);
        }

        Assert.Equal(Numero(esperado, "periodo"), resultado.Periodo);
        Assert.Equal(Numero(esperado, "final"), resultado.Final);
        Assert.Equal(
            esperado.GetProperty("sinDatos").EnumerateArray().Select(s => Id(s.GetString()!)).ToHashSet(),
            resultado.RubrosSinDatos.ToHashSet());
    }

    [Fact]
    public void SPEC03_ExposicionConCriterios_9_8_10_Da9() =>
        Assert.Equal(9m, Calculo.ValorConCriterios(
            [Criterio(1, 40m), Criterio(2, 30m), Criterio(3, 30m)],
            [new(Ids[1], 9m, "ninguna"), new(Ids[2], 8m, "ninguna"), new(Ids[3], 10m, "ninguna")]));

    private static readonly Guid[] Ids = [Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

    private static CriterioCalculo Criterio(int i, decimal peso) => new(Ids[i], peso, FormaCalificacion.Numerica, 5m);

    private static string Texto(JsonElement e, string propiedad) =>
        e.TryGetProperty(propiedad, out var v) ? v.GetString()! : MarcaCalificacion.Ninguna;

    private static decimal? Numero(JsonElement e, string propiedad) =>
        e.TryGetProperty(propiedad, out var v) ? Numero(v) : null;

    private static decimal? Numero(JsonElement v) =>
        v.ValueKind == JsonValueKind.Null ? null : decimal.Parse(v.GetString()!, CultureInfo.InvariantCulture);
}
