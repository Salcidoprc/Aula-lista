using AulaLista.Application.Cierre;
using AulaLista.Domain.Cierre;
using AulaLista.Domain.Cuentas;
using AulaLista.Domain.Grupos;
using AulaLista.Infrastructure.Ia;

namespace AulaLista.Pruebas.Unit.Cierre;

/// <summary>SPEC-04: seudonimización del envío a la IA (RN-019, RNF-007), ciclo de la observación y salida de la IA.</summary>
public sealed class ObservacionesPruebas
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 20, 20, 0, 0, TimeSpan.Zero);

    private static (Grupo Grupo, IReadOnlyList<Alumno> Alumnos) Grupo()
    {
        var ciclo = CicloEscolar.Crear(Guid.CreateVersion7(), "2026-2027", new DateOnly(2026, 8, 25), new DateOnly(2027, 7, 16), TipoUnidad.Trimestre, null, Ahora).Valor;
        var grupo = Domain.Grupos.Grupo.Crear(ciclo, "B", NivelEducativo.Primaria, 4, null, Ahora).Valor;
        var alumnos = Alumno.Inscribir(grupo, [], [("Ana Sofía", "López Ríos"), ("Mateo", "Cruz Álvarez"), ("Regina", "Hernández Paz")], Ahora);
        return (grupo, alumnos);
    }

    private static EntradaObservacion Entrada(string alumno, IReadOnlyList<NotaParaIa> notas, IReadOnlyList<string> ejemplos, string? instruccion) => new(
        alumno, Trato.Ella, NivelEducativo.Primaria, 4, null, "Trimestre 1", 8.4m, 6m,
        [new RubroParaIa("Tareas", 30m, 9.1m, 1), new RubroParaIa("Proyecto", 40m, 8m, 0)],
        notas, new EstiloParaIa(LongitudObservacion.Normal, PersonaObservacion.Tercera, ejemplos), instruccion);

    [Fact]
    public void SPEC04_EnvioALaIA_NoIncluyeNombresApellidosTelefonosNiCorreos()
    {
        var (_, alumnos) = Grupo();
        var ana = alumnos.Single(a => a.Nombres == "Ana Sofía");
        var limpiador = new Seudonimizador(ana, ana.Seudonimo, alumnos, ["Laura Ríos Treviño"]);
        string[] notas =
        [
            "Ana Sofía explicó la fracción a Mateo y a regina.",
            "Su mamá, Laura, pidió cita; su teléfono es 667 123 4567 y su correo laura.rios@correo.mx.",
            "LÓPEZ entregó tarde; Hernandez la ayudó (+52 1 667 765 4321).",
            "Ana trabajó con Cruz Álvarez en el proyecto.",
        ];
        var entrada = Entrada(
            ana.Seudonimo,
            notas.Select(n => new NotaParaIa(new DateOnly(2026, 9, 18), "fortaleza", limpiador.Limpiar(n))).ToList(),
            [limpiador.Limpiar("Mateo mostró mucho avance; llama al 6671112233.")],
            limpiador.Limpiar("menciona que Regina la apoyó"));

        var enviado = string.Join('\n', PromptObservacion.Sistema, PromptObservacion.Ejemplos(entrada.Estilo), PromptObservacion.Usuario(entrada));

        string[] prohibidos =
        [
            "Ana", "Sofía", "Sofia", "López", "Lopez", "Ríos", "Mateo", "Cruz", "Álvarez", "Alvarez", "Regina", "Hernández",
            "Hernandez", "Paz", "Laura", "Treviño", "667", "4567", "4321", "1112233", "laura.rios", "@correo",
        ];
        foreach (var prohibido in prohibidos)
        {
            Assert.DoesNotContain(prohibido, enviado, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains(ana.Seudonimo, enviado, StringComparison.Ordinal);
        Assert.Contains(Seudonimizador.OtroAlumno, enviado, StringComparison.Ordinal);
        Assert.Contains(Seudonimizador.Familia, enviado, StringComparison.Ordinal);
        Assert.Contains(Seudonimizador.Telefono, enviado, StringComparison.Ordinal);
        Assert.Contains(Seudonimizador.Correo, enviado, StringComparison.Ordinal);
    }

    [Fact]
    public void RN019_Seudonimizador_NoCambiaPalabrasQueSoloContienenUnNombre()
    {
        var (_, alumnos) = Grupo();
        var mateo = alumnos.Single(a => a.Nombres == "Mateo");

        var limpio = new Seudonimizador(mateo, mateo.Seudonimo, alumnos, []).Limpiar("Anaranjado y espacioso: Mateo pintó el mapa.");

        Assert.Equal($"Anaranjado y espacioso: {mateo.Seudonimo} pintó el mapa.", limpio);
    }

    [Fact]
    public void SPEC04_RegeneracionNumero6_SeRechazaConLimiteRegeneraciones()
    {
        var (_, alumnos) = Grupo();
        var observacion = Observacion.Nueva(alumnos[0], Guid.CreateVersion7(), Ahora);
        observacion.Encolar(Ahora);
        observacion.RegistrarBorrador(new string('a', 10), "m", "v", 1, 1, Ahora);

        for (var i = 0; i < Observacion.RegeneracionesMaximas; i++)
        {
            Assert.True(observacion.PedirRegeneracion("más breve", Ahora).EsExito);
            observacion.RegistrarBorrador($"versión {i}", "m", "v", 1, 1, Ahora);
        }

        var sexta = observacion.PedirRegeneracion(null, Ahora);

        Assert.Equal(ErroresCierre.LimiteRegeneraciones, sexta.Error);
        Assert.Equal(5, observacion.Regeneraciones);
    }

    [Fact]
    public void RN043_RN045_RegenerarReemplazaElBorradorYConservaLaPrimeraVersion()
    {
        var (_, alumnos) = Grupo();
        var observacion = Observacion.Nueva(alumnos[0], Guid.CreateVersion7(), Ahora);
        observacion.Encolar(Ahora);
        observacion.RegistrarBorrador("primera", "m", "v", 10, 5, Ahora);
        observacion.PedirRegeneracion("más positiva", Ahora);
        observacion.RegistrarBorrador("segunda", "m", "v", 10, 5, Ahora);

        Assert.True(observacion.Aprobar("segunda, corregida", Ahora).EsExito);

        Assert.Equal("primera", observacion.BorradorIa);
        Assert.Equal("segunda, corregida", observacion.TextoFinal);
        Assert.Equal(20, observacion.TokensEntrada);
        Assert.Equal(ErroresCierre.ObservacionAprobada, observacion.PedirRegeneracion(null, Ahora).Error);
    }

    [Fact]
    public void RN042_UnaObservacionSinTextoNoSeAprueba()
    {
        var (_, alumnos) = Grupo();
        var observacion = Observacion.Nueva(alumnos[0], Guid.CreateVersion7(), Ahora);

        Assert.Equal(ErroresCierre.ObservacionVacia, observacion.Aprobar(null, Ahora).Error);
        Assert.Equal(EstadoObservacion.SinGenerar, observacion.Estado);
    }

    [Fact]
    public void SPEC04_FallaDeLaIA_SinTextoPrevioQuedaSinGenerar_ConTextoConservaElBorrador()
    {
        var (_, alumnos) = Grupo();
        var nueva = Observacion.Nueva(alumnos[0], Guid.CreateVersion7(), Ahora);
        nueva.Encolar(Ahora);
        nueva.RegistrarFallo(FalloGeneracion.Rechazo, "m", "v", 3, 0, Ahora);

        var conTexto = Observacion.Nueva(alumnos[1], Guid.CreateVersion7(), Ahora);
        conTexto.Encolar(Ahora);
        conTexto.RegistrarBorrador("borrador", "m", "v", 1, 1, Ahora);
        conTexto.PedirRegeneracion(null, Ahora);
        conTexto.RegistrarFallo(FalloGeneracion.Error, "m", "v", 1, 0, Ahora);

        Assert.Equal((EstadoObservacion.SinGenerar, FalloGeneracion.Rechazo), (nueva.Estado, nueva.Fallo));
        Assert.Equal((EstadoObservacion.Borrador, "borrador", FalloGeneracion.Error), (conTexto.Estado, conTexto.Texto, conTexto.Fallo));
    }

    [Fact]
    public void SPEC04_SalidaDeLaIA_CambiaElSeudonimoPorElNombreYQuitaAdornos()
    {
        var encargo = Encargo(LongitudObservacion.Breve);
        var texto = "**Alumno 7F3K** mostró constancia en las tareas y explicó con claridad sus ideas al equipo durante el proyecto. "
            + "Para el siguiente periodo, conviene que el alumno 7F3K entregue a tiempo cada actividad y repase en casa.";

        var terminado = RedactarObservacionesTrabajo.Terminar(encargo, Redactada(texto));

        Assert.Null(terminado.Fallo);
        Assert.StartsWith("Ana mostró constancia", terminado.Texto, StringComparison.Ordinal);
        Assert.Contains("conviene que Ana entregue", terminado.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("7F3K", terminado.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("*", terminado.Texto, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(LongitudObservacion.Breve, 19, FalloGeneracion.Longitud)]
    [InlineData(LongitudObservacion.Breve, 20, null)]
    [InlineData(LongitudObservacion.Normal, 112, null)]
    [InlineData(LongitudObservacion.Normal, 113, FalloGeneracion.Longitud)]
    [InlineData(LongitudObservacion.Detallada, 175, null)]
    public void RN046_SalidaFueraDelRangoConHolgura_FallaPorLongitud(string longitud, int palabras, string? fallo)
    {
        var texto = string.Join(' ', Enumerable.Repeat("logro", palabras));

        Assert.Equal(fallo, RedactarObservacionesTrabajo.Terminar(Encargo(longitud), Redactada(texto)).Fallo);
    }

    [Fact]
    public void SPEC04_Rechazo_NoGuardaTexto()
    {
        var terminado = RedactarObservacionesTrabajo.Terminar(
            Encargo(LongitudObservacion.Normal), new ObservacionRedactada(null, FalloGeneracion.Rechazo, "m", "v", 1, 0, 0, 1));

        Assert.Equal(new TextoTerminado(null, FalloGeneracion.Rechazo), terminado);
    }

    private static EncargoObservacion Encargo(string longitud) => new(
        Guid.CreateVersion7(), Guid.CreateVersion7(), Entrada("Alumno 7F3K", [], [], null) with { Estilo = new EstiloParaIa(longitud, PersonaObservacion.Tercera, []) },
        "Alumno 7F3K", "Ana", longitud);

    private static ObservacionRedactada Redactada(string texto) => new(texto, null, "m", "v", 1, 1, 0, 1);
}
