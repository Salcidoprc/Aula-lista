using System.Text;
using System.Text.Json;
using AulaLista.Application.Planes;
using AulaLista.Domain.Planes;
using AulaLista.Infrastructure.Planes;

namespace AulaLista.Pruebas.Unit.Planes;

/// <summary>SPEC-08: tarifa, vigencia de la prueba Pro, prepago, gracia, firma de webhooks y traducción de eventos de Stripe.</summary>
public sealed class PlanesPruebas
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 2, 18, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 79)]
    [InlineData(2, 108)]
    [InlineData(3, 137)]
    public void RN082_TotalMensual_EsElPrimerGrupoMasCadaAdicional(int grupos, int total) =>
        Assert.Equal((decimal)total, Tarifa.Mensual(grupos, 79m, 29m));

    [Fact]
    public void RN081_Prueba_Dura14DiasOHastaPublicarLoQuePaseDespuesYNuncaMasDe30()
    {
        var sinPublicar = new VigenciaPrueba(Guid.NewGuid(), Ahora, null, 14, 30);
        var publicadaPronto = sinPublicar with { PublicadaEn = Ahora.AddDays(3) };
        var publicadaTarde = sinPublicar with { PublicadaEn = Ahora.AddDays(20) };

        Assert.Equal(Ahora.AddDays(30), sinPublicar.Fin);
        Assert.Equal(14, sinPublicar.DiasRestantes(Ahora));
        Assert.True(sinPublicar.TerminaAlPublicar(Ahora.AddDays(15)));
        Assert.Equal(Ahora.AddDays(14), publicadaPronto.Fin);
        Assert.Equal(Ahora.AddDays(20), publicadaTarde.Fin);
        Assert.False(sinPublicar.Vigente(Ahora.AddDays(30)));
    }

    [Fact]
    public void RN081_LaPruebaSeUsaUnaSolaVez()
    {
        var s = new Suscripcion(Guid.NewGuid(), "simulado", Ahora);

        Assert.True(s.IniciarPrueba(Guid.NewGuid(), Guid.NewGuid(), Ahora).EsExito);
        Assert.Equal(ErroresPlanes.PruebaUsada, s.IniciarPrueba(Guid.NewGuid(), Guid.NewGuid(), Ahora).Error);
    }

    [Fact]
    public void RN089_RenovarAntesDeVencer_SumaDesdeElFinDelPrepagoYAgregarNoMueveLaFecha()
    {
        var s = new Suscripcion(Guid.NewGuid(), "simulado", Ahora);
        var grupo = Guid.NewGuid();
        var hasta = s.AplicarPrepago([grupo], 3, agregar: false, Ahora);
        var renovado = s.AplicarPrepago([grupo], 6, agregar: false, hasta.AddDays(-2));
        var conOtro = s.AplicarPrepago([Guid.NewGuid()], 0, agregar: true, hasta);

        Assert.Equal(Ahora.AddMonths(3), hasta);
        Assert.Equal(hasta.AddMonths(6), renovado);
        Assert.Equal(renovado, conOtro);
        Assert.Equal(2, s.GruposPro.Count);
    }

    [Fact]
    public void RN089_AvisoDeVencimiento_Sale7Y1DiasAntesUnaVezCadaUno()
    {
        var s = new Suscripcion(Guid.NewGuid(), "simulado", Ahora);
        var hasta = s.AplicarPrepago([Guid.NewGuid()], 3, agregar: false, Ahora);
        int[] dias = [7, 1];

        Assert.Null(s.AvisoVencimientoPendiente(hasta.AddDays(-8), dias));
        Assert.Equal(7, s.AvisoVencimientoPendiente(hasta.AddDays(-6.5), dias));
        s.MarcarAvisoVencimiento(7, Ahora);
        Assert.Null(s.AvisoVencimientoPendiente(hasta.AddDays(-3), dias));
        Assert.Equal(1, s.AvisoVencimientoPendiente(hasta.AddHours(-20), dias));
    }

    [Fact]
    public void RN085_CobrosFallidosSeguidos_NoAlarganLaGraciaYAlTerminarQuedaSinGrupos()
    {
        var s = new Suscripcion(Guid.NewGuid(), "simulado", Ahora);
        s.AplicarSuscripcion("cus_1", "sub_1", [Guid.NewGuid()], Ahora.AddMonths(1), Ahora);

        s.RegistrarCobroFallido(Ahora, 7);
        s.RegistrarCobroFallido(Ahora.AddDays(3), 7);

        Assert.Equal(Ahora.AddDays(7), s.GraciaHasta);
        Assert.False(s.TerminarGracia(Ahora.AddDays(6)));
        Assert.True(s.TerminarGracia(Ahora.AddDays(7)));
        Assert.Equal(EstadoSuscripcion.Cancelada, s.Estado);
        Assert.Empty(s.GruposPro);
    }

    [Fact]
    public void RN087_Firma_SoloValeConElSecretoYDentroDeLaTolerancia()
    {
        var cuerpo = Encoding.UTF8.GetBytes("""{"id":"evt_1"}""");
        var firma = FirmaPagos.Firmar(cuerpo, "whsec_x", Ahora);

        Assert.True(FirmaPagos.Valida(cuerpo, firma, "whsec_x", Ahora.AddSeconds(100), 300));
        Assert.True(FirmaPagos.Valida(cuerpo, $"{firma},v1=00", "whsec_x", Ahora, 300));
        Assert.False(FirmaPagos.Valida(cuerpo, firma, "whsec_y", Ahora, 300));
        Assert.False(FirmaPagos.Valida(cuerpo, firma, "whsec_x", Ahora.AddSeconds(301), 300));
        Assert.False(FirmaPagos.Valida([.. cuerpo, 32], firma, "whsec_x", Ahora, 300));
        Assert.False(FirmaPagos.Valida(cuerpo, null, "whsec_x", Ahora, 300));
    }

    [Fact]
    public void SPEC08_EventosDeStripe_SeTraducenConDocenteYGrupos()
    {
        var docente = Guid.NewGuid();
        var grupos = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var suscripcion = Evento("customer.subscription.updated", $$$"""
            {"id":"sub_1","customer":"cus_1","status":"active","metadata":{"docente_id":"{{{docente}}}","grupos_0":"{{{string.Join(",", grupos)}}}"},
             "items":{"data":[{"current_period_end":1793000000}]}}
            """);
        var oxxo = Evento("checkout.session.async_payment_succeeded", $$$"""
            {"id":"cs_1","mode":"payment","metadata":{"docente_id":"{{{docente}}}"}}
            """);
        var factura = Evento("invoice.payment_failed", $$$"""
            {"id":"in_1","parent":{"subscription_details":{"metadata":{"docente_id":"{{{docente}}}"} } } }
            """);
        var ajeno = Evento("customer.subscription.updated", """{"id":"sub_2","status":"active","metadata":{}}""");

        var s = PasarelaStripe.Traducir(suscripcion)!;
        Assert.Equal(TipoEventoPago.SuscripcionActualizada, s.Tipo);
        Assert.Equal(docente, s.DocenteId);
        Assert.Equal(grupos, s.Grupos);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1793000000), s.PeriodoFin);
        Assert.Equal(TipoEventoPago.PagoUnicoPagado, PasarelaStripe.Traducir(oxxo)!.Tipo);
        Assert.Equal("cs_1", PasarelaStripe.Traducir(oxxo)!.Sesion);
        Assert.Equal(TipoEventoPago.CobroFallido, PasarelaStripe.Traducir(factura)!.Tipo);
        Assert.Null(PasarelaStripe.Traducir(ajeno));
    }

    private static JsonElement Evento(string tipo, string objeto) =>
        JsonDocument.Parse($$$"""{"id":"evt_{{{Guid.NewGuid():N}}}","type":"{{{tipo}}}","data":{"object":{{{objeto}}} } }""").RootElement;
}
