using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AulaLista.Domain.Cierre;

namespace AulaLista.Pruebas.Integracion;

/// <summary>Criterios de aceptación de SPEC-04 (cierre del periodo y observaciones con IA).</summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class Spec04CierrePruebas(BaseDeDatosFixture baseDeDatos) : IDisposable
{
    private static readonly string[] Nombres = ["Ana Sofía", "Mateo", "Regina", "Santiago", "Valentina", "Leonardo", "Ximena", "Emiliano", "Camila", "Diego"];
    private static readonly string[] Apellidos = ["López Ríos", "Cruz Álvarez", "Hernández Paz", "Jiménez Mora", "Flores Vega", "García Luna", "Morales Soto", "Díaz Campos", "Ruiz Peña", "Torres Lara"];

    private static readonly string[] EjemploEstilo = ["Regina, lograste mucho este trimestre."];
    private static readonly string[] CuatroEjemplos = ["a", "b", "c", "d"];
    private static readonly string[] EjemplosConEspacios = [" Un ejemplo. ", ""];

    private readonly FabricaApi _fabrica = new(baseDeDatos);

    public void Dispose() => _fabrica.Dispose();

    [Fact]
    public async Task RN040_IniciarCierre_PideRubrosQueSumen100YCalificaciones()
    {
        var g = await PrepararAsync(3, rubros: false);

        var sinRubros = await g.Cliente.PostAsync($"/api/v1/periodos/{g.PeriodoId}/cierre", null);
        await RubrosAsync(g);
        var sinCalificaciones = await g.Cliente.PostAsync($"/api/v1/periodos/{g.PeriodoId}/cierre", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, sinRubros.StatusCode);
        Assert.Equal("rubros_no_suman_100", await CodigoAsync(sinRubros));
        Assert.Equal("sin_calificaciones", await CodigoAsync(sinCalificaciones));
        var cierre = await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/periodos/{g.PeriodoId}/cierre");
        Assert.Equal("abierto", cierre.GetProperty("estado").GetString());
        Assert.True(cierre.GetProperty("requisitos").GetProperty("rubrosSuman100").GetBoolean());
        Assert.False(cierre.GetProperty("requisitos").GetProperty("hayCalificaciones").GetBoolean());
    }

    [Fact]
    public async Task RF040_IniciarCierre_FijaLosPesosYCadaAlumnoQuedaSinGenerar()
    {
        var g = await PrepararAsync(4);

        var cierre = await IniciarAsync(g);
        var rubros = await g.Cliente.PutAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/rubros", new { rubros = Array.Empty<object>(), confirmarBorrado = true });
        var otraVez = await g.Cliente.PostAsync($"/api/v1/periodos/{g.PeriodoId}/cierre", null);

        Assert.Equal("en_cierre", cierre.GetProperty("estado").GetString());
        Assert.Equal(4, cierre.GetProperty("conteos").GetProperty("sinGenerar").GetInt32());
        Assert.All(cierre.GetProperty("alumnos").EnumerateArray(), a => Assert.NotEqual(JsonValueKind.Null, a.GetProperty("observacionId").ValueKind));
        Assert.Equal(9.5m, cierre.GetProperty("alumnos")[0].GetProperty("calificacion").GetDecimal());
        Assert.Equal("periodo_en_cierre", await CodigoAsync(rubros));
        Assert.Equal(HttpStatusCode.OK, otraVez.StatusCode);
    }

    [Fact]
    public async Task SPEC04_Grupo50_GenerarDejaTodosLosBorradoresListosEnMenosDe3Minutos()
    {
        var g = await PrepararAsync(50);
        await IniciarAsync(g);
        var reloj = Stopwatch.StartNew();

        var respuesta = await g.Cliente.PostAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/observaciones/generar", new { });

        reloj.Stop();
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(reloj.Elapsed < TimeSpan.FromMinutes(3), $"Tardó {reloj.Elapsed}.");
        var cierre = await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/periodos/{g.PeriodoId}/cierre");
        Assert.Equal(50, cierre.GetProperty("conteos").GetProperty("borrador").GetInt32());
        var observaciones = await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/periodos/{g.PeriodoId}/observaciones");
        Assert.All(observaciones.EnumerateArray(), o => Assert.False(string.IsNullOrWhiteSpace(o.GetProperty("texto").GetString())));
        Assert.Equal(50, await ContarAsync($"SELECT count(*) FROM llamada_ia WHERE grupo_id = '{g.GrupoId}' AND resultado = 'ok'"));
        Assert.Equal(50, await ContarAsync($"SELECT count(*) FROM observacion WHERE periodo_id = '{g.PeriodoId}' AND borrador_ia IS NOT NULL AND version_prompt = 'observacion-v1'"));
    }

    [Fact]
    public async Task SPEC04_EnvioALaIA_NoIncluyeNombresTelefonosNiCorreosDeLaBase()
    {
        var g = await PrepararAsync(3);
        var ana = g.Alumnos[0];
        var tutor = await g.Cliente.PostAsJsonAsync($"/api/v1/alumnos/{ana}/tutores", new { nombre = "Laura Treviño", telefono = "6671234567", correo = "laura.trevino@correo.mx", parentesco = "Mamá" });
        Assert.True(tutor.IsSuccessStatusCode, await tutor.Content.ReadAsStringAsync());
        await NotaAsync(g, ana, "Ana Sofía le explicó la fracción a Mateo Cruz; su mamá Laura (667 123 4567) vino.");
        await NotaAsync(g, ana, "Escribir a laura.trevino@correo.mx sobre Regina.");
        var estilo = await g.Cliente.PutAsJsonAsync("/api/v1/docente/estilo-observaciones", new
        {
            longitud = "breve",
            persona = "segunda",
            ejemplos = EjemploEstilo,
        });
        Assert.Equal(HttpStatusCode.OK, estilo.StatusCode);
        await IniciarAsync(g);

        await GenerarAsync(g);

        var enviados = _fabrica.Redactor.Enviados.ToList();
        Assert.Equal(3, enviados.Count);
        string[] prohibidos = ["Ana", "Sofía", "López", "Ríos", "Mateo", "Cruz", "Regina", "Hernández", "Laura", "Treviño", "667", "4567", "@correo"];
        foreach (var enviado in enviados)
        {
            var encontrados = prohibidos.Where(p => enviado.Contains(p, StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.True(encontrados.Count == 0, $"Se enviaron {string.Join(", ", encontrados)}:\n{enviado[^600..]}");
        }

        var seudonimo = await EscalarAsync($"SELECT seudonimo FROM alumno WHERE id = '{ana}'");
        Assert.Contains(enviados, e => e.Contains(seudonimo, StringComparison.Ordinal) && e.Contains("otro alumno;", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RN044_AprobadaYCambiaUnaCalificacion_VuelveABorradorConLaMarca()
    {
        var g = await PrepararAsync(2);
        await IniciarAsync(g);
        await GenerarAsync(g);
        var observacion = await ObservacionAsync(g, g.Alumnos[0]);
        var otra = await ObservacionAsync(g, g.Alumnos[1]);
        await AprobarAsync(g, observacion, "Texto revisado por el docente.");
        await AprobarAsync(g, otra, null);

        await GuardarAsync(g, g.Alumnos[0], 7m);

        var lista = await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/periodos/{g.PeriodoId}/observaciones");
        var cambiada = lista.EnumerateArray().Single(o => o.GetProperty("id").GetGuid() == observacion);
        var intacta = lista.EnumerateArray().Single(o => o.GetProperty("id").GetGuid() == otra);
        Assert.Equal("borrador", cambiada.GetProperty("estado").GetString());
        Assert.True(cambiada.GetProperty("datosCambiaron").GetBoolean());
        Assert.Equal("Texto revisado por el docente.", cambiada.GetProperty("texto").GetString());
        Assert.Equal("aprobada", intacta.GetProperty("estado").GetString());

        var nota = await NotaAsync(g, g.Alumnos[1], "Mejoró su letra.");
        Assert.Equal(HttpStatusCode.Created, nota.StatusCode);
        Assert.Equal("borrador", (await ObservacionDtoAsync(g, otra)).GetProperty("estado").GetString());

        var reaprobada = await AprobarAsync(g, observacion, null);
        Assert.False(reaprobada.GetProperty("datosCambiaron").GetBoolean());
    }

    [Fact]
    public async Task SPEC04_RegeneracionNumero6_SeRechazaConLimiteRegeneraciones()
    {
        var g = await PrepararAsync(1);
        await IniciarAsync(g);
        await GenerarAsync(g);
        var observacion = await ObservacionAsync(g, g.Alumnos[0]);

        for (var i = 0; i < 5; i++)
        {
            var respuesta = await g.Cliente.PostAsJsonAsync($"/api/v1/observaciones/{observacion}/regenerar", new { instruccion = "más breve" });
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }

        var sexta = await g.Cliente.PostAsJsonAsync($"/api/v1/observaciones/{observacion}/regenerar", new { instruccion = "más positiva" });

        Assert.Equal(HttpStatusCode.Conflict, sexta.StatusCode);
        Assert.Equal("limite_regeneraciones", await CodigoAsync(sexta));
        var dto = await ObservacionDtoAsync(g, observacion);
        Assert.Equal(0, dto.GetProperty("regeneracionesRestantes").GetInt32());
        Assert.Equal("más breve", dto.GetProperty("instruccion").GetString());
        Assert.Equal(6, _fabrica.Redactor.Enviados.Count);
        Assert.Contains("\"instruccion_extra\":\"más breve\"", _fabrica.Redactor.Enviados.Last(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SPEC04_RechazoDeLaIA_QuedaSinGenerarYSePuedeEscribirAMano()
    {
        var g = await PrepararAsync(1);
        await IniciarAsync(g);
        _fabrica.Redactor.Fallo = FalloGeneracion.Rechazo;

        var cierre = await GenerarAsync(g);

        var alumno = cierre.GetProperty("alumnos")[0];
        Assert.Equal("sin_generar", alumno.GetProperty("estado").GetString());
        Assert.Equal("rechazo", alumno.GetProperty("fallo").GetString());
        var id = alumno.GetProperty("observacionId").GetGuid();
        var editada = await g.Cliente.PutAsJsonAsync($"/api/v1/observaciones/{id}", new { texto = "La escribí yo." });
        Assert.Equal(HttpStatusCode.OK, editada.StatusCode);
        Assert.Equal("borrador", (await editada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("estado").GetString());
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM llamada_ia WHERE grupo_id = '{g.GrupoId}' AND resultado = 'rechazo'"));
    }

    [Fact]
    public async Task RF046_Reabrir_VuelveABorradorYPermiteEditar()
    {
        var g = await PrepararAsync(1);
        await IniciarAsync(g);
        await GenerarAsync(g);
        var id = await ObservacionAsync(g, g.Alumnos[0]);
        await AprobarAsync(g, id, null);

        var editarAprobada = await g.Cliente.PutAsJsonAsync($"/api/v1/observaciones/{id}", new { texto = "Cambio" });
        var reabierta = await g.Cliente.PostAsync($"/api/v1/observaciones/{id}/reabrir", null);
        var editada = await g.Cliente.PutAsJsonAsync($"/api/v1/observaciones/{id}", new { texto = "Cambio" });

        Assert.Equal("observacion_aprobada", await CodigoAsync(editarAprobada));
        Assert.Equal("borrador", (await reabierta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("estado").GetString());
        Assert.Equal(HttpStatusCode.OK, editada.StatusCode);
    }

    [Fact]
    public async Task RNF004_LimitePorHora_RechazaConLimiteIa()
    {
        using var fabrica = new FabricaApi(baseDeDatos, solicitudesIaPorHora: 3);
        var g = await PrepararAsync(4, fabrica);
        await IniciarAsync(g);

        var todos = await g.Cliente.PostAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/observaciones/generar", new { });
        var tres = await g.Cliente.PostAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/observaciones/generar", new { alumnoIds = g.Alumnos.Take(3) });
        var regenerar = await g.Cliente.PostAsJsonAsync($"/api/v1/observaciones/{await ObservacionAsync(g, g.Alumnos[0])}/regenerar", new { });

        Assert.Equal(HttpStatusCode.TooManyRequests, todos.StatusCode);
        Assert.Equal("limite_ia", await CodigoAsync(todos));
        Assert.Equal(HttpStatusCode.OK, tres.StatusCode);
        Assert.Equal("limite_ia", await CodigoAsync(regenerar));
    }

    [Fact]
    public async Task RF043_Estilo_PorDefectoNormalYTercera_ValidaLoQueSeGuarda()
    {
        var g = await PrepararAsync(0, rubros: false);

        var porDefecto = await g.Cliente.GetFromJsonAsync<JsonElement>("/api/v1/docente/estilo-observaciones");
        var invalido = await g.Cliente.PutAsJsonAsync("/api/v1/docente/estilo-observaciones", new { longitud = "normal", persona = "tercera", ejemplos = CuatroEjemplos });
        var guardado = await g.Cliente.PutAsJsonAsync("/api/v1/docente/estilo-observaciones", new { longitud = "detallada", persona = "segunda", ejemplos = EjemplosConEspacios });

        Assert.Equal(("normal", "tercera", 60, 90), (porDefecto.GetProperty("longitud").GetString(), porDefecto.GetProperty("persona").GetString(), porDefecto.GetProperty("palabrasMinimas").GetInt32(), porDefecto.GetProperty("palabrasMaximas").GetInt32()));
        Assert.Equal("estilo_invalido", await CodigoAsync(invalido));
        var dto = await guardado.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(100, dto.GetProperty("palabrasMinimas").GetInt32());
        Assert.Equal(["Un ejemplo."], dto.GetProperty("ejemplos").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task SPEC04_Aislamiento_ElCierreYLasObservacionesDeOtroDocenteResponden404()
    {
        var g = await PrepararAsync(1);
        await IniciarAsync(g);
        await GenerarAsync(g);
        var id = await ObservacionAsync(g, g.Alumnos[0]);
        var (intruso, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();

        HttpResponseMessage[] respuestas =
        [
            await intruso.GetAsync($"/api/v1/periodos/{g.PeriodoId}/cierre"),
            await intruso.PostAsync($"/api/v1/periodos/{g.PeriodoId}/cierre", null),
            await intruso.PostAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/observaciones/generar", new { }),
            await intruso.GetAsync($"/api/v1/periodos/{g.PeriodoId}/observaciones"),
            await intruso.PutAsJsonAsync($"/api/v1/observaciones/{id}", new { texto = "Intruso" }),
            await intruso.PostAsJsonAsync($"/api/v1/observaciones/{id}/regenerar", new { }),
            await intruso.PostAsJsonAsync($"/api/v1/observaciones/{id}/aprobar", new { }),
            await intruso.PostAsync($"/api/v1/observaciones/{id}/reabrir", null),
        ];

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
        Assert.Equal("borrador", (await ObservacionDtoAsync(g, id)).GetProperty("estado").GetString());
    }

    [Fact]
    public async Task RN041_MientrasNoHayPlanes_UnGrupoGratisPuedeCerrar()
    {
        var g = await PrepararAsync(1);

        var respuesta = await g.Cliente.PostAsync($"/api/v1/periodos/{g.PeriodoId}/cierre", null);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("gratis", await EscalarAsync($"SELECT plan FROM grupo WHERE id = '{g.GrupoId}'"));
    }

    private sealed record Grupo(HttpClient Cliente, Guid GrupoId, Guid PeriodoId, IReadOnlyList<Guid> Alumnos, JsonElement Tipos, Guid? Actividad)
    {
        public Guid Tipo(string nombre) =>
            Tipos.EnumerateArray().Single(t => t.GetProperty("nombre").GetString() == nombre).GetProperty("id").GetGuid();
    }

    private Task<Grupo> PrepararAsync(int alumnos, bool rubros = true) => PrepararAsync(alumnos, _fabrica, rubros);

    /// <summary>Grupo de primaria con alumnos con nombres reales, un rubro de 100 y una actividad con 9.5 para todos.</summary>
    private static async Task<Grupo> PrepararAsync(int alumnos, FabricaApi fabrica, bool rubros = true)
    {
        var (cliente, _, _) = await fabrica.RegistrarDocenteVerificadoAsync();
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
        var g = new Grupo(cliente, grupoId, periodo, ids, tipos, null);
        if (!rubros)
        {
            return g;
        }

        var actividad = await RubrosAsync(g);
        g = g with { Actividad = actividad };
        foreach (var alumno in ids)
        {
            await GuardarAsync(g, alumno, 9.5m);
        }

        return g;
    }

    /// <summary>Un rubro "Tareas" de 100 con la actividad "Tarea 1".</summary>
    private static async Task<Guid> RubrosAsync(Grupo g)
    {
        var rubros = await g.Cliente.PutAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/rubros", new
        {
            rubros = new[] { new { nombre = "Tareas", tipoRubroId = g.Tipo("Tareas"), peso = 100m, forma = "numerica", califMinima = 5m } },
        });
        Assert.Equal(HttpStatusCode.OK, rubros.StatusCode);
        var rubro = (await rubros.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rubros")[0].GetProperty("id").GetGuid();
        var actividad = await g.Cliente.PostAsJsonAsync($"/api/v1/rubros/{rubro}/actividades", new { nombre = "Tarea 1" });
        Assert.Equal(HttpStatusCode.Created, actividad.StatusCode);
        return (await actividad.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task GuardarAsync(Grupo g, Guid alumno, decimal valor)
    {
        var respuesta = await g.Cliente.PutAsJsonAsync("/api/v1/calificaciones", new
        {
            celdas = new[] { new { actividadId = g.Actividad!.Value, alumnoId = alumno, valor = (decimal?)valor, marca = "ninguna", version = 0 } },
        });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Empty((await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rechazadas").EnumerateArray());
    }

    private static Task<HttpResponseMessage> NotaAsync(Grupo g, Guid alumno, string texto) =>
        g.Cliente.PostAsJsonAsync($"/api/v1/alumnos/{alumno}/notas", new { id = Guid.NewGuid(), periodoId = g.PeriodoId, texto, etiqueta = "fortaleza" });

    private static async Task<JsonElement> IniciarAsync(Grupo g)
    {
        var respuesta = await g.Cliente.PostAsync($"/api/v1/periodos/{g.PeriodoId}/cierre", null);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> GenerarAsync(Grupo g)
    {
        var respuesta = await g.Cliente.PostAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/observaciones/generar", new { });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/periodos/{g.PeriodoId}/cierre");
    }

    private static async Task<Guid> ObservacionAsync(Grupo g, Guid alumno) =>
        (await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/periodos/{g.PeriodoId}/cierre"))
            .GetProperty("alumnos").EnumerateArray().Single(a => a.GetProperty("alumnoId").GetGuid() == alumno)
            .GetProperty("observacionId").GetGuid();

    private static async Task<JsonElement> ObservacionDtoAsync(Grupo g, Guid id) =>
        (await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/periodos/{g.PeriodoId}/observaciones"))
            .EnumerateArray().Single(o => o.GetProperty("id").GetGuid() == id);

    private static async Task<JsonElement> AprobarAsync(Grupo g, Guid id, string? texto)
    {
        var respuesta = await g.Cliente.PostAsJsonAsync($"/api/v1/observaciones/{id}/aprobar", new { texto });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

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
