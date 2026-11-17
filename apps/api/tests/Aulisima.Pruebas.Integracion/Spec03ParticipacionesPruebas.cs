using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace Aulisima.Pruebas.Integracion;

/// <summary>Criterios de aceptación de las participaciones extra (SPEC-03, RN-121 a RN-126, ADR-0019).</summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class Spec03ParticipacionesPruebas(BaseDeDatosFixture baseDeDatos) : IDisposable
{
    private static readonly DateOnly Lunes = new(2026, 9, 7);

    private readonly FabricaApi _fabrica = new(baseDeDatos);

    public void Dispose() => _fabrica.Dispose();

    [Fact]
    public async Task RN124_Ordinaria810_Con4ParticipacionesDe010_Da850()
    {
        var g = await PrepararAsync(2);
        await CalificarAsync(g, g.Alumnos[0], 8.1m);
        await CalificarAsync(g, g.Alumnos[1], 9m);
        for (var i = 0; i < 4; i++)
        {
            await DarAsync(g, g.Alumnos[0], Lunes.AddDays(i % 2));
        }

        var matriz = await MatrizAsync(g);
        var resultado = Resultado(matriz, g.Alumnos[0]);

        Assert.Equal(8.50m, resultado.GetProperty("periodo").GetDecimal());
        Assert.Equal(8.50m, resultado.GetProperty("final").GetDecimal());
        Assert.Equal(0.40m, resultado.GetProperty("puntosExtra").GetDecimal());
        Assert.Equal(9.00m, Resultado(matriz, g.Alumnos[1]).GetProperty("periodo").GetDecimal());
        Assert.Equal(0.10m, matriz.GetProperty("configuracion").GetProperty("valorParticipacion").GetDecimal());
        var conteo = Assert.Single(matriz.GetProperty("participaciones").EnumerateArray());
        Assert.Equal(g.Alumnos[0], conteo.GetProperty("alumnoId").GetGuid());
        Assert.Equal(4, conteo.GetProperty("cantidad").GetInt32());
    }

    [Fact]
    public async Task RN124_Ordinaria980_Con5Participaciones_NoPasaDe10()
    {
        var g = await PrepararAsync(1);
        await CalificarAsync(g, g.Alumnos[0], 9.8m);
        for (var i = 0; i < 5; i++)
        {
            await DarAsync(g, g.Alumnos[0], Lunes);
        }

        Assert.Equal(10.00m, Resultado(await MatrizAsync(g), g.Alumnos[0]).GetProperty("periodo").GetDecimal());
    }

    [Fact]
    public async Task RN124_SinCalificacionOrdinaria_LasParticipacionesNoCreanCalificacion()
    {
        var g = await PrepararAsync(1);
        for (var i = 0; i < 3; i++)
        {
            await DarAsync(g, g.Alumnos[0], Lunes);
        }

        var resultado = Resultado(await MatrizAsync(g), g.Alumnos[0]);

        Assert.Equal(JsonValueKind.Null, resultado.GetProperty("periodo").ValueKind);
        Assert.Equal(0.30m, resultado.GetProperty("puntosExtra").GetDecimal());
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(0)]
    [InlineData(0.005)]
    public async Task RN121_ValorFueraDeRango_SeRechaza(double valor)
    {
        var g = await PrepararAsync(1);

        var respuesta = await EditarValorAsync(g, (decimal)valor);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Equal("configuracion_calificacion_invalida", await CodigoAsync(respuesta));
    }

    [Fact]
    public async Task RN123_CambiarElValor_RecalculaLosPuntos()
    {
        var g = await PrepararAsync(1);
        await CalificarAsync(g, g.Alumnos[0], 7m);
        await DarAsync(g, g.Alumnos[0], Lunes);
        await DarAsync(g, g.Alumnos[0], Lunes);

        Assert.Equal(HttpStatusCode.OK, (await EditarValorAsync(g, 1m)).StatusCode);

        var resultado = Resultado(await MatrizAsync(g), g.Alumnos[0]);
        Assert.Equal(9.00m, resultado.GetProperty("periodo").GetDecimal());
        Assert.Equal(1m, (await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{g.GrupoId}")).GetProperty("valorParticipacion").GetDecimal());
    }

    [Fact]
    public async Task RF111_SinConexion_5DadasReenviadasY1Quitada_QuedanCuatroSinDuplicar()
    {
        var g = await PrepararAsync(1);
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        foreach (var id in ids.Concat(ids))
        {
            var respuesta = await g.Cliente.PostAsJsonAsync($"/api/v1/grupos/{g.GrupoId}/participaciones", new { id, alumnoId = g.Alumnos[0], fecha = Lunes });
            Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        }

        var quitada = await g.Cliente.DeleteAsync($"/api/v1/participaciones/{ids[2]}");
        var otraVez = await g.Cliente.DeleteAsync($"/api/v1/participaciones/{ids[2]}");
        var lista = await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{g.GrupoId}/participaciones?desde={Lunes:yyyy-MM-dd}&hasta={Lunes:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.NoContent, quitada.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otraVez.StatusCode);
        Assert.Equal(4, lista.GetArrayLength());
        Assert.Equal(4, await ContarAsync($"SELECT count(*) FROM participacion WHERE alumno_id = '{g.Alumnos[0]}'"));
    }

    [Fact]
    public async Task RN125_UnidadCerrada_RechazaDarYQuitar()
    {
        var g = await PrepararAsync(1);
        var dada = await DarAsync(g, g.Alumnos[0], Lunes);
        await EjecutarSqlAsync($"UPDATE periodo SET estado = 'cerrado' WHERE id = '{g.PeriodoId}'");

        var nueva = await PostAsync(g, g.Alumnos[0], Lunes);
        var quitar = await g.Cliente.DeleteAsync($"/api/v1/participaciones/{dada}");

        Assert.Equal(HttpStatusCode.Conflict, nueva.StatusCode);
        Assert.Equal("periodo_cerrado", await CodigoAsync(nueva));
        Assert.Equal(HttpStatusCode.Conflict, quitar.StatusCode);
    }

    [Fact]
    public async Task RN125_EnCierre_SePuedeDarYMarcaLaUnidad()
    {
        var g = await PrepararAsync(1);
        await EjecutarSqlAsync($"UPDATE periodo SET estado = 'en_cierre' WHERE id = '{g.PeriodoId}'");

        Assert.Equal(HttpStatusCode.Created, (await PostAsync(g, g.Alumnos[0], Lunes)).StatusCode);
    }

    [Fact]
    public async Task RN122_FechaFueraDeUnidadOAlumnoDeOtroGrupo_SeRechaza()
    {
        var g = await PrepararAsync(1);
        var otro = await PrepararAsync(1);

        var fuera = await PostAsync(g, g.Alumnos[0], new DateOnly(2026, 8, 1));
        var ajeno = await g.Cliente.PostAsJsonAsync(
            $"/api/v1/grupos/{g.GrupoId}/participaciones", new { id = Guid.NewGuid(), alumnoId = otro.Alumnos[0], fecha = Lunes });

        Assert.Equal("fecha_fuera_de_unidad", await CodigoAsync(fuera));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, ajeno.StatusCode);
        Assert.Equal("participacion_invalida", await CodigoAsync(ajeno));
    }

    [Fact]
    public async Task RN122_AlumnoDadoDeBaja_NoRecibeParticipaciones()
    {
        var g = await PrepararAsync(1);
        await EjecutarSqlAsync($"UPDATE alumno SET baja_en = now() WHERE id = '{g.Alumnos[0]}'");

        var respuesta = await PostAsync(g, g.Alumnos[0], Lunes);

        Assert.Equal("alumno_dado_de_baja", await CodigoAsync(respuesta));
    }

    [Fact]
    public async Task RNF001_ParticipacionesDeOtroDocente_Responde404()
    {
        var g = await PrepararAsync(1);
        var dada = await DarAsync(g, g.Alumnos[0], Lunes);
        var (otro, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();

        var lectura = await otro.GetAsync($"/api/v1/grupos/{g.GrupoId}/participaciones");
        var escritura = await otro.PostAsJsonAsync($"/api/v1/grupos/{g.GrupoId}/participaciones", new { id = Guid.NewGuid(), alumnoId = g.Alumnos[0], fecha = Lunes });
        var borrado = await otro.DeleteAsync($"/api/v1/participaciones/{dada}");

        Assert.Equal(HttpStatusCode.NotFound, lectura.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, escritura.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, borrado.StatusCode);
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM participacion WHERE alumno_id = '{g.Alumnos[0]}'"));
    }

    private sealed record Grupo(HttpClient Cliente, Guid GrupoId, Guid PeriodoId, IReadOnlyList<Guid> Alumnos, Guid Actividad);

    /// <summary>Grupo de secundaria con un rubro Examen de 100% y una actividad, en Pro para ver promedios (RN-097).</summary>
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
        var examenes = tipos.EnumerateArray().Single(t => t.GetProperty("nombre").GetString() == "Exámenes").GetProperty("id").GetGuid();
        var rubros = await cliente.PutAsJsonAsync($"/api/v1/periodos/{periodo}/rubros", new
        {
            rubros = new[] { new { nombre = "Examen", tipoRubroId = examenes, peso = 100m, forma = "numerica", califMinima = 5m } },
        });
        Assert.Equal(HttpStatusCode.OK, rubros.StatusCode);
        var rubro = (await rubros.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rubros")[0].GetProperty("id").GetGuid();
        var actividad = await cliente.PostAsJsonAsync($"/api/v1/rubros/{rubro}/actividades", new { nombre = "Examen 1", modo = "individual" });
        Assert.Equal(HttpStatusCode.Created, actividad.StatusCode);
        await cliente.ActivarProAsync(grupoId);
        return new Grupo(cliente, grupoId, periodo, ids, (await actividad.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
    }

    private static async Task CalificarAsync(Grupo g, Guid alumno, decimal valor)
    {
        var respuesta = await g.Cliente.PutAsJsonAsync("/api/v1/calificaciones", new
        {
            celdas = new[] { new { actividadId = g.Actividad, alumnoId = alumno, valor = (decimal?)valor, marca = "ninguna", version = 0 } },
        });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    private static Task<HttpResponseMessage> PostAsync(Grupo g, Guid alumno, DateOnly fecha) =>
        g.Cliente.PostAsJsonAsync($"/api/v1/grupos/{g.GrupoId}/participaciones", new { id = Guid.NewGuid(), alumnoId = alumno, fecha });

    private static async Task<Guid> DarAsync(Grupo g, Guid alumno, DateOnly fecha)
    {
        var respuesta = await PostAsync(g, alumno, fecha);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> EditarValorAsync(Grupo g, decimal valor) =>
        g.Cliente.PutAsJsonAsync($"/api/v1/grupos/{g.GrupoId}", new
        {
            nombre = "A",
            grado = 2,
            materia = "Ciencias",
            califMinima = 5m,
            califAprobatoria = 6m,
            decimalesFinal = 2,
            redondeo = "cercano",
            valorParticipacion = valor,
        });

    private static async Task<JsonElement> MatrizAsync(Grupo g) =>
        await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/periodos/{g.PeriodoId}/calificaciones");

    private static JsonElement Resultado(JsonElement matriz, Guid alumno) =>
        matriz.GetProperty("resultados").EnumerateArray().Single(r => r.GetProperty("alumnoId").GetGuid() == alumno);

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
