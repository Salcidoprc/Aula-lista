using AulaLista.Application.Operacion;

namespace AulaLista.Pruebas.Unit.Operacion;

/// <summary>RF-094: el embudo de activación y la retención por cohorte del resumen del panel.</summary>
public sealed class ResumenPruebas
{
    [Fact]
    public void RF094_Embudo_DaElPorcentajeSobreLosRegistradosYSobreElPasoAnterior()
    {
        var pasos = ResumenCasoUso.Embudo(new FilaEmbudo(40, 30, 24, 12, 6, 3));

        Assert.Equal(ResumenCasoUso.Pasos, pasos.Select(p => p.Paso));
        Assert.Equal([40, 30, 24, 12, 6, 3], pasos.Select(p => p.Docentes));
        Assert.Equal([100m, 75m, 60m, 30m, 15m, 7.5m], pasos.Select(p => p.Porcentaje));
        Assert.Equal([null, 75m, 80m, 50m, 50m, 50m], pasos.Select(p => p.PorcentajeDelAnterior));
    }

    [Fact]
    public void RF094_EmbudoSinDocentes_DiceSinDatosEnLugarDeCero()
    {
        var pasos = ResumenCasoUso.Embudo(new FilaEmbudo(0, 0, 0, 0, 0, 0));

        Assert.All(pasos, p => Assert.Null(p.Porcentaje));
        Assert.All(pasos, p => Assert.Null(p.PorcentajeDelAnterior));
    }

    [Theory]
    [InlineData(1, "2026-10-25", null)] // el que se registró el domingo 11 sigue en su semana 1
    [InlineData(1, "2026-10-26", 25.0)]
    [InlineData(2, "2026-11-01", null)]
    [InlineData(2, "2026-11-02", 25.0)]
    [InlineData(4, "2026-11-15", null)]
    [InlineData(4, "2026-11-16", 25.0)]
    public void RF094_Retencion_SoloSeCalculaCuandoLaSemanaTerminoParaTodaLaCohorte(int semana, string hoy, double? esperado)
    {
        var cohorte = new FilaCohorte(new DateOnly(2026, 10, 5), 8, 2, 2, 2);

        var porcentaje = ResumenCasoUso.Retenidos(cohorte, semana, 2, DateOnly.Parse(hoy, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(esperado is null ? null : (decimal)esperado.Value, porcentaje);
    }

    [Fact]
    public void RF094_CohorteVacia_NoTienePorcentaje() =>
        Assert.Null(ResumenCasoUso.Retenidos(new FilaCohorte(new DateOnly(2026, 8, 3), 0, 0, 0, 0), 4, 0, new DateOnly(2026, 10, 28)));
}
