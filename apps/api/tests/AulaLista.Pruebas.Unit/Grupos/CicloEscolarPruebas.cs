using AulaLista.Domain.Grupos;

namespace AulaLista.Pruebas.Unit.Grupos;

/// <summary>SPEC-02, ciclo escolar y unidades (RN-022 a RN-024, RN-026).</summary>
public sealed class CicloEscolarPruebas
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Docente = Guid.CreateVersion7();
    private static readonly DateOnly Inicio = new(2026, 8, 25);
    private static readonly DateOnly Fin = new(2027, 7, 16);

    private static CicloEscolar Crear(string tipo = TipoUnidad.Trimestre, int? cantidad = null) =>
        CicloEscolar.Crear(Docente, "2026-2027", Inicio, Fin, tipo, cantidad, Ahora).Valor;

    [Fact]
    public void Crear_GeneraIdVersion7YMarcasEnUtc()
    {
        var ciclo = Crear();

        Assert.Equal(7, ciclo.Id.Version);
        Assert.Equal(Ahora, ciclo.CreatedAt);
        Assert.Equal(Docente, ciclo.DocenteId);
    }

    [Fact]
    public void SPEC02_CicloDeAgostoAJulioPorTrimestre_ProponeTresUnidadesConsecutivasQueLoCubren()
    {
        var unidades = Crear().Unidades;

        Assert.Equal(["Trimestre 1", "Trimestre 2", "Trimestre 3"], unidades.Select(u => u.Nombre));
        Assert.Equal(Inicio, unidades[0].Inicio);
        Assert.Equal(Fin, unidades[^1].Fin);
        for (var i = 1; i < unidades.Count; i++)
        {
            Assert.Equal(unidades[i - 1].Fin.AddDays(1), unidades[i].Inicio);
        }

        Assert.Equal([1, 2, 3], unidades.Select(u => u.Orden));
    }

    [Theory]
    [InlineData(TipoUnidad.Bimestre, 5)]
    [InlineData(TipoUnidad.Trimestre, 3)]
    [InlineData(TipoUnidad.Semestre, 2)]
    [InlineData(TipoUnidad.Mensual, 10)]
    [InlineData(TipoUnidad.Personalizada, 1)]
    public void RN024_CantidadPropuesta_SegunLaDuracion(string tipo, int esperada) =>
        Assert.Equal(esperada, CicloEscolar.CantidadPropuesta(Inicio, Fin, tipo));

    [Fact]
    public void RN024_ElDocenteEligeLaCantidad()
    {
        var ciclo = Crear(TipoUnidad.Personalizada, 4);

        Assert.Equal(["Unidad 1", "Unidad 2", "Unidad 3", "Unidad 4"], ciclo.Unidades.Select(u => u.Nombre));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void RN024_CantidadFueraDeUnoADoce_SeRechaza(int cantidad) =>
        Assert.Equal(
            ErroresGrupos.CantidadUnidadesInvalida,
            CicloEscolar.Crear(Docente, "2026-2027", Inicio, Fin, TipoUnidad.Mensual, cantidad, Ahora).Error);

    [Fact]
    public void RN022_FinAntesDelInicio_SeRechaza() =>
        Assert.Equal(
            ErroresGrupos.CicloFechasInvalidas,
            CicloEscolar.Crear(Docente, "2026-2027", Fin, Inicio, TipoUnidad.Trimestre, null, Ahora).Error);

    [Fact]
    public void RN022_MasDeTreceMeses_SeRechaza() =>
        Assert.Equal(
            ErroresGrupos.CicloMuyLargo,
            CicloEscolar.Crear(Docente, "2026-2027", Inicio, Inicio.AddMonths(13).AddDays(1), TipoUnidad.Trimestre, null, Ahora).Error);

    [Fact]
    public void SPEC02_UnidadesQueSeTraslapan_SeRechazanConUnidadesTraslapadas()
    {
        var ciclo = Crear();
        var u = ciclo.Unidades;
        var propuestas = new[]
        {
            new UnidadPropuesta(u[0].Id, u[0].Nombre, u[0].Inicio, u[1].Inicio),
            new UnidadPropuesta(u[1].Id, u[1].Nombre, u[1].Inicio, u[1].Fin),
            new UnidadPropuesta(u[2].Id, u[2].Nombre, u[2].Inicio, u[2].Fin),
        };

        var resultado = ciclo.ReemplazarUnidades(propuestas, new HashSet<Guid>(), new HashSet<Guid>(), Ahora);

        Assert.Equal(ErroresGrupos.UnidadesTraslapadas, resultado.Error);
    }

    [Fact]
    public void RN023_HuecosEntreUnidades_SePermitenYSeOrdenanPorFecha()
    {
        var ciclo = Crear();
        var u = ciclo.Unidades;
        var propuestas = new[]
        {
            new UnidadPropuesta(u[2].Id, "Tercera", u[2].Inicio.AddDays(10), u[2].Fin),
            new UnidadPropuesta(u[0].Id, "Primera", u[0].Inicio, u[0].Fin.AddDays(-5)),
            new UnidadPropuesta(u[1].Id, "Segunda", u[1].Inicio, u[1].Fin),
        };

        var resultado = ciclo.ReemplazarUnidades(propuestas, new HashSet<Guid>(), new HashSet<Guid>(), Ahora);

        Assert.True(resultado.EsExito);
        Assert.Equal(["Primera", "Segunda", "Tercera"], ciclo.Unidades.OrderBy(x => x.Orden).Select(x => x.Nombre));
    }

    [Fact]
    public void RN023_UnidadFueraDelCiclo_SeRechaza()
    {
        var ciclo = Crear();
        var u = ciclo.Unidades;
        var propuestas = u.Select(x => new UnidadPropuesta(x.Id, x.Nombre, x.Inicio, x.Fin)).ToList();
        propuestas[^1] = propuestas[^1] with { Fin = Fin.AddDays(1) };

        Assert.Equal(ErroresGrupos.UnidadFueraDeCiclo, ciclo.ReemplazarUnidades(propuestas, new HashSet<Guid>(), new HashSet<Guid>(), Ahora).Error);
    }

    [Fact]
    public void SPEC02_UnidadCerradaEnUnGrupo_NoCambiaDeFechas()
    {
        var ciclo = Crear();
        var u = ciclo.Unidades;
        var propuestas = u.Select(x => new UnidadPropuesta(x.Id, x.Nombre, x.Inicio, x.Fin)).ToList();
        propuestas[0] = propuestas[0] with { Fin = propuestas[0].Fin.AddDays(-1) };

        var resultado = ciclo.ReemplazarUnidades(propuestas, new HashSet<Guid> { u[0].Id }, new HashSet<Guid>(), Ahora);

        Assert.Equal(ErroresGrupos.UnidadCerrada, resultado.Error);
    }

    [Fact]
    public void RN027_AgregarUnidadAlFinal_LaDevuelveComoNueva()
    {
        var ciclo = Crear();
        var u = ciclo.Unidades;
        var propuestas = u.Select(x => new UnidadPropuesta(x.Id, x.Nombre, x.Inicio, x.Fin)).ToList();
        propuestas[^1] = propuestas[^1] with { Fin = propuestas[^1].Fin.AddDays(-20) };
        propuestas.Add(new UnidadPropuesta(null, "Recuperación", Fin.AddDays(-19), Fin));

        var resultado = ciclo.ReemplazarUnidades(propuestas, new HashSet<Guid>(), new HashSet<Guid>(), Ahora);

        Assert.True(resultado.EsExito);
        Assert.Equal("Recuperación", Assert.Single(resultado.Valor).Nombre);
        Assert.Equal(4, ciclo.Unidades.Count);
    }

    [Fact]
    public void RN022_EditarFechasQueDejanUnidadesFuera_SeRechaza() =>
        Assert.Equal(ErroresGrupos.UnidadFueraDeCiclo, Crear().Editar("2026-2027", Inicio.AddDays(1), Fin, Ahora).Error);
}
