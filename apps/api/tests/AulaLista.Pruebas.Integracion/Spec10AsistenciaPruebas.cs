using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace AulaLista.Pruebas.Integracion;

/// <summary>Criterios de aceptación de SPEC-10 (pase de lista, historial, rubro de asistencia y cola sin conexión).</summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class Spec10AsistenciaPruebas(BaseDeDatosFixture baseDeDatos) : IDisposable
{
    private static readonly DateOnly Lunes = new(2026, 9, 7);

    private readonly FabricaApi _fabrica = new(baseDeDatos);

    public void Dispose() => _fabrica.Dispose();

    [Fact]
    public async Task SPEC10_Grupo30_TodosPresentesYDosFaltas_Guarda28PresentesY2Faltas()
    {
        var g = await PrepararAsync(30);

        var toma = await PasarListaAsync(g, Lunes, (g.Alumnos[3], "falta"), (g.Alumnos[17], "falta"));

        var estados = toma.GetProperty("toma").GetProperty("alumnos").EnumerateArray().Select(a => a.GetProperty("estado").GetString()).ToList();
        Assert.Equal(30, estados.Count);
        Assert.Equal(28, estados.Count(e => e == "presente"));
        Assert.Equal(2, estados.Count(e => e == "falta"));
        Assert.Equal(30, await ContarAsync($"SELECT count(*) FROM asistencia a JOIN toma_asistencia t ON t.id = a.toma_asistencia_id WHERE t.grupo_id = '{g.GrupoId}'"));
    }

    [Fact]
    public async Task SPEC10_20Tomas_18Presentes1Falta1Justificada_Da9_47EnElRubro()
    {
        var g = await PrepararAsync(1);
        var rubro = await RubroAsistenciaAsync(g, 100m);
        for (var i = 0; i < 20; i++)
        {
            var estado = i switch { 18 => "falta", 19 => "justificada", _ => "presente" };
            await PasarListaAsync(g, Lunes.AddDays(i), (g.Alumnos[0], estado));
        }

        var matriz = await MatrizAsync(g);

        Assert.Equal(9.47m, PromedioRubro(matriz, g.Alumnos[0], rubro));
        Assert.Equal(20, matriz.GetProperty("tomas").GetInt32());
        Assert.Equal(20, matriz.GetProperty("asistencia")[0].GetProperty("estados").GetArrayLength());
    }

    [Fact]
    public async Task SPEC10_RetardoMedio_9PresentesY1Retardo_Da95Por100()
    {
        var g = await PrepararAsync(1);
        var rubro = await RubroAsistenciaAsync(g, 100m);
        var edicion = await g.Cliente.PutAsJsonAsync($"/api/v1/grupos/{g.GrupoId}", new
        {
            nombre = "A",
            grado = 2,
            materia = "Ciencias",
            califMinima = 5m,
            califAprobatoria = 6m,
            decimalesFinal = 2,
            redondeo = "cercano",
            valorRetardo = 0.5m,
        });
        Assert.Equal(HttpStatusCode.OK, edicion.StatusCode);
        for (var i = 0; i < 10; i++)
        {
            await PasarListaAsync(g, Lunes.AddDays(i), (g.Alumnos[0], i == 4 ? "retardo" : "presente"));
        }

        var matriz = await MatrizAsync(g);

        Assert.Equal(9.50m, PromedioRubro(matriz, g.Alumnos[0], rubro));
        Assert.Equal(0.5m, matriz.GetProperty("configuracion").GetProperty("valorRetardo").GetDecimal());
    }

    [Fact]
    public async Task SPEC10_Asistencia10_ConActividades30YExamen60_ParticipaEnElPromedio()
    {
        var g = await PrepararAsync(1);
        var respuesta = await g.Cliente.PutAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/rubros", new
        {
            rubros = new object[]
            {
                new { nombre = "Actividades", tipoRubroId = g.Tipo("Actividades"), peso = 30m, forma = "numerica", califMinima = 5m },
                new { nombre = "Examen", tipoRubroId = g.Tipo("Exámenes"), peso = 60m, forma = "numerica", califMinima = 5m },
                new { nombre = "Asistencia", tipoRubroId = g.Tipo("Asistencia"), peso = 10m, forma = "numerica", califMinima = 5m },
            },
        });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var rubros = (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rubros").EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).ToList();
        foreach (var (rubro, valor) in new[] { (rubros[0], 8m), (rubros[1], 7m) })
        {
            var actividad = await g.Cliente.PostAsJsonAsync($"/api/v1/rubros/{rubro}/actividades", new { nombre = "Uno", modo = "individual" });
            var id = (await actividad.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var celda = await g.Cliente.PutAsJsonAsync("/api/v1/calificaciones", new
            {
                celdas = new[] { new { actividadId = id, alumnoId = g.Alumnos[0], valor = valor, marca = "ninguna", version = 0 } },
            });
            Assert.Equal(HttpStatusCode.OK, celda.StatusCode);
        }

        for (var i = 0; i < 20; i++)
        {
            await PasarListaAsync(g, Lunes.AddDays(i), (g.Alumnos[0], i < 18 ? "presente" : "falta"));
        }

        var resultado = Resultado(await MatrizAsync(g), g.Alumnos[0]);

        Assert.Equal(7.50m, resultado.GetProperty("periodo").GetDecimal()); // 8 × 0.3 + 7 × 0.6 + 9 × 0.1
    }

    [Fact]
    public async Task SPEC10_SinConexion_ReenviarLaMismaClave_NoDuplicaNiPisaLoNuevo()
    {
        var g = await PrepararAsync(2);
        var clave = Guid.NewGuid();

        await PasarListaAsync(g, Lunes, clave, (g.Alumnos[0], "falta"));
        await PasarListaAsync(g, Lunes, Guid.NewGuid(), (g.Alumnos[0], "retardo"));
        var repetida = await PasarListaAsync(g, Lunes, clave, (g.Alumnos[0], "falta"));

        Assert.Equal("retardo", Estado(repetida.GetProperty("toma"), g.Alumnos[0]));
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM toma_asistencia WHERE grupo_id = '{g.GrupoId}'"));
        Assert.Equal(2, await ContarAsync($"SELECT count(*) FROM asistencia a JOIN toma_asistencia t ON t.id = a.toma_asistencia_id WHERE t.grupo_id = '{g.GrupoId}'"));
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM cambio_sincronizado WHERE id = '{clave}'"));
    }

    [Fact]
    public async Task SPEC10_DosDispositivosElMismoDia_PorAlumnoGanaLaUltimaEscritura()
    {
        var g = await PrepararAsync(3);

        await PasarListaAsync(g, Lunes, (g.Alumnos[0], "falta"), (g.Alumnos[1], "falta"));
        var segunda = await PasarListaAsync(g, Lunes, (g.Alumnos[1], "justificada"), (g.Alumnos[2], "retardo"));

        var toma = segunda.GetProperty("toma");
        Assert.Equal("falta", Estado(toma, g.Alumnos[0]));
        Assert.Equal("justificada", Estado(toma, g.Alumnos[1]));
        Assert.Equal("retardo", Estado(toma, g.Alumnos[2]));
    }

    [Fact]
    public async Task RN100_FechaFueraDeTodaUnidad_SeRechaza()
    {
        var g = await PrepararAsync(1);

        var respuesta = await PutAsync(g, new DateOnly(2027, 8, 2), null, (g.Alumnos[0], "presente"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Equal("fecha_fuera_de_unidad", await CodigoAsync(respuesta));
    }

    [Fact]
    public async Task RN101_EstadoQueNoExiste_SeRechaza()
    {
        var g = await PrepararAsync(1);

        var respuesta = await PutAsync(g, Lunes, null, (g.Alumnos[0], "tarde"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Equal("asistencia_invalida", await CodigoAsync(respuesta));
        Assert.Equal(0, await ContarAsync($"SELECT count(*) FROM toma_asistencia WHERE grupo_id = '{g.GrupoId}'"));
    }

    [Fact]
    public async Task RN104_DosRubrosDeAsistencia_SeRechazan()
    {
        var g = await PrepararAsync(0);
        var asistencia = new { nombre = "Asistencia", tipoRubroId = g.Tipo("Asistencia"), peso = 10m, forma = "numerica", califMinima = 5m };

        var respuesta = await g.Cliente.PutAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/rubros", new { rubros = new[] { asistencia, asistencia with { nombre = "Lista" } } });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Equal("asistencia_duplicada", await CodigoAsync(respuesta));
    }

    [Fact]
    public async Task RN105_AlumnoDeBajaOAltaAMitadDeUnidad_SoloCuentaLasTomasEnQueEstabaActivo()
    {
        var g = await PrepararAsync(2);
        var rubro = await RubroAsistenciaAsync(g, 100m);
        await PasarListaAsync(g, Lunes, (g.Alumnos[1], "falta"));
        Assert.Equal(HttpStatusCode.NoContent, (await g.Cliente.DeleteAsync($"/api/v1/alumnos/{g.Alumnos[1]}")).StatusCode);
        var alta = await g.Cliente.PostAsJsonAsync($"/api/v1/grupos/{g.GrupoId}/alumnos", new { nombres = "Nueva", apellidos = "Alumna" });
        var nueva = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var segunda = (await PasarListaAsync(g, Lunes.AddDays(1))).GetProperty("toma");
        var correccion = await PasarListaAsync(g, Lunes, (g.Alumnos[1], "justificada"));
        var tomas = await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{g.GrupoId}/asistencias?desde={Lunes:yyyy-MM-dd}&hasta={Lunes.AddDays(1):yyyy-MM-dd}");

        var alumnosSegunda = segunda.GetProperty("alumnos").EnumerateArray().Select(a => a.GetProperty("alumnoId").GetGuid()).ToList();
        Assert.DoesNotContain(g.Alumnos[1], alumnosSegunda);
        Assert.Contains(nueva, alumnosSegunda);
        Assert.Equal("justificada", Estado(correccion.GetProperty("toma"), g.Alumnos[1]));
        Assert.DoesNotContain(nueva, tomas[0].GetProperty("alumnos").EnumerateArray().Select(a => a.GetProperty("alumnoId").GetGuid()));
        Assert.Equal(10.00m, PromedioRubro(await MatrizAsync(g), nueva, rubro));
    }

    [Fact]
    public async Task RN106_UnidadCerrada_NoSeCorrige_YEnCierreSi()
    {
        var g = await PrepararAsync(1);
        await PasarListaAsync(g, Lunes);

        await EjecutarSqlAsync($"UPDATE periodo SET estado = 'en_cierre' WHERE id = '{g.PeriodoId}'");
        var enCierre = await PutAsync(g, Lunes, null, (g.Alumnos[0], "falta"));
        await EjecutarSqlAsync($"UPDATE periodo SET estado = 'cerrado' WHERE id = '{g.PeriodoId}'");
        var cerrada = await PutAsync(g, Lunes, null, (g.Alumnos[0], "presente"));
        var borrado = await g.Cliente.DeleteAsync($"/api/v1/grupos/{g.GrupoId}/asistencias/{Lunes:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, enCierre.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, cerrada.StatusCode);
        Assert.Equal("periodo_cerrado", await CodigoAsync(cerrada));
        Assert.Equal(HttpStatusCode.Conflict, borrado.StatusCode);
    }

    [Fact]
    public async Task SPEC10_BorrarLaToma_LaQuitaDelHistorial()
    {
        var g = await PrepararAsync(2);
        await PasarListaAsync(g, Lunes, (g.Alumnos[0], "falta"));

        var borrado = await g.Cliente.DeleteAsync($"/api/v1/grupos/{g.GrupoId}/asistencias/{Lunes:yyyy-MM-dd}");
        var otraVez = await g.Cliente.DeleteAsync($"/api/v1/grupos/{g.GrupoId}/asistencias/{Lunes:yyyy-MM-dd}");
        var tomas = await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{g.GrupoId}/asistencias");

        Assert.Equal(HttpStatusCode.NoContent, borrado.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otraVez.StatusCode);
        Assert.Equal(0, tomas.GetArrayLength());
        Assert.Equal(0, await ContarAsync($"SELECT count(*) FROM asistencia a JOIN alumno al ON al.id = a.alumno_id WHERE al.grupo_id = '{g.GrupoId}'"));
    }

    [Fact]
    public async Task SPEC10_CambiarUnidadesDejandoTomasFuera_PideConfirmarYLasConserva()
    {
        var g = await PrepararAsync(1);
        await PasarListaAsync(g, Lunes);
        var ciclo = await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/ciclos/{g.CicloId}");
        var unidades = ciclo.GetProperty("unidades").EnumerateArray().Select(u => new
        {
            id = u.GetProperty("id").GetGuid(),
            nombre = u.GetProperty("nombre").GetString(),
            inicio = u.GetProperty("orden").GetInt32() == 1 ? "2026-09-14" : u.GetProperty("inicio").GetString(),
            fin = u.GetProperty("fin").GetString(),
        }).ToList();

        var sinConfirmar = await g.Cliente.PutAsJsonAsync($"/api/v1/ciclos/{g.CicloId}/unidades", new { unidades });
        var confirmado = await g.Cliente.PutAsJsonAsync($"/api/v1/ciclos/{g.CicloId}/unidades", new { unidades, confirmarTomasFuera = true });

        Assert.Equal(HttpStatusCode.Conflict, sinConfirmar.StatusCode);
        Assert.Equal("tomas_fuera_de_unidad", await CodigoAsync(sinConfirmar));
        Assert.Equal(HttpStatusCode.OK, confirmado.StatusCode);
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM toma_asistencia WHERE grupo_id = '{g.GrupoId}'"));
    }

    [Fact]
    public async Task RNF001_AsistenciaDeOtroDocente_Responde404()
    {
        var g = await PrepararAsync(1);
        await PasarListaAsync(g, Lunes);
        var (otro, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();

        var lectura = await otro.GetAsync($"/api/v1/grupos/{g.GrupoId}/asistencias");
        var escritura = await otro.PutAsJsonAsync($"/api/v1/grupos/{g.GrupoId}/asistencias/{Lunes:yyyy-MM-dd}", new { alumnos = new[] { new { alumnoId = g.Alumnos[0], estado = "falta" } } });
        var borrado = await otro.DeleteAsync($"/api/v1/grupos/{g.GrupoId}/asistencias/{Lunes:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.NotFound, lectura.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, escritura.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, borrado.StatusCode);
        Assert.Equal(0, await ContarAsync($"SELECT count(*) FROM asistencia WHERE estado = 'falta' AND alumno_id = '{g.Alumnos[0]}'"));
    }

    private sealed record Grupo(HttpClient Cliente, Guid CicloId, Guid GrupoId, Guid PeriodoId, IReadOnlyList<Guid> Alumnos, JsonElement Tipos)
    {
        public Guid Tipo(string nombre) =>
            Tipos.EnumerateArray().Single(t => t.GetProperty("nombre").GetString() == nombre).GetProperty("id").GetGuid();
    }

    private async Task<Grupo> PrepararAsync(int alumnos)
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var ciclo = await cliente.PostAsJsonAsync("/api/v1/ciclos", new { nombre = "2026-2027", inicio = "2026-08-25", fin = "2027-07-16", tipoUnidad = "trimestre" });
        ciclo.EnsureSuccessStatusCode();
        var cicloId = (await ciclo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var grupo = await cliente.PostAsJsonAsync("/api/v1/grupos", new { nombre = "A", nivel = "secundaria", grado = 2, materia = "Ciencias", cicloId });
        grupo.EnsureSuccessStatusCode();
        var grupoId = (await grupo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var periodo = (await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{grupoId}/periodos"))[0].GetProperty("id").GetGuid();
        var ids = new List<Guid>();
        for (var i = 1; i <= alumnos; i++)
        {
            var alta = await cliente.PostAsJsonAsync($"/api/v1/grupos/{grupoId}/alumnos", new { nombres = $"Alumno {i}", apellidos = $"Prueba {i:00}" });
            alta.EnsureSuccessStatusCode();
            ids.Add((await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        }

        var tipos = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/tipos-rubro");
        return new Grupo(cliente, cicloId, grupoId, periodo, ids, tipos);
    }

    private static async Task<Guid> RubroAsistenciaAsync(Grupo g, decimal peso)
    {
        var respuesta = await g.Cliente.PutAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/rubros", new
        {
            rubros = new[] { new { nombre = "Asistencia", tipoRubroId = g.Tipo("Asistencia"), peso, forma = "numerica", califMinima = 5m } },
        });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rubros")[0].GetProperty("id").GetGuid();
    }

    private static Task<JsonElement> PasarListaAsync(Grupo g, DateOnly fecha, params (Guid Alumno, string Estado)[] cambios) =>
        PasarListaAsync(g, fecha, null, cambios);

    private static async Task<JsonElement> PasarListaAsync(Grupo g, DateOnly fecha, Guid? clave, params (Guid Alumno, string Estado)[] cambios)
    {
        var respuesta = await PutAsync(g, fecha, clave, cambios);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<HttpResponseMessage> PutAsync(Grupo g, DateOnly fecha, Guid? clave, params (Guid Alumno, string Estado)[] cambios)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/grupos/{g.GrupoId}/asistencias/{fecha:yyyy-MM-dd}")
        {
            Content = JsonContent.Create(new { alumnos = cambios.Select(c => new { alumnoId = c.Alumno, estado = c.Estado }) }),
        };
        if (clave is { } k)
        {
            solicitud.Headers.Add("Idempotency-Key", k.ToString());
        }

        return await g.Cliente.SendAsync(solicitud);
    }

    private static string? Estado(JsonElement toma, Guid alumno) =>
        toma.GetProperty("alumnos").EnumerateArray().Single(a => a.GetProperty("alumnoId").GetGuid() == alumno).GetProperty("estado").GetString();

    private static async Task<JsonElement> MatrizAsync(Grupo g) =>
        await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/periodos/{g.PeriodoId}/calificaciones");

    private static JsonElement Resultado(JsonElement matriz, Guid alumno) =>
        matriz.GetProperty("resultados").EnumerateArray().Single(r => r.GetProperty("alumnoId").GetGuid() == alumno);

    private static decimal? PromedioRubro(JsonElement matriz, Guid alumno, Guid rubro)
    {
        var promedio = Resultado(matriz, alumno).GetProperty("rubros").EnumerateArray().Single(r => r.GetProperty("rubroId").GetGuid() == rubro).GetProperty("promedio");
        return promedio.ValueKind == JsonValueKind.Null ? null : promedio.GetDecimal();
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
