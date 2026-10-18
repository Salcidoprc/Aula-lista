using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AulaLista.Pruebas.Integracion;

/// <summary>Criterios de aceptación de SPEC-07 (avisos y notificaciones) y RN-054 (aviso de boleta disponible).</summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class Spec07AvisosPruebas(BaseDeDatosFixture baseDeDatos) : IDisposable
{
    private readonly FabricaApi _fabrica = new(baseDeDatos);

    public void Dispose() => _fabrica.Dispose();

    [Fact]
    public async Task SPEC07_AvisoNoUrgenteA30Tutores_NoSaleWhatsappYLesLlegaALosQueTienenPushOCorreo()
    {
        var g = await GrupoAsync(30);
        var tutores = new List<Tutor>();
        for (var i = 0; i < 30; i++)
        {
            tutores.Add(await TutorAsync(g, g.Alumnos[i], $"Tutor {i:00}", i < 10 ? $"tutor{i}-{Guid.NewGuid():N}@correo.mx" : null));
        }

        foreach (var t in tutores.Skip(10).Take(5))
        {
            await ActivarPushAsync(await SesionAsync(g, t));
        }

        var aviso = await CrearAvisoAsync(g, "Salida temprano", "Mañana salimos a las 12:00.", urgente: false);

        Assert.Equal(30, aviso.GetProperty("destinatarios").GetInt32());
        Assert.DoesNotContain(_fabrica.Whatsapp.Enviados, m => tutores.Any(t => t.Telefono == m.TelefonoE164));
        Assert.Equal(0, await ContarAsync($"SELECT count(*) FROM mensaje_whatsapp WHERE grupo_id = '{g.GrupoId}'"));
        foreach (var t in tutores.Take(10))
        {
            var correo = Assert.Single(_fabrica.Buzon.Correos, c => c.Para == t.Correo);
            Assert.Equal("Aviso del grupo 4° B", correo.Asunto);
            Assert.Contains("Salida temprano", correo.Texto, StringComparison.Ordinal);
            Assert.Contains($"https://aula-lista.test/tutor?alumno={t.Alumno}&ver=avisos#", correo.Texto, StringComparison.Ordinal);

            // RN-019: el correo no lleva el nombre del alumno.
            Assert.DoesNotContain("Alumno", correo.Texto, StringComparison.Ordinal);
        }

        var push = _fabrica.Push.Enviados.Where(p => tutores.Any(t => t.Id == p.TutorId)).ToList();
        Assert.Equal(tutores.Skip(10).Take(5).Select(t => t.Id).Order(), push.Select(p => p.TutorId).Order());
        Assert.All(push, p => Assert.Equal("Salida temprano", p.Mensaje.Cuerpo));
        var entregas = aviso.GetProperty("entregas").EnumerateArray().ToList();
        Assert.Equal(15, entregas.Count(e => e.GetProperty("porCompartir").GetBoolean()));
        Assert.All(entregas, e => Assert.False(e.GetProperty("whatsapp").GetBoolean()));
    }

    [Fact]
    public async Task SPEC07_AvisoUrgenteSinSaldoDeWhatsapp_SalePorPushYCorreoYElRestoQuedaParaCompartirConLaRazon()
    {
        var g = await GrupoAsync(2);
        var conCorreo = await TutorAsync(g, g.Alumnos[0], "Laura Treviño", $"laura-{Guid.NewGuid():N}@correo.mx");
        var sinNada = await TutorAsync(g, g.Alumnos[1], "Pedro Hernández", null);

        // RN-086: hasta 4 urgentes por WhatsApp por tutor al mes.
        for (var i = 0; i < 4; i++)
        {
            var conSaldo = await CrearAvisoAsync(g, $"Urgente {i}", "Revisen el portal.", urgente: true);
            Assert.All(conSaldo.GetProperty("entregas").EnumerateArray(), e => Assert.True(e.GetProperty("whatsapp").GetBoolean()));
        }

        var destinatarios = await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{g.GrupoId}/avisos/destinatarios");
        Assert.All(destinatarios.GetProperty("tutores").EnumerateArray(), t => Assert.Equal(0, t.GetProperty("urgentesRestantes").GetInt32()));

        var aviso = await CrearAvisoAsync(g, "Suspensión de clases", "Mañana no hay clases por la lluvia.", urgente: true);

        var entregas = aviso.GetProperty("entregas").EnumerateArray().ToDictionary(e => e.GetProperty("tutorId").GetGuid());
        Assert.True(entregas[conCorreo.Id].GetProperty("correo").GetBoolean());
        Assert.False(entregas[conCorreo.Id].GetProperty("porCompartir").GetBoolean());
        Assert.True(entregas[sinNada.Id].GetProperty("porCompartir").GetBoolean());
        Assert.All(entregas.Values, e =>
        {
            Assert.False(e.GetProperty("whatsapp").GetBoolean());
            Assert.True(e.GetProperty("sinSaldoWhatsapp").GetBoolean());
        });
        Assert.Equal(4, _fabrica.Whatsapp.Enviados.Count(m => m.TelefonoE164 == sinNada.Telefono));
        Assert.Equal(5, _fabrica.Buzon.Correos.Count(c => c.Para == conCorreo.Correo));

        // RN-073: el docente lo comparte desde su WhatsApp con un enlace que abre los avisos del hijo.
        var avisoId = aviso.GetProperty("id").GetGuid();
        var compartir = await g.Cliente.PostAsync($"/api/v1/avisos/{avisoId}/entregas/{sinNada.Id}/compartir", null);
        Assert.Equal(HttpStatusCode.OK, compartir.StatusCode);
        var url = (await compartir.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!;
        Assert.StartsWith($"https://aula-lista.test/tutor?alumno={sinNada.Alumno}&ver=avisos#", url, StringComparison.Ordinal);
        var detalle = await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/avisos/{avisoId}");
        var compartida = detalle.GetProperty("entregas").EnumerateArray().Single(e => e.GetProperty("tutorId").GetGuid() == sinNada.Id);
        Assert.NotEqual(JsonValueKind.Null, compartida.GetProperty("compartidoEn").ValueKind);

        // RN-074: el WhatsApp del sistema usa la plantilla y no lleva el texto del aviso.
        var urgente = _fabrica.Whatsapp.Enviados.First(m => m.TelefonoE164 == sinNada.Telefono);
        Assert.Equal("aviso_urgente", urgente.Plantilla);
        Assert.Equal("Pedro", urgente.Parametros[0]);
        Assert.Equal("4° B", urgente.Parametros[1]);
        Assert.DoesNotContain(urgente.Parametros, p => p.Contains("Revisen", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SPEC07_TutorQueAbreElAvisoEnElPortal_ElDocenteLoVeLeidoAlMomento()
    {
        var g = await GrupoAsync(1);
        var t = await TutorAsync(g, g.Alumnos[0], "Laura Treviño", null);
        var tutor = await SesionAsync(g, t);
        var aviso = await CrearAvisoAsync(g, "Junta de padres", "El viernes a las 8:00 en el salón.\nTraigan la boleta firmada.", urgente: false);
        var avisoId = aviso.GetProperty("id").GetGuid();

        var hijos = await tutor.GetFromJsonAsync<JsonElement>("/api/v1/tutor/alumnos");
        var hijo = hijos.GetProperty("hijos")[0];
        Assert.Equal("Junta de padres", hijo.GetProperty("ultimoAviso").GetProperty("titulo").GetString());
        Assert.False(hijo.GetProperty("ultimoAviso").GetProperty("leido").GetBoolean());
        Assert.Equal(1, hijo.GetProperty("avisosSinLeer").GetInt32());
        var lista = await tutor.GetFromJsonAsync<JsonElement>($"/api/v1/tutor/alumnos/{t.Alumno}/avisos");
        Assert.Equal("El viernes a las 8:00 en el salón.\nTraigan la boleta firmada.", lista[0].GetProperty("cuerpo").GetString());
        Assert.Equal("Martha Elena Ruiz", lista[0].GetProperty("docente").GetString());

        // RN-075: abrir la pestaña Avisos lo marca leído; el docente lo ve en la siguiente consulta.
        Assert.Equal(HttpStatusCode.NoContent, (await tutor.PostAsync($"/api/v1/tutor/alumnos/{t.Alumno}/avisos/leidos", null)).StatusCode);

        var detalle = await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/avisos/{avisoId}");
        Assert.Equal(1, detalle.GetProperty("leidos").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, detalle.GetProperty("entregas")[0].GetProperty("leidoEn").ValueKind);
        var avisos = await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{g.GrupoId}/avisos");
        Assert.Equal(1, avisos.GetProperty("avisos")[0].GetProperty("leidos").GetInt32());
        hijos = await tutor.GetFromJsonAsync<JsonElement>("/api/v1/tutor/alumnos");
        Assert.Equal(0, hijos.GetProperty("hijos")[0].GetProperty("avisosSinLeer").GetInt32());
    }

    [Fact]
    public async Task SPEC07_WebhookDeWhatsappConFirmaInvalida_Responde401YNoCambiaNingunEstado()
    {
        var g = await GrupoAsync(1);
        var t = await TutorAsync(g, g.Alumnos[0], "Laura Treviño", null);
        await CrearAvisoAsync(g, "Suspensión de clases", "Mañana no hay clases.", urgente: true);
        var idMeta = _fabrica.Whatsapp.Enviados.Single(m => m.TelefonoE164 == t.Telefono).IdMeta;
        var cuerpo = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            entry = new[] { new { changes = new[] { new { value = new { statuses = new[] { new { id = idMeta, status = "delivered" } } } } } } },
        }));

        var sinFirma = await WebhookAsync(cuerpo, null);
        var alterada = await WebhookAsync(cuerpo, Firma(cuerpo, "otro-secreto"));

        Assert.Equal(HttpStatusCode.Unauthorized, sinFirma.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, alterada.StatusCode);
        Assert.Equal("enviado", await EstadoAsync(idMeta));

        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync(cuerpo, Firma(cuerpo, FabricaApi.SecretoWebhook))).StatusCode);
        Assert.Equal("entregado", await EstadoAsync(idMeta));

        // Un evento atrasado no regresa el estado (idempotente).
        var atrasado = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(cuerpo).Replace("delivered", "sent", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync(atrasado, Firma(atrasado, FabricaApi.SecretoWebhook))).StatusCode);
        Assert.Equal("entregado", await EstadoAsync(idMeta));
    }

    [Fact]
    public async Task RN054_PublicarBoletas_AvisaUnaVezPorPeriodoYDevuelveAQuienesNoLesLlega()
    {
        var g = await GrupoAsync(3);
        var laura = await TutorAsync(g, g.Alumnos[0], "Laura Treviño", $"laura-{Guid.NewGuid():N}@correo.mx");
        var pedro = await TutorAsync(g, g.Alumnos[1], "Pedro Hernández", null);

        // Pedro también es tutor del tercer alumno (hermanos).
        await TutorAsync(g, g.Alumnos[2], "Pedro Hernández", null, pedro.Telefono);
        await BoletasListasAsync(g);

        var primera = await PublicarAsync(g, [g.Alumnos[0], g.Alumnos[1]]);

        Assert.Empty(primera.GetProperty("porCompartir").EnumerateArray());
        var boleta = Assert.Single(_fabrica.Whatsapp.Enviados, m => m.TelefonoE164 == pedro.Telefono);
        Assert.Equal("boleta_disponible", boleta.Plantilla);
        Assert.Equal(["Pedro", "Alumno", "Bimestre 1"], boleta.Parametros.Take(3));
        Assert.Single(_fabrica.Whatsapp.Enviados, m => m.TelefonoE164 == laura.Telefono);
        var correo = Assert.Single(_fabrica.Buzon.Correos, c => c.Para == laura.Correo);
        Assert.Equal("Reporte del Bimestre 1 disponible", correo.Asunto);

        // RN-086: un WhatsApp de boleta por tutor y periodo. Sin push ni correo, Pedro queda para compartir.
        var segunda = await PublicarAsync(g, [g.Alumnos[2]]);

        var porCompartir = Assert.Single(segunda.GetProperty("porCompartir").EnumerateArray());
        Assert.Equal(pedro.Id, porCompartir.GetProperty("tutorId").GetGuid());
        Assert.Single(_fabrica.Whatsapp.Enviados, m => m.TelefonoE164 == pedro.Telefono);
        var boletaId = porCompartir.GetProperty("boletaId").GetGuid();
        var compartir = await g.Cliente.PostAsync($"/api/v1/boletas/{boletaId}/tutores/{pedro.Id}/compartir", null);
        Assert.Equal(HttpStatusCode.OK, compartir.StatusCode);
        var url = (await compartir.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!;
        Assert.StartsWith($"https://aula-lista.test/tutor?alumno={g.Alumnos[2]}#", url, StringComparison.Ordinal);
        Assert.Equal(2, await ContarAsync($"SELECT count(*) FROM mensaje_whatsapp WHERE grupo_id = '{g.GrupoId}' AND plantilla = 'boleta_disponible'"));
    }

    [Fact]
    public async Task RN076_AvisoBorrado_DesapareceDelPortalYQuedaEnAuditoria()
    {
        var g = await GrupoAsync(1);
        var t = await TutorAsync(g, g.Alumnos[0], "Laura Treviño", null);
        var tutor = await SesionAsync(g, t);
        var avisoId = (await CrearAvisoAsync(g, "Salida temprano", "A las 12:00.", urgente: false)).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await g.Cliente.DeleteAsync($"/api/v1/avisos/{avisoId}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await g.Cliente.GetAsync($"/api/v1/avisos/{avisoId}")).StatusCode);
        Assert.Empty((await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{g.GrupoId}/avisos")).GetProperty("avisos").EnumerateArray());
        Assert.Empty((await tutor.GetFromJsonAsync<JsonElement>($"/api/v1/tutor/alumnos/{t.Alumno}/avisos")).EnumerateArray());
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM auditoria WHERE accion = 'aviso_borrado' AND entidad_id = '{avisoId}' AND actor_id = '{g.DocenteId}'"));
    }

    [Fact]
    public async Task RF064_ElTutorRegistraSuCorreoYActivaPush_YUnaSuscripcionAOtroServidorSeRechaza()
    {
        var g = await GrupoAsync(1);
        var t = await TutorAsync(g, g.Alumnos[0], "Laura Treviño", null);
        var tutor = await SesionAsync(g, t);
        var correo = $"laura-{Guid.NewGuid():N}@correo.mx";

        var contacto = await tutor.GetFromJsonAsync<JsonElement>("/api/v1/tutor/contacto");
        Assert.Equal(JsonValueKind.Null, contacto.GetProperty("correo").ValueKind);
        Assert.Equal(87, contacto.GetProperty("clavePush").GetString()!.Length);
        var guardar = await tutor.PutAsJsonAsync("/api/v1/tutor/contacto", new { correo = correo.ToUpperInvariant() });
        Assert.Equal(HttpStatusCode.OK, guardar.StatusCode);
        Assert.Equal(correo, (await guardar.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("correo").GetString());
        var invalido = await tutor.PutAsJsonAsync("/api/v1/tutor/contacto", new { correo = "laura@" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalido.StatusCode);

        var interno = await tutor.PostAsJsonAsync("/api/v1/tutor/push", Suscripcion("https://169.254.169.254/latest"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, interno.StatusCode);
        await ActivarPushAsync(tutor);

        var destinatarios = await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{g.GrupoId}/avisos/destinatarios");
        var suyo = destinatarios.GetProperty("tutores")[0];
        Assert.True(suyo.GetProperty("push").GetBoolean());
        Assert.True(suyo.GetProperty("correo").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await tutor.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/tutor/push")
        {
            Content = JsonContent.Create(new { endpoint = Suscripcion(null).Endpoint }),
        })).StatusCode);
        destinatarios = await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{g.GrupoId}/avisos/destinatarios");
        Assert.False(destinatarios.GetProperty("tutores")[0].GetProperty("push").GetBoolean());
    }

    [Fact]
    public async Task RNF001_AvisosDeOtroDocenteYDeOtroTutor_Responden404YNoSeLeenSinDueno()
    {
        var g = await GrupoAsync(1);
        var t = await TutorAsync(g, g.Alumnos[0], "Laura Treviño", null);
        var avisoId = (await CrearAvisoAsync(g, "Salida temprano", "A las 12:00.", urgente: true)).GetProperty("id").GetGuid();
        var otro = await GrupoAsync(1);
        var ajeno = await TutorAsync(otro, otro.Alumnos[0], "Pedro Hernández", null);
        await ActivarPushAsync(await SesionAsync(g, t));
        var tutorAjeno = await SesionAsync(otro, ajeno);

        Assert.Equal(HttpStatusCode.NotFound, (await otro.Cliente.GetAsync($"/api/v1/avisos/{avisoId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otro.Cliente.DeleteAsync($"/api/v1/avisos/{avisoId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otro.Cliente.PostAsync($"/api/v1/avisos/{avisoId}/entregas/{t.Id}/compartir", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otro.Cliente.GetAsync($"/api/v1/grupos/{g.GrupoId}/avisos")).StatusCode);
        var crear = await otro.Cliente.PostAsJsonAsync($"/api/v1/grupos/{g.GrupoId}/avisos", new { titulo = "x", cuerpo = "y", urgente = false });
        Assert.Equal(HttpStatusCode.NotFound, crear.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await tutorAjeno.GetAsync($"/api/v1/tutor/alumnos/{t.Alumno}/avisos")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await tutorAjeno.PostAsync($"/api/v1/tutor/alumnos/{t.Alumno}/avisos/leidos", null)).StatusCode);

        // El docente ajeno no ve si el tutor tiene push: no hay vínculo con él.
        var destinatarios = await otro.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{otro.GrupoId}/avisos/destinatarios");
        Assert.False(destinatarios.GetProperty("tutores")[0].GetProperty("push").GetBoolean());

        await using var conexion = new Npgsql.NpgsqlConnection(baseDeDatos.ConexionRuntime);
        await conexion.OpenAsync();
        foreach (var tabla in new[] { "aviso", "entrega_aviso", "suscripcion_push", "mensaje_whatsapp" })
        {
            await using var comando = new Npgsql.NpgsqlCommand($"SELECT count(*) FROM {tabla}", conexion);
            Assert.Equal(0L, (long)(await comando.ExecuteScalarAsync())!);
        }
    }

    [Fact]
    public async Task RN070_RN071_TituloYCuerpoConLimites_YLosElegidosDebenSerTutoresDelGrupo()
    {
        var g = await GrupoAsync(2);
        var laura = await TutorAsync(g, g.Alumnos[0], "Laura Treviño", null);
        await TutorAsync(g, g.Alumnos[1], "Pedro Hernández", null);
        var otro = await GrupoAsync(1);
        var ajeno = await TutorAsync(otro, otro.Alumnos[0], "Rosa Vega", null);

        Assert.Equal("aviso_titulo_invalido", await CodigoAsync(await PostAvisoAsync(g, new string('a', 81), "Cuerpo", null)));
        Assert.Equal("aviso_cuerpo_invalido", await CodigoAsync(await PostAvisoAsync(g, "Título", new string('a', 1001), null)));
        Assert.Equal("aviso_cuerpo_invalido", await CodigoAsync(await PostAvisoAsync(g, "Título", "   ", null)));
        Assert.Equal("aviso_sin_destinatarios", await CodigoAsync(await PostAvisoAsync(g, "Título", "Cuerpo", [ajeno.Id])));
        Assert.Equal("aviso_sin_destinatarios", await CodigoAsync(await PostAvisoAsync(g, "Título", "Cuerpo", [])));

        var elegido = await PostAvisoAsync(g, new string('a', 80), new string('b', 1000), [laura.Id]);
        Assert.Equal(HttpStatusCode.Created, elegido.StatusCode);
        var aviso = await elegido.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(aviso.GetProperty("paraTodos").GetBoolean());
        Assert.Equal(laura.Id, Assert.Single(aviso.GetProperty("entregas").EnumerateArray()).GetProperty("tutorId").GetGuid());
    }

    private sealed record Grupo(HttpClient Cliente, Guid DocenteId, Guid GrupoId, Guid PeriodoId, IReadOnlyList<Guid> Alumnos);

    private sealed record Tutor(Guid Id, Guid Alumno, string Telefono, string? Correo);

    private static string NuevoTelefono() => "668" + Random.Shared.Next(1_000_000, 9_999_999).ToString(CultureInfo.InvariantCulture);

    private static string Firma(byte[] cuerpo, string secreto) =>
        "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secreto), cuerpo));

    private static SuscripcionDePrueba Suscripcion(string? endpoint) => new(
        endpoint ?? "https://fcm.googleapis.com/fcm/send/prueba-de-aula-lista",
        "BNcRdreALRFXTkOOUHK1EtK2wtaz5Ry4YfYCA_0QTpQtUbVlUls0VJXg7A8u-Ts1XbjhazAkj7I99e8QcYP7DkM",
        "tBHItJI5svbpez7KI4CCXg");

    private static async Task ActivarPushAsync(HttpClient tutor)
    {
        var respuesta = await tutor.PostAsJsonAsync("/api/v1/tutor/push", Suscripcion(null));
        Assert.True(respuesta.StatusCode == HttpStatusCode.NoContent, await respuesta.Content.ReadAsStringAsync());
    }

    private Task<HttpResponseMessage> WebhookAsync(byte[] cuerpo, string? firma)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/whatsapp") { Content = new ByteArrayContent(cuerpo) };
        if (firma is not null)
        {
            solicitud.Headers.Add("X-Hub-Signature-256", firma);
        }

        return _fabrica.Cliente().SendAsync(solicitud);
    }

    private static Task<HttpResponseMessage> PostAvisoAsync(Grupo g, string titulo, string cuerpo, Guid[]? tutorIds) =>
        g.Cliente.PostAsJsonAsync($"/api/v1/grupos/{g.GrupoId}/avisos", new { titulo, cuerpo, urgente = false, tutorIds });

    private static async Task<JsonElement> CrearAvisoAsync(Grupo g, string titulo, string cuerpo, bool urgente)
    {
        var respuesta = await g.Cliente.PostAsJsonAsync($"/api/v1/grupos/{g.GrupoId}/avisos", new { titulo, cuerpo, urgente });
        Assert.True(respuesta.StatusCode == HttpStatusCode.Created, await respuesta.Content.ReadAsStringAsync());
        var aviso = await respuesta.Content.ReadFromJsonAsync<JsonElement>();

        // La respuesta se arma antes del envío; el detalle trae el estado de cada WhatsApp ya enviado.
        return await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/avisos/{aviso.GetProperty("id").GetGuid()}");
    }

    private static async Task<string?> CodigoAsync(HttpResponseMessage respuesta) =>
        (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetString();

    private async Task<HttpClient> SesionAsync(Grupo g, Tutor t)
    {
        var enlace = await g.Cliente.PostAsJsonAsync($"/api/v1/alumnos/{t.Alumno}/tutores/{t.Id}/enlace", new { regenerar = false });
        var token = (await enlace.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!.Split('#')[1];
        var tutor = _fabrica.Cliente();
        Assert.Equal(HttpStatusCode.NoContent, (await tutor.PostAsJsonAsync("/api/v1/tutor/sesion", new { token })).StatusCode);
        return tutor;
    }

    private static async Task<Tutor> TutorAsync(Grupo g, Guid alumno, string nombre, string? correo, string? telefono = null)
    {
        telefono ??= NuevoTelefono();
        var vinculo = await g.Cliente.PostAsJsonAsync($"/api/v1/alumnos/{alumno}/tutores", new { nombre, telefono, correo, parentesco = "Mamá" });
        Assert.True(vinculo.IsSuccessStatusCode, await vinculo.Content.ReadAsStringAsync());
        var tutor = (await vinculo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tutor");
        return new Tutor(tutor.GetProperty("id").GetGuid(), alumno, tutor.GetProperty("telefono").GetString()!, correo);
    }

    /// <summary>Un docente con su perfil, un grupo "4° B" y sus alumnos "Alumno NN".</summary>
    private async Task<Grupo> GrupoAsync(int alumnos)
    {
        var (cliente, docenteId, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        (await cliente.PutAsJsonAsync("/api/v1/docente/perfil", new
        {
            nombre = "Martha Elena Ruiz",
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
        var ids = new List<Guid>();
        for (var i = 0; i < alumnos; i++)
        {
            var alta = await cliente.PostAsJsonAsync($"/api/v1/grupos/{grupoId}/alumnos", new { nombres = $"Alumno {i:00}", apellidos = "Prueba Ríos" });
            alta.EnsureSuccessStatusCode();
            ids.Add((await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        }

        await cliente.ActivarProAsync(grupoId);
        return new Grupo(cliente, docenteId, grupoId, periodo, ids);
    }

    /// <summary>Rubro de 100, una calificación por alumno, observaciones aprobadas y boletas generadas.</summary>
    private static async Task BoletasListasAsync(Grupo g)
    {
        var cliente = g.Cliente;
        var tipos = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/tipos-rubro");
        var tareas = tipos.EnumerateArray().First(t => t.GetProperty("nombre").GetString() == "Tareas").GetProperty("id").GetGuid();
        var rubros = await cliente.PutAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/rubros", new
        {
            rubros = new[] { new { nombre = "Tareas", tipoRubroId = tareas, peso = 100m, forma = "numerica", califMinima = 5m } },
        });
        var rubro = (await rubros.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rubros")[0].GetProperty("id").GetGuid();
        var actividad = await cliente.PostAsJsonAsync($"/api/v1/rubros/{rubro}/actividades", new { nombre = "Tarea 1" });
        var actividadId = (await actividad.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await cliente.PutAsJsonAsync("/api/v1/calificaciones", new
        {
            celdas = g.Alumnos.Select(a => new { actividadId, alumnoId = a, valor = (decimal?)9.5m, marca = "ninguna", version = 0 }).ToArray(),
        })).EnsureSuccessStatusCode();
        (await cliente.PostAsync($"/api/v1/periodos/{g.PeriodoId}/cierre", null)).EnsureSuccessStatusCode();
        (await cliente.PostAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/observaciones/generar", new { })).EnsureSuccessStatusCode();
        var cierre = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/periodos/{g.PeriodoId}/cierre");
        foreach (var alumno in cierre.GetProperty("alumnos").EnumerateArray())
        {
            var observacion = alumno.GetProperty("observacionId").GetGuid();
            (await cliente.PostAsJsonAsync($"/api/v1/observaciones/{observacion}/aprobar", new { texto = (string?)null })).EnsureSuccessStatusCode();
        }

        (await cliente.PostAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/boletas/generar", new { })).EnsureSuccessStatusCode();
    }

    private static async Task<JsonElement> PublicarAsync(Grupo g, Guid[] alumnos)
    {
        var respuesta = await g.Cliente.PostAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/boletas/publicar", new { alumnoIds = alumnos });
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<string> EstadoAsync(string idMeta)
    {
        await using var conexion = new Npgsql.NpgsqlConnection(baseDeDatos.ConexionAdmin);
        await conexion.OpenAsync();
        await using var comando = new Npgsql.NpgsqlCommand("SELECT estado FROM mensaje_whatsapp WHERE id_meta = @id", conexion);
        comando.Parameters.AddWithValue("id", idMeta);
        return (string)(await comando.ExecuteScalarAsync())!;
    }

    private async Task<long> ContarAsync(string sql)
    {
        await using var conexion = new Npgsql.NpgsqlConnection(baseDeDatos.ConexionAdmin);
        await conexion.OpenAsync();
        await using var comando = new Npgsql.NpgsqlCommand(sql, conexion);
        return (long)(await comando.ExecuteScalarAsync())!;
    }

    private sealed record SuscripcionDePrueba(string Endpoint, string P256dh, string Auth);
}
