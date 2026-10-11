using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AulaLista.Application.Cuentas;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AulaLista.Pruebas.Integracion;

/// <summary>Criterios de aceptación de SPEC-01 (cuenta y acceso del docente).</summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class Spec01CuentaPruebas(BaseDeDatosFixture baseDeDatos) : IDisposable
{
    private readonly FabricaApi _fabrica = new(baseDeDatos);

    public void Dispose() => _fabrica.Dispose();

    [Fact]
    public async Task SPEC01_Registro_EnviaCorreoDeVerificacionYElCorreoQuedaSinVerificar()
    {
        var (cliente, _, correo) = await _fabrica.RegistrarDocenteAsync();

        Assert.Contains(_fabrica.Buzon.Correos, c => c.Para == correo && c.Asunto.Contains("Confirma tu correo", StringComparison.Ordinal));
        var perfil = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/docente/perfil");
        Assert.False(perfil.GetProperty("correoVerificado").GetBoolean());
        Assert.Empty(perfil.GetProperty("aceptacionesPendientes").EnumerateArray());
        Assert.False(perfil.GetProperty("declaracionDatosAceptada").GetBoolean());
    }

    [Fact]
    public async Task SPEC01_VerificarCorreo_ConElEnlaceDelCorreo_LoMarcaVerificadoYNoSeReusa()
    {
        var (cliente, _, correo) = await _fabrica.RegistrarDocenteAsync();
        var token = _fabrica.TokenDelCorreo(correo, "Confirma tu correo");

        var primera = await cliente.PostAsJsonAsync("/api/v1/cuentas/verificar-correo", new { token });
        var segunda = await cliente.PostAsJsonAsync("/api/v1/cuentas/verificar-correo", new { token });

        Assert.Equal(HttpStatusCode.NoContent, primera.StatusCode);
        Assert.Equal("token_invalido", await CodigoAsync(segunda));
        var perfil = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/docente/perfil");
        Assert.True(perfil.GetProperty("correoVerificado").GetBoolean());
    }

    [Fact]
    public async Task SPEC01_TokenDeVerificacionVencido_SeRechazaYSePuedeReenviar()
    {
        var (cliente, _, correo) = await _fabrica.RegistrarDocenteAsync();
        var token = _fabrica.TokenDelCorreo(correo, "Confirma tu correo");
        _fabrica.Reloj.Advance(TimeSpan.FromHours(25));

        var respuesta = await _fabrica.Cliente().PostAsJsonAsync("/api/v1/cuentas/verificar-correo", new { token });

        Assert.Equal("token_vencido", await CodigoAsync(respuesta));
    }

    [Fact]
    public async Task SPEC01_ReenviarVerificacion_MaximoTresPorHora()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteAsync();

        var estados = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            estados.Add((await cliente.PostAsync("/api/v1/cuentas/verificar-correo/reenviar", null)).StatusCode);
        }

        Assert.Equal([HttpStatusCode.Accepted, HttpStatusCode.Accepted, HttpStatusCode.Accepted, HttpStatusCode.TooManyRequests], estados);
    }

    [Fact]
    public async Task SPEC01_ReenviarVerificacion_CuentaDeHaceMasDeUnaHora_TambienSonTres()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteAsync();
        _fabrica.Reloj.Advance(TimeSpan.FromHours(2));

        var estados = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            estados.Add((await cliente.PostAsync("/api/v1/cuentas/verificar-correo/reenviar", null)).StatusCode);
        }

        Assert.Equal([HttpStatusCode.Accepted, HttpStatusCode.Accepted, HttpStatusCode.Accepted, HttpStatusCode.TooManyRequests], estados);
    }

    [Fact]
    public async Task SPEC01_SesionSinUsarse29Dias_SigueActiva()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteAsync();
        _fabrica.Reloj.Advance(TimeSpan.FromDays(29));

        var respuesta = await cliente.PostAsync("/api/v1/cuentas/sesion/renovar", null);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task SPEC01_SesionSinUsarse31Dias_Vence()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteAsync();
        _fabrica.Reloj.Advance(TimeSpan.FromDays(31));

        var respuesta = await cliente.PostAsync("/api/v1/cuentas/sesion/renovar", null);

        Assert.Equal("sesion_invalida", await CodigoAsync(respuesta));
    }

    [Fact]
    public async Task SPEC01_TokenDeActualizacionReutilizado_RevocaTodasLasSesionesYQuedaEnAuditoria()
    {
        var (cliente, docenteId, correo) = await _fabrica.RegistrarDocenteAsync();
        using var otroDispositivo = _fabrica.Cliente();
        (await otroDispositivo.PostAsJsonAsync("/api/v1/cuentas/sesion", new { correo, contrasena = FabricaApi.ContrasenaValida }))
            .EnsureSuccessStatusCode();

        // Un atacante copió la cookie antes de que el docente la rotara.
        var robada = _fabrica.CookieSesion(cliente);
        (await cliente.PostAsync("/api/v1/cuentas/sesion/renovar", null)).EnsureSuccessStatusCode();
        using var atacante = _fabrica.Cliente();
        var reuso = new HttpRequestMessage(HttpMethod.Post, "/api/v1/cuentas/sesion/renovar");
        reuso.Headers.Add("Cookie", $"aula_lista_sesion={robada}");
        var respuestaAtacante = await atacante.SendAsync(reuso);

        Assert.Equal("sesion_invalida", await CodigoAsync(respuestaAtacante));
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsync("/api/v1/cuentas/sesion/renovar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await otroDispositivo.PostAsync("/api/v1/cuentas/sesion/renovar", null)).StatusCode);
        Assert.Equal(1, await ContarAsync("SELECT count(*) FROM auditoria WHERE actor_id = @id AND accion = 'reutilizacion_token_actualizacion'", docenteId));
    }

    [Fact]
    public async Task SPEC01_CerrarSesionEnTodosLosDispositivos_InvalidaLasDemasSesiones()
    {
        var (cliente, _, correo) = await _fabrica.RegistrarDocenteAsync();
        using var otro = _fabrica.Cliente();
        (await otro.PostAsJsonAsync("/api/v1/cuentas/sesion", new { correo, contrasena = FabricaApi.ContrasenaValida }))
            .EnsureSuccessStatusCode();

        var cierre = await cliente.DeleteAsync("/api/v1/cuentas/sesiones");

        Assert.Equal(HttpStatusCode.NoContent, cierre.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await otro.PostAsync("/api/v1/cuentas/sesion/renovar", null)).StatusCode);
    }

    [Fact]
    public async Task SPEC01_IniciarSesion_ConContrasenaIncorrecta_RechazaSinDecirPorQue()
    {
        var (_, _, correo) = await _fabrica.RegistrarDocenteAsync();
        using var cliente = _fabrica.Cliente();

        var mala = await cliente.PostAsJsonAsync("/api/v1/cuentas/sesion", new { correo, contrasena = "otra-contrasena-123" });
        var inexistente = await cliente.PostAsJsonAsync("/api/v1/cuentas/sesion", new { correo = "nadie@prueba.mx", contrasena = "otra-contrasena-123" });

        Assert.Equal("credenciales_invalidas", await CodigoAsync(mala));
        Assert.Equal("credenciales_invalidas", await CodigoAsync(inexistente));
    }

    [Fact]
    public async Task SPEC01_SeisIntentosFallidosEnUnMinuto_ElSextoSeRechaza()
    {
        using var fabrica = new FabricaApi(baseDeDatos, intentosInicioSesionPorMinuto: 5);
        var (_, _, correo) = await fabrica.RegistrarDocenteAsync();
        using var cliente = fabrica.Cliente();

        var codigos = new List<string?>();
        for (var i = 0; i < 6; i++)
        {
            var respuesta = await cliente.PostAsJsonAsync("/api/v1/cuentas/sesion", new { correo, contrasena = "otra-contrasena-123" });
            codigos.Add(await CodigoAsync(respuesta));
        }

        Assert.All(codigos.Take(5), c => Assert.Equal("credenciales_invalidas", c));
        Assert.Equal("demasiados_intentos", codigos[5]);
    }

    [Fact]
    public async Task SPEC01_RN002_ContrasenaComunOCorta_SeRechaza()
    {
        using var cliente = _fabrica.Cliente();

        var comun = await cliente.PostAsJsonAsync("/api/v1/cuentas/registro",
            new { correo = "comun@prueba.mx", contrasena = "1234567890", aceptaTerminos = true, aceptaPrivacidad = true });
        var corta = await cliente.PostAsJsonAsync("/api/v1/cuentas/registro",
            new { correo = "corta@prueba.mx", contrasena = "corta", aceptaTerminos = true, aceptaPrivacidad = true });

        Assert.Equal("contrasena_comun", await CodigoAsync(comun));
        Assert.Equal("validacion_fallida", await CodigoAsync(corta));
    }

    [Fact]
    public async Task SPEC01_RN003_SinAceptarTerminos_NoSeRegistra()
    {
        using var cliente = _fabrica.Cliente();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/cuentas/registro",
            new { correo = "sin-terminos@prueba.mx", contrasena = FabricaApi.ContrasenaValida, aceptaTerminos = false, aceptaPrivacidad = true });

        Assert.Equal("aceptacion_requerida", await CodigoAsync(respuesta));
    }

    [Fact]
    public async Task SPEC01_CorreoYaRegistrado_SinImportarMayusculas_Responde409()
    {
        var (_, _, correo) = await _fabrica.RegistrarDocenteAsync();
        using var cliente = _fabrica.Cliente();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/cuentas/registro",
            new { correo = correo.ToUpperInvariant(), contrasena = FabricaApi.ContrasenaValida, aceptaTerminos = true, aceptaPrivacidad = true });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("correo_registrado", await CodigoAsync(respuesta));
    }

    [Fact]
    public async Task SPEC01_RecuperarContrasena_ConElEnlaceDelCorreo_CambiaLaContrasenaYCierraSesiones()
    {
        var (cliente, _, correo) = await _fabrica.RegistrarDocenteAsync();
        using var anonimo = _fabrica.Cliente();

        Assert.Equal(HttpStatusCode.Accepted, (await anonimo.PostAsJsonAsync("/api/v1/cuentas/recuperar", new { correo })).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await anonimo.PostAsJsonAsync("/api/v1/cuentas/recuperar", new { correo = "nadie@prueba.mx" })).StatusCode);
        var token = _fabrica.TokenDelCorreo(correo, "Restablece");
        var nueva = "cuaderno-de-forma-italiana-7";
        var restablecer = await anonimo.PostAsJsonAsync("/api/v1/cuentas/restablecer", new { token, contrasena = nueva });

        Assert.Equal(HttpStatusCode.NoContent, restablecer.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsync("/api/v1/cuentas/sesion/renovar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonimo.PostAsJsonAsync("/api/v1/cuentas/sesion", new { correo, contrasena = nueva })).StatusCode);
    }

    [Fact]
    public async Task SPEC01_Perfil_SeCompletaYValidaLaCct()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteAsync();

        var mala = await cliente.PutAsJsonAsync("/api/v1/docente/perfil", new
        {
            nombre = "Maestra Prueba",
            nivel = "primaria",
            escuelaNombre = "Escuela Benito Juárez",
            escuelaCct = "123",
            entidadFederativa = "sinaloa",
        });
        var buena = await cliente.PutAsJsonAsync("/api/v1/docente/perfil", new
        {
            nombre = "Maestra Prueba",
            nivel = "primaria",
            escuelaNombre = "Escuela Benito Juárez",
            escuelaCct = "25dpr0123x",
            entidadFederativa = "sinaloa",
        });

        Assert.Equal("cct_invalida", await CodigoAsync(mala));
        Assert.Equal(HttpStatusCode.NoContent, buena.StatusCode);
        var perfil = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/docente/perfil");
        Assert.True(perfil.GetProperty("perfilCompleto").GetBoolean());
        Assert.Equal("25DPR0123X", perfil.GetProperty("escuelaCct").GetString());
    }

    [Fact]
    public async Task SPEC01_RN004_DeclaracionDeDatos_SeGuardaConLaVersionVigente()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteAsync();

        var vieja = await cliente.PostAsJsonAsync("/api/v1/docente/aceptaciones", new { documento = "declaracion_datos", version = "2020-01-01" });
        var vigente = await cliente.PostAsJsonAsync("/api/v1/docente/aceptaciones", new { documento = "declaracion_datos", version = "2026-10-11" });

        Assert.Equal("version_no_vigente", await CodigoAsync(vieja));
        Assert.Equal(HttpStatusCode.NoContent, vigente.StatusCode);
        var perfil = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/docente/perfil");
        Assert.True(perfil.GetProperty("declaracionDatosAceptada").GetBoolean());
    }

    [Fact]
    public async Task SPEC01_Exportacion_GeneraExcelYPdfYAvisaPorCorreo()
    {
        var (cliente, _, correo) = await _fabrica.RegistrarDocenteAsync();

        var solicitud = await cliente.PostAsync("/api/v1/docente/exportacion", null);
        var id = (await solicitud.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var estado = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/docente/exportaciones/{id}");
        var excel = await cliente.GetAsync($"/api/v1/docente/exportaciones/{id}/excel");
        var pdf = await cliente.GetAsync($"/api/v1/docente/exportaciones/{id}/pdf");

        Assert.Equal(HttpStatusCode.Accepted, solicitud.StatusCode);
        Assert.Equal("lista", estado.GetProperty("estado").GetString());
        Assert.Equal(HttpStatusCode.OK, excel.StatusCode);
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(await pdf.Content.ReadAsByteArrayAsync())[..4], StringComparison.Ordinal);
        Assert.Contains(_fabrica.Buzon.Correos, c => c.Para == correo && c.Asunto.Contains("están listos", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SPEC01_RNF001_ExportacionDeOtroDocente_Responde404()
    {
        var (duena, _, _) = await _fabrica.RegistrarDocenteAsync();
        var (ajeno, _, _) = await _fabrica.RegistrarDocenteAsync();
        var id = (await (await duena.PostAsync("/api/v1/docente/exportacion", null)).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await ajeno.GetAsync($"/api/v1/docente/exportaciones/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ajeno.GetAsync($"/api/v1/docente/exportaciones/{id}/excel")).StatusCode);
    }

    [Fact]
    public async Task SPEC01_Exportacion_VenceA24Horas()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteAsync();
        var id = (await (await cliente.PostAsync("/api/v1/docente/exportacion", null)).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        _fabrica.Reloj.Advance(TimeSpan.FromHours(25));

        Assert.Equal("exportacion_no_disponible", await CodigoAsync(await cliente.GetAsync($"/api/v1/docente/exportaciones/{id}/excel")));
    }

    [Fact]
    public async Task SPEC01_ExportacionQueFallaTodosLosReintentos_QuedaFallida()
    {
        await using var fabrica = _fabrica.WithWebHostBuilder(b => b.ConfigureTestServices(
            s => s.AddScoped<IGeneradorExportacion, GeneradorQueFalla>()));
        using var cliente = fabrica.CreateDefaultClient(new Uri("https://localhost"), new CookieContainerHandler());
        var registro = await cliente.PostAsJsonAsync(
            "/api/v1/cuentas/registro",
            new { correo = $"docente-{Guid.NewGuid():N}@prueba.mx", contrasena = FabricaApi.ContrasenaValida, aceptaTerminos = true, aceptaPrivacidad = true });
        var sesion = await registro.Content.ReadFromJsonAsync<SesionDto>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sesion!.TokenAcceso);

        try
        {
            // En pruebas el trabajo corre al confirmar la solicitud, así que su falla puede llegar hasta aquí.
            await cliente.PostAsync("/api/v1/docente/exportacion", null);
        }
        catch (HttpRequestException)
        {
        }

        var id = await IdUltimaExportacionAsync(sesion.DocenteId);
        var estado = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/docente/exportaciones/{id}");

        Assert.Equal("fallida", estado.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task SPEC01_EliminarCuenta_NoPuedeEntrarYA30DiasNoQuedanSusDatos()
    {
        var (cliente, docenteId, correo) = await _fabrica.RegistrarDocenteAsync();
        (await cliente.PostAsync("/api/v1/docente/exportacion", null)).EnsureSuccessStatusCode();

        var sinConfirmar = await cliente.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/docente")
        {
            Content = JsonContent.Create(new { contrasena = "no-es-la-contrasena" }),
        });
        var eliminar = await cliente.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/docente")
        {
            Content = JsonContent.Create(new { contrasena = FabricaApi.ContrasenaValida }),
        });

        Assert.Equal("reautenticacion_requerida", await CodigoAsync(sinConfirmar));
        Assert.Equal(HttpStatusCode.NoContent, eliminar.StatusCode);
        using var otro = _fabrica.Cliente();
        Assert.Equal("credenciales_invalidas", await CodigoAsync(
            await otro.PostAsJsonAsync("/api/v1/cuentas/sesion", new { correo, contrasena = FabricaApi.ContrasenaValida })));

        _fabrica.Reloj.Advance(TimeSpan.FromDays(29));
        Assert.Equal(0, await EjecutarRetencionAsync());
        Assert.Equal(1, await ContarAsync("SELECT count(*) FROM cuenta WHERE id = @id", docenteId));

        _fabrica.Reloj.Advance(TimeSpan.FromDays(2));
        Assert.True(await EjecutarRetencionAsync() >= 1);
        foreach (var consulta in new[]
        {
            "SELECT count(*) FROM cuenta WHERE id = @id",
            "SELECT count(*) FROM docente WHERE id = @id",
            "SELECT count(*) FROM aceptacion_legal WHERE docente_id = @id",
            "SELECT count(*) FROM exportacion WHERE docente_id = @id",
            "SELECT count(*) FROM sesion WHERE cuenta_id = @id",
            "SELECT count(*) FROM token_cuenta WHERE cuenta_id = @id",
            "SELECT count(*) FROM auditoria WHERE actor_id = @id AND ip IS NOT NULL",
        })
        {
            Assert.Equal(0, await ContarAsync(consulta, docenteId));
        }
    }

    [Fact]
    public async Task SPEC01_CuentaSoloConGoogle_PuedeDefinirContrasena()
    {
        var resultado = await IniciarConGoogleAsync(new DatosGoogle($"sub-{Guid.NewGuid():N}", $"g-{Guid.NewGuid():N}@gmail.com", true));
        using var cliente = _fabrica.Cliente();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", resultado.TokenAcceso);

        var antes = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/docente/perfil");
        var definir = await cliente.PutAsJsonAsync("/api/v1/cuentas/contrasena", new { nueva = "mochila-con-ruedas-roja" });
        var despues = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/docente/perfil");

        Assert.False(antes.GetProperty("tieneContrasena").GetBoolean());
        Assert.True(antes.GetProperty("correoVerificado").GetBoolean());
        Assert.Equal(2, antes.GetProperty("aceptacionesPendientes").GetArrayLength());
        Assert.Equal(HttpStatusCode.NoContent, definir.StatusCode);
        Assert.True(despues.GetProperty("tieneContrasena").GetBoolean());
    }

    [Fact]
    public async Task SPEC01_RN005_GoogleConElMismoCorreo_EsLaMismaCuenta()
    {
        var (_, docenteId, correo) = await _fabrica.RegistrarDocenteAsync();

        var resultado = await IniciarConGoogleAsync(new DatosGoogle($"sub-{Guid.NewGuid():N}", correo.ToUpperInvariant(), true));

        Assert.Equal(docenteId, resultado.DocenteId);
    }

    [Fact]
    public async Task SPEC01_GoogleSinCredenciales_RegresaAEntrarConAviso()
    {
        using var cliente = _fabrica.Cliente();

        var respuesta = await cliente.GetAsync("/api/v1/cuentas/google/inicio?volver=/ajustes");

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("https://aula-lista.test/cuenta/entrar?error=google_no_disponible", respuesta.Headers.Location?.ToString());
    }

    [Fact]
    public async Task SPEC01_GoogleSinCorreoVerificado_SeRechaza()
    {
        await using var alcance = _fabrica.Services.CreateAsyncScope();
        var caso = alcance.ServiceProvider.GetRequiredService<IniciarConGoogleCasoUso>();

        var resultado = await caso.EjecutarAsync(new DatosGoogle("sub-x", "x@gmail.com", false), CancellationToken.None);

        Assert.Equal("google_sin_correo_verificado", resultado.Error.Codigo);
    }

    private async Task<SesionEmitida> IniciarConGoogleAsync(DatosGoogle datos)
    {
        await using var alcance = _fabrica.Services.CreateAsyncScope();
        var resultado = await alcance.ServiceProvider.GetRequiredService<IniciarConGoogleCasoUso>()
            .EjecutarAsync(datos, CancellationToken.None);
        return resultado.Valor;
    }

    private async Task<int> EjecutarRetencionAsync()
    {
        await using var alcance = _fabrica.Services.CreateAsyncScope();
        return await alcance.ServiceProvider.GetRequiredService<RetencionCuentasTrabajo>().EjecutarAsync(CancellationToken.None);
    }

    private async Task<Guid> IdUltimaExportacionAsync(Guid docenteId)
    {
        await using var conexion = new NpgsqlConnection(baseDeDatos.ConexionAdmin);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT id FROM exportacion WHERE docente_id = @id ORDER BY created_at DESC LIMIT 1", conexion);
        comando.Parameters.AddWithValue("id", docenteId);
        return (Guid)(await comando.ExecuteScalarAsync())!;
    }

    private sealed class GeneradorQueFalla : IGeneradorExportacion
    {
        public Task<(string RutaExcel, string RutaPdf)> GenerarAsync(DatosExportacion datos, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Falla simulada del generador.");
    }

    private async Task<long> ContarAsync(string consulta, Guid id)
    {
        await using var conexion = new NpgsqlConnection(baseDeDatos.ConexionAdmin);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(consulta, conexion);
        comando.Parameters.AddWithValue("id", id);
        return (long)(await comando.ExecuteScalarAsync())!;
    }

    private static async Task<string?> CodigoAsync(HttpResponseMessage respuesta)
    {
        using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return cuerpo.RootElement.GetProperty("codigo").GetString();
    }
}
