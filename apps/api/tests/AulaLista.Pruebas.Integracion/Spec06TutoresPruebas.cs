using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AulaLista.Pruebas.Integracion;

/// <summary>Criterios de aceptación de SPEC-06 (portal de tutores).</summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class Spec06TutoresPruebas(BaseDeDatosFixture baseDeDatos) : IDisposable
{
    private readonly FabricaApi _fabrica = new(baseDeDatos);

    public void Dispose() => _fabrica.Dispose();

    [Fact]
    public async Task SPEC06_TutorConDosHijosDeDocentesDistintos_AlAbrirSuEnlaceVeAAmbos()
    {
        var telefono = NuevoTelefono();
        var ana = await GrupoConBoletaAsync("Ana Sofía", "López Ríos", "Martha Elena Ruiz", "Laura Treviño", telefono);
        var mateo = await GrupoConBoletaAsync("Mateo", "López Ríos", "Jorge Salas", "Laura T.", telefono);
        var enlace = await EnlaceAsync(ana);
        var tutor = _fabrica.Cliente();

        var canje = await tutor.PostAsJsonAsync("/api/v1/tutor/sesion", new { token = Token(enlace) });

        Assert.Equal(HttpStatusCode.NoContent, canje.StatusCode);
        var hijos = await tutor.GetFromJsonAsync<JsonElement>("/api/v1/tutor/alumnos");
        Assert.Equal("Laura Treviño", hijos.GetProperty("nombre").GetString());
        var lista = hijos.GetProperty("hijos").EnumerateArray().ToList();
        Assert.Equal(2, lista.Count);
        Assert.Equal(["Ana Sofía", "Mateo"], lista.Select(h => h.GetProperty("nombres").GetString()));
        Assert.Equal(["Martha Elena Ruiz", "Jorge Salas"], lista.Select(h => h.GetProperty("docente").GetString()));
        Assert.All(lista, h => Assert.Equal("Bimestre 1", h.GetProperty("ultimaBoleta").GetProperty("unidad").GetString()));

        var detalle = await tutor.GetFromJsonAsync<JsonElement>($"/api/v1/tutor/alumnos/{mateo.Alumno}");
        var boleta = detalle.GetProperty("boletas")[0];
        Assert.Equal(9.5m, boleta.GetProperty("calificacion").GetDecimal());
        Assert.False(string.IsNullOrWhiteSpace(boleta.GetProperty("observacion").GetString()));
        var pdf = await tutor.GetAsync($"/api/v1/boletas/{boleta.GetProperty("id").GetGuid()}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task SPEC06_DocenteQueConsultaAlTutor_SoloVeSuVinculoConSusAlumnos()
    {
        var telefono = NuevoTelefono();
        var ana = await GrupoConBoletaAsync("Ana Sofía", "López Ríos", "Martha Elena Ruiz", "Laura Treviño", telefono);
        var mateo = await GrupoConBoletaAsync("Mateo", "López Ríos", "Jorge Salas", "Laura T.", telefono);
        var tutor = await SesionDeTutorAsync(ana);
        await tutor.GetAsync("/api/v1/tutor/alumnos");

        var suyos = await ana.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/alumnos/{ana.Alumno}");
        var vinculo = Assert.Single(suyos.GetProperty("tutores").EnumerateArray());
        Assert.Equal("Laura Treviño", vinculo.GetProperty("nombre").GetString());
        Assert.NotEqual(JsonValueKind.Null, vinculo.GetProperty("enlace").GetProperty("ultimoAccesoEn").ValueKind);

        // El alumno del otro docente y su boleta no existen para ella.
        Assert.Equal(HttpStatusCode.NotFound, (await ana.Cliente.GetAsync($"/api/v1/alumnos/{mateo.Alumno}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ana.Cliente.GetAsync($"/api/v1/alumnos/{mateo.Alumno}/tutores")).StatusCode);
        var ajeno = await ana.Cliente.PostAsJsonAsync($"/api/v1/alumnos/{mateo.Alumno}/tutores/{mateo.Tutor}/enlace", new { regenerar = false });
        Assert.Equal(HttpStatusCode.NotFound, ajeno.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ana.Cliente.GetAsync($"/api/v1/boletas/{mateo.Boleta}/pdf")).StatusCode);
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM vinculo_tutor WHERE tutor_id = '{ana.Tutor}' AND docente_id = '{ana.DocenteId}'"));
        Assert.Equal(ana.Tutor, mateo.Tutor);
    }

    [Fact]
    public async Task SPEC06_TokenAlteradoORevocado_Responde401SinIndicarSiExistio()
    {
        var ana = await GrupoConBoletaAsync("Ana Sofía", "López Ríos", "Martha Elena Ruiz", "Laura Treviño");
        var primero = Token(await EnlaceAsync(ana));
        var tutor = await SesionDeTutorAsync(ana, primero);

        // RN-063: regenerar revoca el enlace anterior y cierra las sesiones abiertas con él.
        var nuevo = Token(await EnlaceAsync(ana, regenerar: true));

        var alterado = await _fabrica.Cliente().PostAsJsonAsync("/api/v1/tutor/sesion", new { token = primero[..^2] + "xx" });
        var revocado = await _fabrica.Cliente().PostAsJsonAsync("/api/v1/tutor/sesion", new { token = primero });
        Assert.Equal(HttpStatusCode.Unauthorized, alterado.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, revocado.StatusCode);
        var cuerpo = await SinTrazaAsync(revocado);
        Assert.Equal(await SinTrazaAsync(alterado), cuerpo);
        Assert.Contains("\"codigo\",\"Value\":\"enlace_invalido\"", cuerpo, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Unauthorized, (await tutor.GetAsync("/api/v1/tutor/alumnos")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _fabrica.Cliente().GetAsync("/api/v1/tutor/alumnos")).StatusCode);

        var otraVez = await _fabrica.Cliente().PostAsJsonAsync("/api/v1/tutor/sesion", new { token = nuevo });
        Assert.Equal(HttpStatusCode.NoContent, otraVez.StatusCode);
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM auditoria WHERE accion = 'enlace_tutor_regenerado' AND actor_id = '{ana.DocenteId}'"));
        Assert.Equal(0, await ContarAsync($"SELECT count(*) FROM enlace_tutor WHERE token_hash = '{primero}' OR token_hash = '{nuevo}'"));
    }

    [Fact]
    public async Task SPEC06_TutorQuePideElPdfDeUnAlumnoNoVinculado_Recibe404()
    {
        var ana = await GrupoConBoletaAsync("Ana Sofía", "López Ríos", "Martha Elena Ruiz", "Laura Treviño");
        var otro = await GrupoConBoletaAsync("Regina", "Hernández Paz", "Jorge Salas", "Pedro Hernández");
        var tutor = await SesionDeTutorAsync(ana);

        Assert.Equal(HttpStatusCode.NotFound, (await tutor.GetAsync($"/api/v1/boletas/{otro.Boleta}/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await tutor.GetAsync($"/api/v1/tutor/alumnos/{otro.Alumno}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await tutor.GetAsync($"/api/v1/boletas/{ana.Boleta}/pdf")).StatusCode);

        // La cookie del tutor no abre las rutas del docente.
        Assert.Equal(HttpStatusCode.Unauthorized, (await tutor.GetAsync($"/api/v1/alumnos/{ana.Alumno}")).StatusCode);
    }

    [Fact]
    public async Task SPEC06_TutorQueYaAbrioElEnlace_VuelveDentroDe90DiasSinElEnlaceYLaSesionSeRenueva()
    {
        var ana = await GrupoConBoletaAsync("Ana Sofía", "López Ríos", "Martha Elena Ruiz", "Laura Treviño");
        var tutor = await SesionDeTutorAsync(ana);

        _fabrica.Reloj.Advance(TimeSpan.FromDays(80));
        Assert.Equal(HttpStatusCode.OK, (await tutor.GetAsync("/api/v1/tutor/alumnos")).StatusCode);

        // RN-062: el uso corrió el vencimiento; sin uso por más de 90 días ya no entra.
        _fabrica.Reloj.Advance(TimeSpan.FromDays(80));
        Assert.Equal(HttpStatusCode.OK, (await tutor.GetAsync("/api/v1/tutor/alumnos")).StatusCode);
        _fabrica.Reloj.Advance(TimeSpan.FromDays(91));
        Assert.Equal(HttpStatusCode.Unauthorized, (await tutor.GetAsync("/api/v1/tutor/alumnos")).StatusCode);
    }

    [Fact]
    public async Task RN062_ElEnlaceVenceAlTerminarElCicloMas30Dias()
    {
        var ana = await GrupoConBoletaAsync("Ana Sofía", "López Ríos", "Martha Elena Ruiz", "Laura Treviño");

        var enlace = await EnlaceAsync(ana);

        Assert.Equal(new DateTimeOffset(2027, 8, 16, 0, 0, 0, TimeSpan.Zero), enlace.GetProperty("expiraEn").GetDateTimeOffset());
        Assert.StartsWith("https://aula-lista.test/tutor#", enlace.GetProperty("url").GetString());
    }

    [Fact]
    public async Task RN064_ElTutorSoloVeLoPublicadoYLaVersionVigente()
    {
        var ana = await GrupoConBoletaAsync("Ana Sofía", "López Ríos", "Martha Elena Ruiz", "Laura Treviño", publicar: false);
        var tutor = await SesionDeTutorAsync(ana);

        var sinPublicar = await tutor.GetFromJsonAsync<JsonElement>($"/api/v1/tutor/alumnos/{ana.Alumno}");
        Assert.Empty(sinPublicar.GetProperty("boletas").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await tutor.GetAsync($"/api/v1/boletas/{ana.Boleta}/pdf")).StatusCode);

        await PublicarAsync(ana);
        var publicada = await tutor.GetFromJsonAsync<JsonElement>($"/api/v1/tutor/alumnos/{ana.Alumno}");
        var boleta = Assert.Single(publicada.GetProperty("boletas").EnumerateArray());
        foreach (var campo in boleta.EnumerateObject().Select(p => p.Name))
        {
            Assert.Contains(campo, CamposBoletaTutor);
        }
    }

    [Fact]
    public async Task RN065_EnlaceAbiertoEnMasDe5Dispositivos_SeMarcaSospechosoYSeAvisaAlDocenteUnaVez()
    {
        var ana = await GrupoConBoletaAsync("Ana Sofía", "López Ríos", "Martha Elena Ruiz", "Laura Treviño");
        var token = Token(await EnlaceAsync(ana));
        var navegador = _fabrica.Cliente();
        for (var i = 0; i < 3; i++)
        {
            // El mismo navegador reutiliza su sesión: no cuenta como dispositivo nuevo.
            Assert.Equal(HttpStatusCode.NoContent, (await navegador.PostAsJsonAsync("/api/v1/tutor/sesion", new { token })).StatusCode);
        }

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.NoContent, (await _fabrica.Cliente().PostAsJsonAsync("/api/v1/tutor/sesion", new { token })).StatusCode);
        }

        var avisos = _fabrica.Buzon.Correos.Where(c => c.Para == ana.Correo && c.Asunto.Contains("muchos dispositivos", StringComparison.Ordinal)).ToList();
        var aviso = Assert.Single(avisos);
        Assert.Contains($"/alumnos/{ana.Alumno}?seccion=datos", aviso.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Ana", aviso.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Laura", aviso.Texto, StringComparison.Ordinal);
        var ficha = await ana.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/alumnos/{ana.Alumno}");
        Assert.True(ficha.GetProperty("tutores")[0].GetProperty("enlace").GetProperty("sospechoso").GetBoolean());

        // Regenerar quita la marca: el enlace nuevo no es sospechoso.
        await EnlaceAsync(ana, regenerar: true);
        ficha = await ana.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/alumnos/{ana.Alumno}");
        Assert.False(ficha.GetProperty("tutores")[0].GetProperty("enlace").GetProperty("sospechoso").GetBoolean());
    }

    [Fact]
    public async Task RNF001_EnlacesYSesionesDelTutor_NoSeLeenSinSaberSuToken()
    {
        var ana = await GrupoConBoletaAsync("Ana Sofía", "López Ríos", "Martha Elena Ruiz", "Laura Treviño");
        await SesionDeTutorAsync(ana);

        await using var conexion = new Npgsql.NpgsqlConnection(baseDeDatos.ConexionRuntime);
        await conexion.OpenAsync();
        foreach (var tabla in new[] { "enlace_tutor", "sesion_tutor" })
        {
            await using var comando = new Npgsql.NpgsqlCommand($"SELECT count(*) FROM {tabla}", conexion);
            Assert.Equal(0L, (long)(await comando.ExecuteScalarAsync())!);
        }
    }

    private static readonly string[] CamposBoletaTutor =
        ["id", "unidad", "ciclo", "calificacion", "decimalesFinal", "rubros", "observacion", "version", "codigo", "publicadaEn", "escuela"];

    private sealed record Hijo(HttpClient Cliente, Guid DocenteId, string Correo, Guid PeriodoId, Guid Alumno, Guid Tutor, Guid Boleta);

    /// <summary>El cuerpo del error sin el <c>traceId</c>, que cambia en cada solicitud.</summary>
    private static async Task<string> SinTrazaAsync(HttpResponseMessage respuesta)
    {
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        cuerpo!.Remove("traceId");
        return JsonSerializer.Serialize(cuerpo.OrderBy(p => p.Key, StringComparer.Ordinal));
    }

    private static string NuevoTelefono() => "667" + Random.Shared.Next(1_000_000, 9_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Token(JsonElement enlace) => enlace.GetProperty("url").GetString()!.Split('#')[1];

    private static async Task<JsonElement> EnlaceAsync(Hijo h, bool regenerar = false)
    {
        var respuesta = await h.Cliente.PostAsJsonAsync($"/api/v1/alumnos/{h.Alumno}/tutores/{h.Tutor}/enlace", new { regenerar });
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<HttpClient> SesionDeTutorAsync(Hijo h, string? token = null)
    {
        token ??= Token(await EnlaceAsync(h));
        var tutor = _fabrica.Cliente();
        var canje = await tutor.PostAsJsonAsync("/api/v1/tutor/sesion", new { token });
        Assert.Equal(HttpStatusCode.NoContent, canje.StatusCode);
        return tutor;
    }

    private static async Task PublicarAsync(Hijo h)
    {
        var publicar = await h.Cliente.PostAsJsonAsync($"/api/v1/periodos/{h.PeriodoId}/boletas/publicar", new { alumnoIds = (Guid[]?)null });
        Assert.True(publicar.IsSuccessStatusCode, await publicar.Content.ReadAsStringAsync());
    }

    /// <summary>Un docente con un grupo de un alumno, su tutor, rubro de 100, 9.5, observación aprobada y boleta lista.</summary>
    private async Task<Hijo> GrupoConBoletaAsync(
        string nombres, string apellidos, string docente, string tutor, string? telefono = null, bool publicar = true)
    {
        // La base es compartida entre pruebas: cada tutor lleva un teléfono propio salvo que se pida el mismo.
        telefono ??= NuevoTelefono();
        var (cliente, docenteId, correo) = await _fabrica.RegistrarDocenteVerificadoAsync();
        (await cliente.PutAsJsonAsync("/api/v1/docente/perfil", new
        {
            nombre = docente,
            nivel = "primaria",
            escuelaNombre = "Escuela Primaria Benito Juárez",
            entidadFederativa = "sinaloa",
        })).EnsureSuccessStatusCode();
        var ciclo = await cliente.PostAsJsonAsync("/api/v1/ciclos", new { nombre = "2026-2027", inicio = "2026-08-25", fin = "2027-07-16", tipoUnidad = "bimestre" });
        ciclo.EnsureSuccessStatusCode();
        var cicloId = (await ciclo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var grupo = await cliente.PostAsJsonAsync("/api/v1/grupos", new { nombre = "B", nivel = "primaria", grado = 4, cicloId });
        grupo.EnsureSuccessStatusCode();
        var grupoId = (await grupo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var periodo = (await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{grupoId}/periodos"))[0].GetProperty("id").GetGuid();
        var alta = await cliente.PostAsJsonAsync($"/api/v1/grupos/{grupoId}/alumnos", new { nombres, apellidos });
        alta.EnsureSuccessStatusCode();
        var alumno = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var vinculo = await cliente.PostAsJsonAsync($"/api/v1/alumnos/{alumno}/tutores", new { nombre = tutor, telefono, correo = (string?)null, parentesco = "Mamá" });
        Assert.True(vinculo.IsSuccessStatusCode, await vinculo.Content.ReadAsStringAsync());
        var tutorId = (await vinculo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tutor").GetProperty("id").GetGuid();

        var tipos = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/tipos-rubro");
        var tareas = tipos.EnumerateArray().First(t => t.GetProperty("nombre").GetString() == "Tareas").GetProperty("id").GetGuid();
        var rubros = await cliente.PutAsJsonAsync($"/api/v1/periodos/{periodo}/rubros", new
        {
            rubros = new[] { new { nombre = "Tareas", tipoRubroId = tareas, peso = 100m, forma = "numerica", califMinima = 5m } },
        });
        var rubro = (await rubros.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rubros")[0].GetProperty("id").GetGuid();
        var actividad = await cliente.PostAsJsonAsync($"/api/v1/rubros/{rubro}/actividades", new { nombre = "Tarea 1" });
        var actividadId = (await actividad.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await cliente.PutAsJsonAsync("/api/v1/calificaciones", new
        {
            celdas = new[] { new { actividadId, alumnoId = alumno, valor = (decimal?)9.5m, marca = "ninguna", version = 0 } },
        })).EnsureSuccessStatusCode();
        await cliente.ActivarProAsync(grupoId);
        (await cliente.PostAsync($"/api/v1/periodos/{periodo}/cierre", null)).EnsureSuccessStatusCode();
        (await cliente.PostAsJsonAsync($"/api/v1/periodos/{periodo}/observaciones/generar", new { })).EnsureSuccessStatusCode();
        var observacion = (await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/periodos/{periodo}/cierre"))
            .GetProperty("alumnos")[0].GetProperty("observacionId").GetGuid();
        (await cliente.PostAsJsonAsync($"/api/v1/observaciones/{observacion}/aprobar", new { texto = (string?)null })).EnsureSuccessStatusCode();
        (await cliente.PostAsJsonAsync($"/api/v1/periodos/{periodo}/boletas/generar", new { })).EnsureSuccessStatusCode();
        var boletas = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/periodos/{periodo}/boletas");
        var boleta = boletas.GetProperty("alumnos")[0].GetProperty("boletaId").GetGuid();
        var hijo = new Hijo(cliente, docenteId, correo, periodo, alumno, tutorId, boleta);
        if (publicar)
        {
            await PublicarAsync(hijo);
        }

        return hijo;
    }

    private async Task<long> ContarAsync(string sql)
    {
        await using var conexion = new Npgsql.NpgsqlConnection(baseDeDatos.ConexionAdmin);
        await conexion.OpenAsync();
        await using var comando = new Npgsql.NpgsqlCommand(sql, conexion);
        return (long)(await comando.ExecuteScalarAsync())!;
    }
}
