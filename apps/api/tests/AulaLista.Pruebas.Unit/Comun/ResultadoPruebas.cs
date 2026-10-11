using AulaLista.Domain.Comun;

namespace AulaLista.Pruebas.Unit.Comun;

public sealed class ResultadoPruebas
{
    [Fact]
    public void Exito_ExponeValorYNoTieneError()
    {
        var resultado = Resultado.Exito(42m);

        Assert.True(resultado.EsExito);
        Assert.Equal(42m, resultado.Valor);
        Assert.Equal(ErrorNegocio.Ninguno, resultado.Error);
    }

    [Fact]
    public void Fallo_ConservaCodigoYNoExponeValor()
    {
        var error = new ErrorNegocio("pesos_no_suman_100", "Los pesos deben sumar 100.");

        var resultado = Resultado.Fallo<decimal>(error);

        Assert.True(resultado.EsFallo);
        Assert.Equal("pesos_no_suman_100", resultado.Error.Codigo);
        Assert.Throws<InvalidOperationException>(() => resultado.Valor);
    }
}
