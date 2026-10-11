using AulaLista.Domain.Cuentas;
using AulaLista.Domain.Grupos;

namespace AulaLista.Pruebas.Unit.Grupos;

/// <summary>SPEC-02: alumnos, número de lista, seudónimo y teléfonos (RN-011, RN-013, RN-014, RN-019).</summary>
public sealed class AlumnosYTutoresPruebas
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    private static Grupo NuevoGrupo()
    {
        var ciclo = CicloEscolar.Crear(Guid.CreateVersion7(), "2026-2027", new DateOnly(2026, 8, 25), new DateOnly(2027, 7, 16), TipoUnidad.Trimestre, null, Ahora).Valor;
        return Grupo.Crear(ciclo, "A", NivelEducativo.Primaria, 4, null, Ahora).Valor;
    }

    [Theory]
    [InlineData("6671234567", "+526671234567")]
    [InlineData("667 123 4567", "+526671234567")]
    [InlineData("(667) 123-45-67", "+526671234567")]
    [InlineData("+52 667 123 4567", "+526671234567")]
    [InlineData("+52 1 667 123 4567", "+526671234567")]
    [InlineData("526671234567", "+526671234567")]
    [InlineData("044 667 123 4567", "+526671234567")]
    [InlineData("+1 415 555 0100", "+14155550100")]
    public void RN014_Telefono_SeNormalizaAE164(string texto, string esperado) =>
        Assert.Equal(esperado, Telefono.Normalizar(texto));

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("667123456")]
    [InlineData("+52 667 123 456")]
    [InlineData("tel 6671234567")]
    [InlineData("66+71234567")]
    public void RN014_TelefonoInvalido_DevuelveNulo(string texto) =>
        Assert.Null(Telefono.Normalizar(texto));

    [Fact]
    public void RN013_ElLoteSeNumeraPorApellidosDespuesDelUltimo()
    {
        var grupo = NuevoGrupo();
        var primeros = Alumno.Inscribir(grupo, [], [("Ana", "Zamora"), ("Luis", "Álvarez")], Ahora);

        var llegan = Alumno.Inscribir(grupo, primeros, [("Sofía", "Méndez"), ("Raúl", "Beltrán")], Ahora);

        Assert.Equal([("Álvarez", 1), ("Zamora", 2)], primeros.OrderBy(a => a.NumeroLista).Select(a => (a.Apellidos, a.NumeroLista)));
        Assert.Equal([("Beltrán", 3), ("Méndez", 4)], llegan.OrderBy(a => a.NumeroLista).Select(a => (a.Apellidos, a.NumeroLista)));
        Assert.Equal(["Méndez", "Beltrán"], llegan.Select(a => a.Apellidos));
    }

    [Fact]
    public void RN011_SoloCabenLosPrimerosHastaCincuentaActivos()
    {
        var grupo = NuevoGrupo();
        var existentes = Alumno.Inscribir(grupo, [], Enumerable.Range(1, 48).Select(i => ($"N{i}", $"A{i:00}")), Ahora);

        var llegan = Alumno.Inscribir(grupo, existentes, [("X", "Primero"), ("Y", "Segundo"), ("Z", "Tercero")], Ahora);

        Assert.Equal(["Primero", "Segundo"], llegan.Select(a => a.Apellidos));
    }

    [Fact]
    public void RN011_UnaBajaNoCuentaParaElLimite()
    {
        var grupo = NuevoGrupo();
        var existentes = Alumno.Inscribir(grupo, [], Enumerable.Range(1, 50).Select(i => ($"N{i}", $"A{i:00}")), Ahora);
        existentes[0].DarDeBaja(Ahora);

        Assert.Single(Alumno.Inscribir(grupo, existentes, [("X", "Nuevo")], Ahora));
    }

    [Fact]
    public void RN013_CambiarAUnNumeroOcupado_IntercambiaLosNumeros()
    {
        var grupo = NuevoGrupo();
        var alumnos = Alumno.Inscribir(grupo, [], [("Ana", "Álvarez"), ("Luis", "Beltrán"), ("Sofía", "Castro")], Ahora);

        Assert.True(alumnos[2].CambiarNumero(1, alumnos, Ahora).EsExito);

        Assert.Equal([3, 2, 1], alumnos.Select(a => a.NumeroLista));
    }

    [Fact]
    public void RN019_CadaAlumnoTieneSeudonimoUnicoSinCaracteresAmbiguos()
    {
        var grupo = NuevoGrupo();
        var alumnos = Alumno.Inscribir(grupo, [], Enumerable.Range(1, 50).Select(i => ($"N{i}", $"A{i:00}")), Ahora);

        Assert.Equal(50, alumnos.Select(a => a.Seudonimo).Distinct().Count());
        Assert.All(alumnos, a => Assert.Matches("^Alumno [2-9A-HJKMNP-Z]{4}$", a.Seudonimo));
    }

    [Fact]
    public void RN018_GrupoArchivado_NoSeEdita()
    {
        var grupo = NuevoGrupo();
        grupo.Archivar(Ahora);

        Assert.Equal(ErroresGrupos.GrupoArchivado, grupo.Editar("B", 4, null, ConfiguracionCalificacion.PorDefecto, Ahora).Error);
    }

    [Theory]
    [InlineData(6, 5, 2, Redondeo.Cercano)]
    [InlineData(-1, 6, 2, Redondeo.Cercano)]
    [InlineData(5, 6, 3, Redondeo.Cercano)]
    [InlineData(5, 6, 2, "hacia_el_cero")]
    [InlineData(5.555, 6, 2, Redondeo.Cercano)]
    public void RN030_ConfiguracionInvalida_SeRechaza(double minima, double aprobatoria, int decimales, string redondeo)
    {
        var configuracion = new ConfiguracionCalificacion((decimal)minima, (decimal)aprobatoria, decimales, redondeo);

        Assert.Equal(ErroresGrupos.ConfiguracionCalificacionInvalida, NuevoGrupo().Editar("A", 4, null, configuracion, Ahora).Error);
    }

    [Fact]
    public void RF010_EnPrimariaNoHayMateriaYElGradoVaDeUnoASeis()
    {
        var ciclo = CicloEscolar.Crear(Guid.CreateVersion7(), "2026-2027", new DateOnly(2026, 8, 25), new DateOnly(2027, 7, 16), TipoUnidad.Trimestre, null, Ahora).Valor;

        Assert.Null(Grupo.Crear(ciclo, "A", NivelEducativo.Primaria, 6, "Matemáticas", Ahora).Valor.Materia);
        Assert.Equal(ErroresGrupos.GradoInvalido, Grupo.Crear(ciclo, "A", NivelEducativo.Secundaria, 4, "Historia", Ahora).Error);
        Assert.Equal("Historia", Grupo.Crear(ciclo, "A", NivelEducativo.Secundaria, 3, " Historia ", Ahora).Valor.Materia);
    }
}
