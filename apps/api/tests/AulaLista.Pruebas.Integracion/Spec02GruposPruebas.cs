using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Npgsql;

namespace AulaLista.Pruebas.Integracion;

/// <summary>Criterios de aceptación de SPEC-02 (grupos, alumnos, tutores, ciclo y unidades).</summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class Spec02GruposPruebas(BaseDeDatosFixture baseDeDatos) : IDisposable
{
    private static readonly string[] CuatroGrupos = ["A", "B", "C", "D"];
    private static readonly string[] TresGrupos = ["A", "B", "C"];
    private readonly FabricaApi _fabrica = new(baseDeDatos);

    public void Dispose() => _fabrica.Dispose();

    [Fact]
    public async Task SPEC02_CicloDeAgostoAJulioPorTrimestre_ProponeTresUnidadesEditables()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/ciclos", new { nombre = "2026-2027", inicio = "2026-08-25", fin = "2027-07-16", tipoUnidad = "trimestre" });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var ciclo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var unidades = ciclo.GetProperty("unidades").EnumerateArray().ToList();
        Assert.Equal(3, unidades.Count);
        Assert.Equal("2026-08-25", unidades[0].GetProperty("inicio").GetString());
        Assert.Equal("2027-07-16", unidades[2].GetProperty("fin").GetString());
        for (var i = 1; i < unidades.Count; i++)
        {
            var finAnterior = DateOnly.Parse(unidades[i - 1].GetProperty("fin").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(finAnterior.AddDays(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), unidades[i].GetProperty("inicio").GetString());
        }

        var editadas = unidades.Select((u, i) => new
        {
            id = u.GetProperty("id").GetGuid(),
            nombre = $"Trimestre {(char)('A' + i)}",
            inicio = u.GetProperty("inicio").GetString(),
            fin = u.GetProperty("fin").GetString(),
        });
        var edicion = await cliente.PutAsJsonAsync($"/api/v1/ciclos/{ciclo.GetProperty("id").GetGuid()}/unidades", new { unidades = editadas });
        Assert.Equal(HttpStatusCode.OK, edicion.StatusCode);
        Assert.Equal("Trimestre A", (await edicion.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("unidades")[0].GetProperty("nombre").GetString());
    }

    [Fact]
    public async Task SPEC02_CambiarFinDeUnidadUno_SeReflejaEnLosCuatroGrupos()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var ciclo = await CrearCicloAsync(cliente);
        var lote = await cliente.PostAsJsonAsync("/api/v1/grupos/lote", new { nivel = "secundaria", grado = 1, materia = "Matemáticas", cicloId = ciclo.Id, nombres = CuatroGrupos });
        Assert.Equal(HttpStatusCode.Created, lote.StatusCode);
        var grupos = (await lote.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Select(g => g.GetProperty("id").GetGuid()).ToList();

        var unidades = ciclo.Unidades.ToList();
        var nuevoFin = new DateOnly(2026, 11, 20);
        unidades[0] = unidades[0] with { Fin = nuevoFin };
        var respuesta = await cliente.PutAsJsonAsync($"/api/v1/ciclos/{ciclo.Id}/unidades", new { unidades });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(4, grupos.Count);
        foreach (var grupo in grupos)
        {
            var periodos = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{grupo}/periodos");
            Assert.Equal(3, periodos.GetArrayLength());
            Assert.Equal("2026-11-20", periodos[0].GetProperty("fin").GetString());
            Assert.Equal("abierto", periodos[0].GetProperty("estado").GetString());
        }
    }

    [Fact]
    public async Task SPEC02_UnidadesQueSeTraslapan_SeRechazanConUnidadesTraslapadas()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var ciclo = await CrearCicloAsync(cliente);
        var unidades = ciclo.Unidades.ToList();
        unidades[0] = unidades[0] with { Fin = unidades[1].Inicio.AddDays(3) };

        var respuesta = await cliente.PutAsJsonAsync($"/api/v1/ciclos/{ciclo.Id}/unidades", new { unidades });

        Assert.Equal("unidades_traslapadas", await CodigoAsync(respuesta));
    }

    [Fact]
    public async Task SPEC02_UnidadCerradaEnUnGrupo_NoCambiaDeFechas()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var ciclo = await CrearCicloAsync(cliente);
        var grupo = await CrearGrupoAsync(cliente, ciclo.Id);
        await EjecutarSqlAsync($"UPDATE periodo SET estado = 'cerrado' WHERE grupo_id = '{grupo}' AND unidad_id = '{ciclo.Unidades[0].Id}'");
        var unidades = ciclo.Unidades.ToList();
        unidades[0] = unidades[0] with { Fin = unidades[0].Fin.AddDays(-2) };

        var respuesta = await cliente.PutAsJsonAsync($"/api/v1/ciclos/{ciclo.Id}/unidades", new { unidades });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("unidad_cerrada", await CodigoAsync(respuesta));
    }

    [Fact]
    public async Task RN027_AgregarUnidadAlFinal_CreaSuPeriodoEnCadaGrupo()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var ciclo = await CrearCicloAsync(cliente);
        var grupo = await CrearGrupoAsync(cliente, ciclo.Id);
        var unidades = ciclo.Unidades.Select(u => (object)u).ToList();
        var ultima = ciclo.Unidades[^1];
        unidades[^1] = ultima with { Fin = ultima.Fin.AddDays(-15) };
        unidades.Add(new { nombre = "Recuperación", inicio = ultima.Fin.AddDays(-14), fin = ultima.Fin });

        var respuesta = await cliente.PutAsJsonAsync($"/api/v1/ciclos/{ciclo.Id}/unidades", new { unidades });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var periodos = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{grupo}/periodos");
        Assert.Equal("Recuperación", periodos[3].GetProperty("nombre").GetString());
    }

    [Fact]
    public async Task SPEC02_ExcelCon35AlumnosYSusTutores_QuedanConTelefonosE164()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var grupo = await CrearGrupoAsync(cliente, (await CrearCicloAsync(cliente)).Id);
        var excel = ExcelDeAlumnos(35);

        var vista = await SubirAsync(cliente, grupo, excel, "lista 4B.xlsx");
        Assert.Equal(
            ["apellidos", "nombres", "tutor_nombre_1", "tutor_telefono_1", "tutor_correo_1", "tutor_parentesco_1"],
            vista.GetProperty("mapeo").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal(35, vista.GetProperty("filas").GetArrayLength());
        Assert.Equal(50, vista.GetProperty("disponibles").GetInt32());

        var filas = vista.GetProperty("filas").EnumerateArray().Select(f => new
        {
            nombres = f[1].GetString(),
            apellidos = f[0].GetString(),
            tutores = new[] { new { nombre = f[2].GetString(), telefono = f[3].GetString(), correo = f[4].GetString(), parentesco = f[5].GetString() } },
        });
        var confirmacion = await cliente.PostAsJsonAsync($"/api/v1/importaciones/{vista.GetProperty("id").GetGuid()}/confirmar", new { filas });

        var resultado = await confirmacion.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(35, resultado.GetProperty("guardados").GetInt32());
        Assert.Equal(0, resultado.GetProperty("omitidosPorError").GetInt32());
        var alumnos = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{grupo}/alumnos");
        Assert.Equal(35, alumnos.GetArrayLength());
        Assert.All(alumnos.EnumerateArray(), a =>
        {
            var tutor = Assert.Single(a.GetProperty("tutores").EnumerateArray());
            Assert.Matches(@"^\+52\d{10}$", tutor.GetProperty("telefono").GetString());
        });
        Assert.Equal(Enumerable.Range(1, 35), alumnos.EnumerateArray().Select(a => a.GetProperty("numeroLista").GetInt32()));
        Assert.Equal("Apellido01 Uno", alumnos[0].GetProperty("apellidos").GetString());

        var otraVez = await cliente.PostAsJsonAsync($"/api/v1/importaciones/{vista.GetProperty("id").GetGuid()}/confirmar", new { filas });
        Assert.Equal("importacion_confirmada", await CodigoAsync(otraVez));
    }

    [Fact]
    public async Task RF013_ConfirmarConFilasInvalidas_GuardaSoloLasValidasEInformaLasOmitidas()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var grupo = await CrearGrupoAsync(cliente, (await CrearCicloAsync(cliente)).Id);
        var vista = await PegarAsync(cliente, grupo, "Uno\nDos\nTres");
        var filas = new object[]
        {
            new { nombres = "Ana", apellidos = "Ruiz" },
            new { nombres = " ", apellidos = "Sin nombre" },
            new { nombres = "Luis", apellidos = "Soto", tutores = new[] { new { nombre = "Papá", telefono = "123" } } },
            new { nombres = "Eva", apellidos = "Paz", tutores = new[] { new { nombre = (string?)null, telefono = "6671234567" } } },
        };

        var respuesta = await cliente.PostAsJsonAsync($"/api/v1/importaciones/{vista.GetProperty("id").GetGuid()}/confirmar", new { filas });

        var resultado = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, resultado.GetProperty("guardados").GetInt32());
        Assert.Equal(3, resultado.GetProperty("omitidosPorError").GetInt32());
        Assert.Equal(
            [(2, "nombre_vacio"), (3, "telefono_invalido"), (4, "tutor_incompleto")],
            resultado.GetProperty("errores").EnumerateArray().Select(e => (e.GetProperty("fila").GetInt32(), e.GetProperty("codigo").GetString())));
    }

    [Fact]
    public async Task RF014_TextoPegado_DevuelveUnaColumnaPorAlumnoSinLaNumeracion()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var grupo = await CrearGrupoAsync(cliente, (await CrearCicloAsync(cliente)).Id);

        var vista = await PegarAsync(cliente, grupo, "1. García López Ana\r\n2) Pérez Soto Luis\n\n- Ruiz Paz, Eva María\n");

        Assert.Equal("texto", vista.GetProperty("origen").GetString());
        Assert.Equal(["nombre_completo"], vista.GetProperty("mapeo").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal(
            ["García López Ana", "Pérez Soto Luis", "Ruiz Paz, Eva María"],
            vista.GetProperty("filas").EnumerateArray().Select(f => f[0].GetString()));
    }

    [Fact]
    public async Task RF013_CsvDeExcelEnWindows1252ConPuntoYComa_SeLeeConAcentos()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var grupo = await CrearGrupoAsync(cliente, (await CrearCicloAsync(cliente)).Id);
        var csv = Encoding.GetEncoding(1252).GetBytes("Nombre;Apellido paterno;Apellido materno;Teléfono\r\nJosé;Núñez;\"Peña; Sr.\";667 123 4567\r\n");

        var vista = await SubirAsync(cliente, grupo, csv, "lista.csv");

        Assert.Equal(["nombres", "apellido_paterno", "apellido_materno", "tutor_telefono_1"], vista.GetProperty("mapeo").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal(["José", "Núñez", "Peña; Sr.", "667 123 4567"], vista.GetProperty("filas")[0].EnumerateArray().Select(c => c.GetString()));
    }

    [Fact]
    public async Task RF013_ArchivoQueNoEsExcelNiCsv_SeRechaza()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var grupo = await CrearGrupoAsync(cliente, (await CrearCicloAsync(cliente)).Id);

        var pdf = await SubirSinLeerAsync(cliente, grupo, "%PDF-1.7"u8.ToArray(), "lista.pdf");
        var roto = await SubirSinLeerAsync(cliente, grupo, "no es un zip"u8.ToArray(), "lista.xlsx");

        Assert.Equal("archivo_invalido", await CodigoAsync(pdf));
        Assert.Equal("archivo_ilegible", await CodigoAsync(roto));
    }

    [Fact]
    public async Task SPEC02_GrupoConCincuentaActivos_RechazaOtroConLimiteAlumnosGrupo()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var grupo = await CrearGrupoAsync(cliente, (await CrearCicloAsync(cliente)).Id);
        var vista = await PegarAsync(cliente, grupo, "x");
        var filas = Enumerable.Range(1, 52).Select(i => new { nombres = $"Nombre{i}", apellidos = $"Apellido{i:00}" });
        var importacion = await cliente.PostAsJsonAsync($"/api/v1/importaciones/{vista.GetProperty("id").GetGuid()}/confirmar", new { filas });
        var resultado = await importacion.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(50, resultado.GetProperty("guardados").GetInt32());
        Assert.Equal(2, resultado.GetProperty("omitidosPorLimite").GetInt32());

        var otro = await cliente.PostAsJsonAsync($"/api/v1/grupos/{grupo}/alumnos", new { nombres = "Uno", apellidos = "Más" });

        Assert.Equal("limite_alumnos_grupo", await CodigoAsync(otro));
    }

    [Fact]
    public async Task SPEC02_TelefonoRegistradoPorOtroDocente_SeVinculaSinVerSusDatos()
    {
        var (clienteA, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var (clienteB, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var alumnoA = await CrearAlumnoAsync(clienteA);
        var alumnoB = await CrearAlumnoAsync(clienteB);
        var telefono = $"667{Random.Shared.Next(1_000_000, 9_999_999)}";

        var deA = await clienteA.PostAsJsonAsync($"/api/v1/alumnos/{alumnoA}/tutores", new { nombre = "María López", telefono, correo = "maria@correo.mx", parentesco = "Mamá" });
        var deB = await clienteB.PostAsJsonAsync($"/api/v1/alumnos/{alumnoB}/tutores", new { nombre = "Sra. López", telefono = $"+52 {telefono}" });

        Assert.Equal(HttpStatusCode.Created, deB.StatusCode);
        var vinculoA = await deA.Content.ReadFromJsonAsync<JsonElement>();
        var vinculoB = await deB.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(vinculoA.GetProperty("yaRegistrado").GetBoolean());
        Assert.True(vinculoB.GetProperty("yaRegistrado").GetBoolean());
        Assert.Equal(vinculoA.GetProperty("tutor").GetProperty("id").GetGuid(), vinculoB.GetProperty("tutor").GetProperty("id").GetGuid());

        var tutoresB = await clienteB.GetStringAsync($"/api/v1/alumnos/{alumnoB}/tutores");
        Assert.Contains("Sra. López", tutoresB, StringComparison.Ordinal);
        Assert.DoesNotContain("María", tutoresB, StringComparison.Ordinal);
        Assert.DoesNotContain("maria@correo.mx", tutoresB, StringComparison.Ordinal);
        Assert.DoesNotContain("Mamá", tutoresB, StringComparison.Ordinal);
        var tutoresA = await clienteA.GetFromJsonAsync<JsonElement>($"/api/v1/alumnos/{alumnoA}/tutores");
        Assert.Equal("María López", Assert.Single(tutoresA.EnumerateArray()).GetProperty("nombre").GetString());
    }

    [Fact]
    public async Task SPEC02_HermanosConElMismoTelefono_UnSoloTutorYSinAviso()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var grupo = await CrearGrupoAsync(cliente, (await CrearCicloAsync(cliente)).Id);
        var uno = await AgregarAlumnoAsync(cliente, grupo, "Ana", "Ruiz");
        var dos = await AgregarAlumnoAsync(cliente, grupo, "Luis", "Ruiz");
        var telefono = $"667{Random.Shared.Next(1_000_000, 9_999_999)}";

        var primero = await (await cliente.PostAsJsonAsync($"/api/v1/alumnos/{uno}/tutores", new { nombre = "Papá Ruiz", telefono })).Content.ReadFromJsonAsync<JsonElement>();
        var segundo = await (await cliente.PostAsJsonAsync($"/api/v1/alumnos/{dos}/tutores", new { nombre = "Papá Ruiz", telefono })).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(primero.GetProperty("tutor").GetProperty("id").GetGuid(), segundo.GetProperty("tutor").GetProperty("id").GetGuid());
        Assert.False(segundo.GetProperty("yaRegistrado").GetBoolean());
    }

    [Fact]
    public async Task RN016_CuartoTutor_SeRechaza()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var alumno = await CrearAlumnoAsync(cliente);
        for (var i = 0; i < 3; i++)
        {
            var ok = await cliente.PostAsJsonAsync($"/api/v1/alumnos/{alumno}/tutores", new { nombre = $"Tutor {i}", telefono = $"667{Random.Shared.Next(1_000_000, 9_999_999)}" });
            Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        }

        var cuarto = await cliente.PostAsJsonAsync($"/api/v1/alumnos/{alumno}/tutores", new { nombre = "Otro", telefono = "6670000000" });

        Assert.Equal("limite_tutores_alumno", await CodigoAsync(cuarto));
    }

    [Fact]
    public async Task RN017_AlumnoDadoDeBaja_ConservaSuHistorialYLiberaLugar()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var grupo = await CrearGrupoAsync(cliente, (await CrearCicloAsync(cliente)).Id);
        var alumno = await AgregarAlumnoAsync(cliente, grupo, "Ana", "Ruiz");

        var baja = await cliente.DeleteAsync($"/api/v1/alumnos/{alumno}");

        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);
        var ficha = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/alumnos/{alumno}");
        Assert.NotEqual(JsonValueKind.Null, ficha.GetProperty("bajaEn").ValueKind);
        var resumen = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{grupo}");
        Assert.Equal(0, resumen.GetProperty("alumnosActivos").GetInt32());
        var editar = await cliente.PutAsJsonAsync($"/api/v1/alumnos/{alumno}", new { nombres = "Ana", apellidos = "Ruiz", trato = "ella", numeroLista = 1 });
        Assert.Equal("alumno_dado_de_baja", await CodigoAsync(editar));
    }

    [Fact]
    public async Task RN013_CambiarNumeroDeLista_IntercambiaConQuienLoTenia()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var grupo = await CrearGrupoAsync(cliente, (await CrearCicloAsync(cliente)).Id);
        var ana = await AgregarAlumnoAsync(cliente, grupo, "Ana", "Álvarez");
        var luis = await AgregarAlumnoAsync(cliente, grupo, "Luis", "Beltrán");

        var respuesta = await cliente.PutAsJsonAsync($"/api/v1/alumnos/{luis}", new { nombres = "Luis", apellidos = "Beltrán", trato = "el", numeroLista = 1 });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var alumnos = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{grupo}/alumnos");
        Assert.Equal([(luis, 1), (ana, 2)], alumnos.EnumerateArray().Select(a => (a.GetProperty("id").GetGuid(), a.GetProperty("numeroLista").GetInt32())));
        Assert.Equal("el", alumnos[0].GetProperty("trato").GetString());
    }

    [Fact]
    public async Task RN021_LoteConUnNombreRepetido_NoCreaNinguno()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var ciclo = await CrearCicloAsync(cliente);
        await CrearGrupoAsync(cliente, ciclo.Id, "C");

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/grupos/lote", new { nivel = "secundaria", grado = 2, materia = "Matemáticas", cicloId = ciclo.Id, nombres = TresGrupos });

        Assert.Equal("grupo_duplicado", await CodigoAsync(respuesta));
        Assert.Equal(1, (await cliente.GetFromJsonAsync<JsonElement>("/api/v1/grupos")).GetArrayLength());
    }

    [Fact]
    public async Task RN021_MismoNombreEnOtroGrado_SePermiteYEnElMismoGradoNo()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var ciclo = await CrearCicloAsync(cliente);
        await CrearGrupoAsync(cliente, ciclo.Id, "A");

        var otroGrado = await cliente.PostAsJsonAsync("/api/v1/grupos", new { nombre = "A", nivel = "secundaria", grado = 3, materia = "Matemáticas", cicloId = ciclo.Id });
        var mismoGrado = await cliente.PostAsJsonAsync("/api/v1/grupos", new { nombre = "a", nivel = "secundaria", grado = 2, materia = "matemáticas", cicloId = ciclo.Id });

        Assert.Equal(HttpStatusCode.Created, otroGrado.StatusCode);
        Assert.Equal("grupo_duplicado", await CodigoAsync(mismoGrado));
        Assert.Equal(2, (await cliente.GetFromJsonAsync<JsonElement>("/api/v1/grupos")).GetArrayLength());
    }

    [Fact]
    public async Task RN018_GrupoArchivado_SeConsultaPeroNoSeEdita()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var grupo = await CrearGrupoAsync(cliente, (await CrearCicloAsync(cliente)).Id);

        Assert.Equal(HttpStatusCode.NoContent, (await cliente.PostAsync($"/api/v1/grupos/{grupo}/archivar", null)).StatusCode);

        Assert.Equal(0, (await cliente.GetFromJsonAsync<JsonElement>("/api/v1/grupos")).GetArrayLength());
        Assert.Equal(1, (await cliente.GetFromJsonAsync<JsonElement>("/api/v1/grupos?archivados=true")).GetArrayLength());
        var alta = await cliente.PostAsJsonAsync($"/api/v1/grupos/{grupo}/alumnos", new { nombres = "Ana" });
        Assert.Equal("grupo_archivado", await CodigoAsync(alta));
        var edicion = await cliente.PutAsJsonAsync($"/api/v1/grupos/{grupo}", new { nombre = "B", grado = 4, califMinima = 5, califAprobatoria = 6, decimalesFinal = 1, redondeo = "arriba" });
        Assert.Equal("grupo_archivado", await CodigoAsync(edicion));
    }

    [Fact]
    public async Task RN035_ConfiguracionDeCalificacion_SeGuardaYSeValida()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var grupo = await CrearGrupoAsync(cliente, (await CrearCicloAsync(cliente)).Id);

        var valida = await cliente.PutAsJsonAsync($"/api/v1/grupos/{grupo}", new { nombre = "A", grado = 2, materia = "Matemáticas", califMinima = 6, califAprobatoria = 7, decimalesFinal = 1, redondeo = "abajo" });
        var invalida = await cliente.PutAsJsonAsync($"/api/v1/grupos/{grupo}", new { nombre = "A", grado = 2, materia = "Matemáticas", califMinima = 8, califAprobatoria = 7, decimalesFinal = 1, redondeo = "abajo" });

        var guardado = await valida.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(6m, guardado.GetProperty("califMinima").GetDecimal());
        Assert.Equal("abajo", guardado.GetProperty("redondeo").GetString());
        Assert.Equal("configuracion_calificacion_invalida", await CodigoAsync(invalida));
    }

    [Fact]
    public async Task RNF001_RecursosDeOtroDocente_Responden404()
    {
        var (clienteA, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var (intruso, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var ciclo = await CrearCicloAsync(clienteA);
        var grupo = await CrearGrupoAsync(clienteA, ciclo.Id);
        var alumno = await AgregarAlumnoAsync(clienteA, grupo, "Ana", "Ruiz");
        var vista = await PegarAsync(clienteA, grupo, "Uno");

        var respuestas = new[]
        {
            await intruso.GetAsync($"/api/v1/ciclos/{ciclo.Id}"),
            await intruso.PutAsJsonAsync($"/api/v1/ciclos/{ciclo.Id}/unidades", new { unidades = ciclo.Unidades }),
            await intruso.GetAsync($"/api/v1/grupos/{grupo}"),
            await intruso.GetAsync($"/api/v1/grupos/{grupo}/periodos"),
            await intruso.GetAsync($"/api/v1/grupos/{grupo}/alumnos"),
            await intruso.PostAsJsonAsync($"/api/v1/grupos/{grupo}/alumnos", new { nombres = "Intruso" }),
            await intruso.DeleteAsync($"/api/v1/grupos/{grupo}"),
            await intruso.PostAsync($"/api/v1/grupos/{grupo}/archivar", null),
            await intruso.GetAsync($"/api/v1/alumnos/{alumno}"),
            await intruso.DeleteAsync($"/api/v1/alumnos/{alumno}"),
            await intruso.PostAsJsonAsync($"/api/v1/alumnos/{alumno}/tutores", new { nombre = "X", telefono = "6671234567" }),
            await intruso.PostAsJsonAsync($"/api/v1/importaciones/{vista.GetProperty("id").GetGuid()}/confirmar", new { filas = new[] { new { nombres = "Intruso" } } }),
        };

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
        Assert.Empty((await intruso.GetFromJsonAsync<JsonElement>("/api/v1/grupos")).EnumerateArray());
        Assert.Single((await clienteA.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{grupo}/alumnos")).EnumerateArray());
        var grupoCon = await intruso.PostAsJsonAsync("/api/v1/grupos", new { nombre = "Z", nivel = "primaria", grado = 1, cicloId = ciclo.Id });
        Assert.Equal("ciclo_inexistente", await CodigoAsync(grupoCon));
    }

    [Fact]
    public async Task RNF001_RolRuntime_NoEscribeTutoresNiLeeLosDeOtroDocente()
    {
        var (cliente, docenteId, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var alumno = await CrearAlumnoAsync(cliente);
        await cliente.PostAsJsonAsync($"/api/v1/alumnos/{alumno}/tutores", new { nombre = "María", telefono = $"667{Random.Shared.Next(1_000_000, 9_999_999)}" });

        await using var conexion = new NpgsqlConnection(baseDeDatos.ConexionRuntime);
        await conexion.OpenAsync();
        await using var transaccion = await conexion.BeginTransactionAsync();
        await using (var fijar = new NpgsqlCommand($"SELECT set_config('app.docente_id', '{Guid.CreateVersion7()}', true)", conexion, transaccion))
        {
            await fijar.ExecuteNonQueryAsync();
        }

        await using var leer = new NpgsqlCommand("SELECT count(*) FROM tutor", conexion, transaccion);
        Assert.Equal(0L, await leer.ExecuteScalarAsync());
        await using var escribir = new NpgsqlCommand("UPDATE tutor SET nombre = 'x'", conexion, transaccion);
        var error = await Assert.ThrowsAsync<PostgresException>(() => escribir.ExecuteNonQueryAsync());
        Assert.Equal("42501", error.SqlState);
        Assert.NotEqual(Guid.Empty, docenteId);
    }

    [Fact]
    public async Task RN006_BorradoDeCuenta_EliminaGruposYTutoresSinOtroVinculo()
    {
        var (clienteA, docenteA, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var (clienteB, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var alumnoA = await CrearAlumnoAsync(clienteA);
        var alumnoB = await CrearAlumnoAsync(clienteB);
        var compartido = $"667{Random.Shared.Next(1_000_000, 9_999_999)}";
        var propio = $"668{Random.Shared.Next(1_000_000, 9_999_999)}";
        await clienteA.PostAsJsonAsync($"/api/v1/alumnos/{alumnoA}/tutores", new { nombre = "Compartido", telefono = compartido });
        await clienteA.PostAsJsonAsync($"/api/v1/alumnos/{alumnoA}/tutores", new { nombre = "Propio", telefono = propio });
        await clienteB.PostAsJsonAsync($"/api/v1/alumnos/{alumnoB}/tutores", new { nombre = "Compartido", telefono = compartido });

        await EjecutarSqlAsync($"UPDATE cuenta SET estado = 'eliminacion_solicitada', eliminacion_solicitada_en = now() - interval '31 days' WHERE id = '{docenteA}'");
        await EjecutarSqlAsync("SELECT eliminar_cuentas_vencidas(now())");

        Assert.Equal(0L, await ContarAsync($"SELECT count(*) FROM grupo WHERE docente_id = '{docenteA}'"));
        Assert.Equal(0L, await ContarAsync($"SELECT count(*) FROM alumno WHERE docente_id = '{docenteA}'"));
        Assert.Equal(0L, await ContarAsync($"SELECT count(*) FROM tutor WHERE telefono_e164 = '+52{propio}'"));
        Assert.Equal(1L, await ContarAsync($"SELECT count(*) FROM tutor WHERE telefono_e164 = '+52{compartido}'"));
        Assert.Single((await clienteB.GetFromJsonAsync<JsonElement>($"/api/v1/alumnos/{alumnoB}/tutores")).EnumerateArray());
    }

    [Fact]
    public async Task RF013_Plantilla_TraeLasColumnasQueSeDetectan()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();

        var bytes = await cliente.GetByteArrayAsync("/api/v1/importaciones/plantilla");

        using var libro = new XLWorkbook(new MemoryStream(bytes));
        var encabezados = libro.Worksheet(1).Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Equal(["Apellidos", "Nombres", "Tutor", "WhatsApp del tutor"], encabezados.Take(4));
    }

    [Fact]
    public async Task RF007_Exportacion_IncluyeGruposAlumnosYTutores()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var alumno = await CrearAlumnoAsync(cliente);
        await cliente.PostAsJsonAsync($"/api/v1/alumnos/{alumno}/tutores", new { nombre = "Rosa Ruiz", telefono = $"667{Random.Shared.Next(1_000_000, 9_999_999)}" });

        var solicitud = await cliente.PostAsync("/api/v1/docente/exportacion", null);
        var id = (await solicitud.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var bytes = await cliente.GetByteArrayAsync($"/api/v1/docente/exportaciones/{id}/excel");

        using var libro = new XLWorkbook(new MemoryStream(bytes));
        string Celdas(string hoja) => string.Join('|', libro.Worksheet(hoja).CellsUsed().Select(c => c.GetString()));
        Assert.Contains("2026-2027", Celdas("Ciclos y unidades"), StringComparison.Ordinal);
        Assert.Contains("Matemáticas", Celdas("Grupos"), StringComparison.Ordinal);
        Assert.Contains("Ruiz", Celdas("Alumnos"), StringComparison.Ordinal);
        Assert.Contains("Rosa Ruiz", Celdas("Tutores"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RN001_SinCorreoConfirmado_NoDaDeAltaAlumnosPeroSiCreaGrupos()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteAsync();
        var grupo = await CrearGrupoAsync(cliente, (await CrearCicloAsync(cliente)).Id);

        var alta = await cliente.PostAsJsonAsync($"/api/v1/grupos/{grupo}/alumnos", new { nombres = "Ana", apellidos = "Ruiz" });
        var vista = await PegarAsync(cliente, grupo, "Ruiz Ana");
        var confirmar = await cliente.PostAsJsonAsync(
            $"/api/v1/importaciones/{vista.GetProperty("id").GetGuid()}/confirmar",
            new { filas = new[] { new { nombres = "Ana", apellidos = "Ruiz" } } });

        Assert.Equal(HttpStatusCode.Forbidden, alta.StatusCode);
        Assert.Equal("correo_no_verificado", await CodigoAsync(alta));
        Assert.Equal("correo_no_verificado", await CodigoAsync(confirmar));
    }

    private sealed record Unidad(Guid Id, string Nombre, DateOnly Inicio, DateOnly Fin);

    private sealed record Ciclo(Guid Id, IReadOnlyList<Unidad> Unidades);

    private static async Task<Ciclo> CrearCicloAsync(HttpClient cliente)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/ciclos", new { nombre = "2026-2027", inicio = "2026-08-25", fin = "2027-07-16", tipoUnidad = "trimestre" });
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<Ciclo>())!;
    }

    private static async Task<Guid> CrearGrupoAsync(HttpClient cliente, Guid cicloId, string nombre = "A")
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/grupos", new { nombre, nivel = "secundaria", grado = 2, materia = "Matemáticas", cicloId });
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> AgregarAlumnoAsync(HttpClient cliente, Guid grupo, string nombres, string apellidos)
    {
        var respuesta = await cliente.PostAsJsonAsync($"/api/v1/grupos/{grupo}/alumnos", new { nombres, apellidos });
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CrearAlumnoAsync(HttpClient cliente) =>
        await AgregarAlumnoAsync(cliente, await CrearGrupoAsync(cliente, (await CrearCicloAsync(cliente)).Id), "Ana", "Ruiz");

    private static async Task<JsonElement> SubirAsync(HttpClient cliente, Guid grupo, byte[] archivo, string nombre)
    {
        var respuesta = await SubirSinLeerAsync(cliente, grupo, archivo, nombre);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<HttpResponseMessage> SubirSinLeerAsync(HttpClient cliente, Guid grupo, byte[] archivo, string nombre)
    {
        using var formulario = new MultipartFormDataContent { { new ByteArrayContent(archivo), "archivo", nombre } };
        return await cliente.PostAsync($"/api/v1/grupos/{grupo}/importaciones", formulario);
    }

    private static async Task<JsonElement> PegarAsync(HttpClient cliente, Guid grupo, string texto)
    {
        using var formulario = new MultipartFormDataContent { { new StringContent(texto), "texto" } };
        var respuesta = await cliente.PostAsync($"/api/v1/grupos/{grupo}/importaciones", formulario);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static byte[] ExcelDeAlumnos(int cuantos)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("4B");
        string[] encabezados = ["Apellidos", "Nombre(s)", "Nombre del tutor", "Celular", "Correo", "Parentesco"];
        for (var c = 0; c < encabezados.Length; c++)
        {
            hoja.Cell(1, c + 1).Value = encabezados[c];
        }

        for (var i = 1; i <= cuantos; i++)
        {
            hoja.Cell(i + 1, 1).Value = $"Apellido{36 - i:00} Uno";
            hoja.Cell(i + 1, 2).Value = $"Alumno {i}";
            hoja.Cell(i + 1, 3).Value = $"Tutor {i}";
            hoja.Cell(i + 1, 4).Value = 6670000000d + (i * 1000) + Random.Shared.Next(0, 999);
            hoja.Cell(i + 1, 6).Value = i % 2 == 0 ? "Mamá" : "Papá";
        }

        using var salida = new MemoryStream();
        libro.SaveAs(salida);
        return salida.ToArray();
    }

    private async Task EjecutarSqlAsync(string sql)
    {
        await using var conexion = new NpgsqlConnection(baseDeDatos.ConexionAdmin);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync();
    }

    private async Task<long> ContarAsync(string sql)
    {
        await using var conexion = new NpgsqlConnection(baseDeDatos.ConexionAdmin);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        return (long)(await comando.ExecuteScalarAsync())!;
    }

    private static async Task<string?> CodigoAsync(HttpResponseMessage respuesta)
    {
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.TryGetProperty("codigo", out var codigo) ? codigo.GetString() : null;
    }
}
