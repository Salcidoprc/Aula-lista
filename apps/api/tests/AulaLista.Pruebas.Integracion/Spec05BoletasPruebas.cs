using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace AulaLista.Pruebas.Integracion;

/// <summary>Criterios de aceptación de SPEC-05 (boletas).</summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed partial class Spec05BoletasPruebas(BaseDeDatosFixture baseDeDatos) : IDisposable
{
    private static readonly string[] Nombres = ["Ana Sofía", "Mateo", "Regina", "Santiago", "Valentina", "Leonardo", "Ximena", "Emiliano", "Camila", "Diego"];
    private static readonly string[] Apellidos = ["López Ríos", "Cruz Álvarez", "Hernández Paz", "Jiménez Mora", "Flores Vega", "García Luna", "Morales Soto", "Díaz Campos", "Ruiz Peña", "Torres Lara"];

    private readonly FabricaApi _fabrica = new(baseDeDatos);

    public void Dispose() => _fabrica.Dispose();

    [Fact]
    public async Task SPEC05_Grupo30Aprobadas_Genera30PdfsEnMenosDe60SegundosYUnoCombinado()
    {
        var g = await PrepararAsync(30);
        var reloj = Stopwatch.StartNew();

        var boletas = await GenerarAsync(g);

        reloj.Stop();
        Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(60), $"Tardó {reloj.Elapsed}.");
        Assert.Equal(30, boletas.GetProperty("conteos").GetProperty("lista").GetInt32());
        Assert.Equal(30, await ContarAsync($"SELECT count(*) FROM boleta WHERE periodo_id = '{g.PeriodoId}' AND pdf_ruta IS NOT NULL"));
        var primera = boletas.GetProperty("alumnos")[0].GetProperty("boletaId").GetGuid();
        var pdf = await g.Cliente.GetAsync($"/api/v1/boletas/{primera}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length < 200 * 1024, $"La boleta pesa {bytes.Length / 1024} KB.");

        var grupo = await g.Cliente.GetAsync($"/api/v1/periodos/{g.PeriodoId}/boletas/pdf");
        Assert.Equal(HttpStatusCode.OK, grupo.StatusCode);
        Assert.Equal(30, Paginas(await grupo.Content.ReadAsByteArrayAsync()));
    }

    [Fact]
    public async Task RN050_SoloSeGeneraBoletaDeAlumnosActivosConObservacionAprobada()
    {
        var g = await PrepararAsync(3, aprobar: 2);
        (await g.Cliente.DeleteAsync($"/api/v1/alumnos/{g.Alumnos[1]}")).EnsureSuccessStatusCode();

        var boletas = await GenerarAsync(g);

        var alumnos = boletas.GetProperty("alumnos").EnumerateArray().ToList();
        Assert.Equal(2, alumnos.Count);
        Assert.Equal("lista", Alumno(boletas, g.Alumnos[0]).GetProperty("estado").GetString());
        Assert.Equal("sin_aprobar", Alumno(boletas, g.Alumnos[2]).GetProperty("estado").GetString());
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM boleta WHERE periodo_id = '{g.PeriodoId}'"));
    }

    [Fact]
    public async Task RN051_LaBoletaPublicadaGuardaSuFotoAunqueCambienLasCalificaciones()
    {
        var g = await PrepararAsync(2);
        await GenerarAsync(g);
        await PublicarAsync(g, [g.Alumnos[0]]);

        await GuardarAsync(g, g.Alumnos[0], 6m);

        var boletas = await BoletasAsync(g);
        var ana = Alumno(boletas, g.Alumnos[0]);
        Assert.Equal("publicada", ana.GetProperty("estado").GetString());
        Assert.True(ana.GetProperty("desactualizada").GetBoolean());
        Assert.Equal("9.5", await EscalarAsync($"SELECT trim_scale((datos->>'calificacion')::numeric)::text FROM boleta WHERE alumno_id = '{g.Alumnos[0]}'"));
    }

    [Fact]
    public async Task RN052_CorregirUnaPublicada_CreaVersionNuevaVigenteYConservaLaAnterior()
    {
        var g = await PrepararAsync(1);
        await GenerarAsync(g);
        var publicadas = await PublicarAsync(g, null);
        Assert.Equal("cerrado", publicadas.GetProperty("estado").GetString()); // RN-048: todas publicadas
        var original = Alumno(publicadas, g.Alumnos[0]).GetProperty("boletaId").GetGuid();

        // RN-038: para corregir se reabre la unidad, se cambia la calificación y se vuelve a aprobar.
        var cerrada = await g.Cliente.PostAsync($"/api/v1/periodos/{g.PeriodoId}/boletas/generar", null);
        Assert.Equal("periodo_cerrado", await CodigoAsync(cerrada));
        var reabierta = await g.Cliente.PostAsync($"/api/v1/periodos/{g.PeriodoId}/cierre/reabrir", null);
        Assert.Equal(HttpStatusCode.OK, reabierta.StatusCode);
        await GuardarAsync(g, g.Alumnos[0], 8m);
        var sinAprobar = await g.Cliente.PostAsync($"/api/v1/boletas/{original}/corregir", null);
        Assert.Equal("observacion_sin_aprobar", await CodigoAsync(sinAprobar));
        await AprobarAsync(g, g.Alumnos[0]);

        var corregir = await g.Cliente.PostAsync($"/api/v1/boletas/{original}/corregir", null);

        Assert.Equal(HttpStatusCode.OK, corregir.StatusCode);
        var boletas = await corregir.Content.ReadFromJsonAsync<JsonElement>();
        var vigente = Alumno(boletas, g.Alumnos[0]);
        Assert.Equal(2, vigente.GetProperty("version").GetInt32());
        Assert.Equal("publicada", vigente.GetProperty("estado").GetString());
        Assert.False(vigente.GetProperty("desactualizada").GetBoolean());
        Assert.Equal("cerrado", boletas.GetProperty("estado").GetString());
        var nueva = vigente.GetProperty("boletaId").GetGuid();
        Assert.Equal(nueva.ToString(), await EscalarAsync($"SELECT reemplazada_por::text FROM boleta WHERE id = '{original}'"));
        Assert.Equal("9.5", await EscalarAsync($"SELECT trim_scale((datos->>'calificacion')::numeric)::text FROM boleta WHERE id = '{original}'"));
        Assert.Equal("8", await EscalarAsync($"SELECT trim_scale((datos->>'calificacion')::numeric)::text FROM boleta WHERE id = '{nueva}'"));
        Assert.Equal(HttpStatusCode.OK, (await g.Cliente.GetAsync($"/api/v1/boletas/{original}/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await g.Cliente.GetAsync($"/api/v1/boletas/{nueva}/pdf")).StatusCode);
        var otraVez = await g.Cliente.PostAsync($"/api/v1/boletas/{original}/corregir", null);
        Assert.Equal("periodo_cerrado", await CodigoAsync(otraVez));
    }

    [Fact]
    public async Task SPEC05_AlumnoSinTutores_SuBoletaSePublicaYSeDescargaYElResumenLoMarcaSinContacto()
    {
        var g = await PrepararAsync(2);
        var tutor = await g.Cliente.PostAsJsonAsync($"/api/v1/alumnos/{g.Alumnos[0]}/tutores", new { nombre = "Laura Treviño", telefono = "6671234567", correo = "laura@correo.mx", parentesco = "Mamá" });
        Assert.True(tutor.IsSuccessStatusCode, await tutor.Content.ReadAsStringAsync());
        await GenerarAsync(g);

        var boletas = await PublicarAsync(g, null);

        var conTutor = Alumno(boletas, g.Alumnos[0]);
        var sinTutor = Alumno(boletas, g.Alumnos[1]);
        Assert.Equal(1, conTutor.GetProperty("tutoresWhatsapp").GetInt32());
        Assert.Equal(1, conTutor.GetProperty("tutoresCorreo").GetInt32());
        Assert.Equal(0, sinTutor.GetProperty("tutoresWhatsapp").GetInt32());
        Assert.Equal("publicada", sinTutor.GetProperty("estado").GetString());
        var pdf = await g.Cliente.GetAsync($"/api/v1/boletas/{sinTutor.GetProperty("boletaId").GetGuid()}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
    }

    [Fact]
    public async Task RN056_ExportacionAExcel_PromediosCoincidenConLasBoletas()
    {
        var g = await PrepararAsync(3);
        await GuardarAsync(g, g.Alumnos[1], 7.25m);
        await AprobarAsync(g, g.Alumnos[1]);
        await GenerarAsync(g);

        var respuesta = await g.Cliente.GetAsync($"/api/v1/periodos/{g.PeriodoId}/exportacion.xlsx");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var libro = new XLWorkbook(await respuesta.Content.ReadAsStreamAsync());
        var hoja = libro.Worksheet("Calificaciones");
        Assert.Equal(["Número", "Alumno", "Tareas (100%)", "Final", "Observación"], hoja.Row(1).CellsUsed().Select(c => c.GetString()).ToArray());
        for (var i = 0; i < 3; i++)
        {
            var fila = hoja.Row(i + 2);
            var datos = JsonDocument.Parse(await EscalarAsync($"SELECT datos::text FROM boleta WHERE alumno_id = '{g.Alumnos[i]}'")).RootElement;
            Assert.Equal(datos.GetProperty("numeroLista").GetInt32(), fila.Cell(1).GetValue<int>());
            Assert.Equal(datos.GetProperty("alumno").GetString(), fila.Cell(2).GetString());
            Assert.Equal(datos.GetProperty("rubros")[0].GetProperty("promedio").GetDecimal(), fila.Cell(3).GetValue<decimal>());
            Assert.Equal(datos.GetProperty("calificacion").GetDecimal(), fila.Cell(4).GetValue<decimal>());
            Assert.Equal(datos.GetProperty("observacion").GetString(), fila.Cell(5).GetString());
        }

        Assert.Equal(7.25m, hoja.Row(3).Cell(3).GetValue<decimal>());
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM auditoria WHERE accion = 'calificaciones_exportadas' AND entidad_id = '{g.PeriodoId}' AND cantidad = 3"));
    }

    [Fact]
    public async Task RNF005_Publicar_QuedaEnAuditoriaConDocentePeriodoYNumeroDeBoletas()
    {
        var g = await PrepararAsync(3);
        await GenerarAsync(g);

        await PublicarAsync(g, [g.Alumnos[0], g.Alumnos[2]]);

        var registro = await EscalarAsync(
            $"SELECT actor_tipo || ':' || actor_id || ':' || entidad || ':' || cantidad FROM auditoria WHERE accion = 'boletas_publicadas' AND entidad_id = '{g.PeriodoId}'");
        Assert.Equal($"docente:{g.DocenteId}:periodo:2", registro);
        var boletas = await BoletasAsync(g);
        Assert.Equal(2, boletas.GetProperty("conteos").GetProperty("publicada").GetInt32());
        Assert.Equal("en_cierre", boletas.GetProperty("estado").GetString());
        Assert.Equal("publicada", await EscalarAsync($"SELECT estado FROM observacion WHERE alumno_id = '{g.Alumnos[0]}'"));
        Assert.Equal("aprobada", await EscalarAsync($"SELECT estado FROM observacion WHERE alumno_id = '{g.Alumnos[1]}'"));
    }

    [Fact]
    public async Task RN053_PublicarBoletasDesactualizadas_PideVolverAGenerar()
    {
        var g = await PrepararAsync(2);
        await GenerarAsync(g);
        await GuardarAsync(g, g.Alumnos[0], 8m);
        await AprobarAsync(g, g.Alumnos[0]);

        var publicar = await g.Cliente.PostAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/boletas/publicar", new { });

        Assert.Equal(HttpStatusCode.Conflict, publicar.StatusCode);
        Assert.Equal("boletas_desactualizadas", await CodigoAsync(publicar));
        var regeneradas = await GenerarAsync(g);
        Assert.False(Alumno(regeneradas, g.Alumnos[0]).GetProperty("desactualizada").GetBoolean());
        Assert.Equal(1, Alumno(regeneradas, g.Alumnos[0]).GetProperty("version").GetInt32());
        await PublicarAsync(g, null);
    }

    [Fact]
    public async Task RN048_RN038_CerrarAMano_YReabrir_QuedanEnAuditoria()
    {
        var g = await PrepararAsync(2);
        await GenerarAsync(g);
        await PublicarAsync(g, [g.Alumnos[0]]);

        var cerrar = await g.Cliente.PostAsync($"/api/v1/periodos/{g.PeriodoId}/cierre/cerrar", null);

        Assert.Equal(HttpStatusCode.OK, cerrar.StatusCode);
        Assert.Equal("cerrado", (await cerrar.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("estado").GetString());
        var boletas = await BoletasAsync(g);
        Assert.True(boletas.GetProperty("soloLectura").GetBoolean());
        var publicar = await g.Cliente.PostAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/boletas/publicar", new { });
        Assert.Equal("periodo_cerrado", await CodigoAsync(publicar));
        var calificar = await g.Cliente.PutAsJsonAsync("/api/v1/calificaciones", new
        {
            celdas = new[] { new { actividadId = g.Actividad, alumnoId = g.Alumnos[0], valor = (decimal?)6m, marca = "ninguna", version = 0 } },
        });
        Assert.Contains("periodo_cerrado", await calificar.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var reabrir = await g.Cliente.PostAsync($"/api/v1/periodos/{g.PeriodoId}/cierre/reabrir", null);

        Assert.Equal("en_cierre", (await reabrir.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("estado").GetString());
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM auditoria WHERE accion = 'periodo_cerrado' AND entidad_id = '{g.PeriodoId}' AND actor_id = '{g.DocenteId}'"));
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM auditoria WHERE accion = 'periodo_reabierto' AND entidad_id = '{g.PeriodoId}' AND actor_id = '{g.DocenteId}'"));
        Assert.Equal("publicada", Alumno(await BoletasAsync(g), g.Alumnos[0]).GetProperty("estado").GetString());
    }

    [Fact]
    public async Task RNF001_BoletasDeOtroDocente_Responden404()
    {
        var g = await PrepararAsync(1);
        var boletas = await GenerarAsync(g);
        var boleta = boletas.GetProperty("alumnos")[0].GetProperty("boletaId").GetGuid();
        var (otro, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();

        HttpResponseMessage[] respuestas =
        [
            await otro.GetAsync($"/api/v1/periodos/{g.PeriodoId}/boletas"),
            await otro.PostAsync($"/api/v1/periodos/{g.PeriodoId}/boletas/generar", null),
            await otro.PostAsync($"/api/v1/periodos/{g.PeriodoId}/boletas/publicar", null),
            await otro.GetAsync($"/api/v1/periodos/{g.PeriodoId}/boletas/pdf"),
            await otro.GetAsync($"/api/v1/periodos/{g.PeriodoId}/exportacion.xlsx"),
            await otro.GetAsync($"/api/v1/boletas/{boleta}/pdf"),
            await otro.PostAsync($"/api/v1/boletas/{boleta}/corregir", null),
            await otro.PostAsync($"/api/v1/periodos/{g.PeriodoId}/cierre/cerrar", null),
            await otro.PostAsync($"/api/v1/periodos/{g.PeriodoId}/cierre/reabrir", null),
        ];

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
    }

    private static int Paginas(byte[] pdf) => PaginaPdf().Count(Encoding.Latin1.GetString(pdf));

    [GeneratedRegex(@"/Type\s*/Page(?![s\w])")]
    private static partial Regex PaginaPdf();

    private static JsonElement Alumno(JsonElement boletas, Guid alumno) =>
        boletas.GetProperty("alumnos").EnumerateArray().Single(a => a.GetProperty("alumnoId").GetGuid() == alumno);

    private sealed record Grupo(HttpClient Cliente, Guid DocenteId, Guid GrupoId, Guid PeriodoId, IReadOnlyList<Guid> Alumnos, Guid Actividad);

    /// <summary>
    /// Grupo de primaria con un rubro de 100, 9.5 para todos, cierre iniciado, observaciones generadas y las primeras
    /// <paramref name="aprobar"/> aprobadas (todas si es nulo).
    /// </summary>
    private async Task<Grupo> PrepararAsync(int alumnos, int? aprobar = null)
    {
        var (cliente, docenteId, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var perfil = await cliente.PutAsJsonAsync("/api/v1/docente/perfil", new
        {
            nombre = "Martha Elena Ruiz",
            nivel = "primaria",
            escuelaNombre = "Escuela Primaria Benito Juárez",
            escuelaCct = "25DPR0123X",
            entidadFederativa = "sinaloa",
        });
        Assert.True(perfil.IsSuccessStatusCode, await perfil.Content.ReadAsStringAsync());
        var ciclo = await cliente.PostAsJsonAsync("/api/v1/ciclos", new { nombre = "2026-2027", inicio = "2026-08-25", fin = "2027-07-16", tipoUnidad = "trimestre" });
        ciclo.EnsureSuccessStatusCode();
        var cicloId = (await ciclo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var grupo = await cliente.PostAsJsonAsync("/api/v1/grupos", new { nombre = "B", nivel = "primaria", grado = 4, cicloId });
        grupo.EnsureSuccessStatusCode();
        var grupoId = (await grupo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var periodo = (await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{grupoId}/periodos"))[0].GetProperty("id").GetGuid();
        var ids = new List<Guid>();
        for (var i = 0; i < alumnos; i++)
        {
            var alta = await cliente.PostAsJsonAsync(
                $"/api/v1/grupos/{grupoId}/alumnos",
                new { nombres = i < 10 ? Nombres[i] : $"{Nombres[i % 10]} {i}", apellidos = Apellidos[i % 10] });
            alta.EnsureSuccessStatusCode();
            ids.Add((await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        }

        var tipos = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/tipos-rubro");
        var tareas = tipos.EnumerateArray().First(t => t.GetProperty("nombre").GetString() == "Tareas").GetProperty("id").GetGuid();
        var rubros = await cliente.PutAsJsonAsync($"/api/v1/periodos/{periodo}/rubros", new
        {
            rubros = new[] { new { nombre = "Tareas", tipoRubroId = tareas, peso = 100m, forma = "numerica", califMinima = 5m } },
        });
        Assert.Equal(HttpStatusCode.OK, rubros.StatusCode);
        var rubro = (await rubros.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rubros")[0].GetProperty("id").GetGuid();
        var actividad = await cliente.PostAsJsonAsync($"/api/v1/rubros/{rubro}/actividades", new { nombre = "Tarea 1" });
        Assert.Equal(HttpStatusCode.Created, actividad.StatusCode);
        var g = new Grupo(cliente, docenteId, grupoId, periodo, ids, (await actividad.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        foreach (var alumno in ids)
        {
            await GuardarAsync(g, alumno, 9.5m);
        }

        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsync($"/api/v1/periodos/{periodo}/cierre", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsJsonAsync($"/api/v1/periodos/{periodo}/observaciones/generar", new { })).StatusCode);
        foreach (var alumno in ids.Take(aprobar ?? ids.Count))
        {
            await AprobarAsync(g, alumno);
        }

        return g;
    }

    private static async Task GuardarAsync(Grupo g, Guid alumno, decimal valor)
    {
        var respuesta = await g.Cliente.PutAsJsonAsync("/api/v1/calificaciones", new
        {
            celdas = new[] { new { actividadId = g.Actividad, alumnoId = alumno, valor = (decimal?)valor, marca = "ninguna", version = 0 } },
        });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Empty((await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rechazadas").EnumerateArray());
    }

    private static async Task AprobarAsync(Grupo g, Guid alumno)
    {
        var observacion = (await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/periodos/{g.PeriodoId}/cierre"))
            .GetProperty("alumnos").EnumerateArray().Single(a => a.GetProperty("alumnoId").GetGuid() == alumno)
            .GetProperty("observacionId").GetGuid();
        var respuesta = await g.Cliente.PostAsJsonAsync($"/api/v1/observaciones/{observacion}/aprobar", new { texto = (string?)null });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    private static async Task<JsonElement> GenerarAsync(Grupo g)
    {
        var respuesta = await g.Cliente.PostAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/boletas/generar", new { });
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());
        return await BoletasAsync(g);
    }

    private static async Task<JsonElement> PublicarAsync(Grupo g, Guid[]? alumnos)
    {
        var respuesta = await g.Cliente.PostAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/boletas/publicar", new { alumnoIds = alumnos });
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<JsonElement> BoletasAsync(Grupo g) =>
        g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/periodos/{g.PeriodoId}/boletas");

    private async Task<long> ContarAsync(string sql) => (long)(await EscalarObjetoAsync(sql))!;

    private async Task<string> EscalarAsync(string sql) => (string)(await EscalarObjetoAsync(sql))!;

    private async Task<object?> EscalarObjetoAsync(string sql)
    {
        await using var conexion = new Npgsql.NpgsqlConnection(baseDeDatos.ConexionAdmin);
        await conexion.OpenAsync();
        await using var comando = new Npgsql.NpgsqlCommand(sql, conexion);
        return await comando.ExecuteScalarAsync();
    }

    private static async Task<string?> CodigoAsync(HttpResponseMessage respuesta)
    {
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.TryGetProperty("codigo", out var codigo) ? codigo.GetString() : null;
    }
}
