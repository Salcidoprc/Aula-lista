using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AulaLista.Application.Operacion;
using AulaLista.Infrastructure.Operacion;
using AulaLista.Infrastructure.Trabajos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace AulaLista.Pruebas.Integracion;

/// <summary>Criterios de aceptación de SPEC-09 (operación y métricas) y el de la fase H6 (costo contra ingreso).</summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class Spec09OperacionPruebas(BaseDeDatosFixture baseDeDatos) : IDisposable
{
    private readonly FabricaApi _fabrica = new(baseDeDatos, registrosDetallados: true);

    public void Dispose() => _fabrica.Dispose();

    [Fact]
    public async Task SPEC09_AdminCreaUnTipoNuevo_LosDocentesLoVenAlCrearUnRubroSinDesplegar()
    {
        var admin = await AdminAsync();
        var g = await GrupoAsync(1);

        // "Participaciones" ya viene sembrado (H3); el ejemplo de la spec que falta es "Laboratorio".
        var creado = await admin.PostAsJsonAsync("/api/v1/admin/tipos-rubro", Tipo("Laboratorio", activo: true));

        Assert.True(creado.StatusCode == HttpStatusCode.Created, await creado.Content.ReadAsStringAsync());
        var tipo = await creado.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("actividades", tipo.GetProperty("comportamiento").GetString());
        var tipos = await g.Cliente.GetFromJsonAsync<JsonElement>("/api/v1/tipos-rubro");
        var visto = Assert.Single(tipos.EnumerateArray(), t => t.GetProperty("nombre").GetString() == "Laboratorio");
        Assert.Equal(tipo.GetProperty("id").GetGuid(), visto.GetProperty("id").GetGuid());
        var rubros = await GuardarRubrosAsync(g, ("Participación en clase", tipo.GetProperty("id").GetGuid(), null));
        Assert.True(rubros.IsSuccessStatusCode, await rubros.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SPEC09_TipoUsadoPorUnRubro_SoloSeDesactivaYLosRubrosSiguenCalculandose()
    {
        var admin = await AdminAsync();
        var g = await GrupoAsync(1);
        var nombre = $"Taller {Random.Shared.Next(1000, 9999)}";
        var tipo = await (await admin.PostAsJsonAsync("/api/v1/admin/tipos-rubro", Tipo(nombre, activo: true))).Content.ReadFromJsonAsync<JsonElement>();
        var tipoId = tipo.GetProperty("id").GetGuid();
        var guardados = await (await GuardarRubrosAsync(g, ("Prácticas", tipoId, null))).Content.ReadFromJsonAsync<JsonElement>();
        var rubroId = guardados.GetProperty("rubros")[0].GetProperty("id").GetGuid();
        var actividad = await (await g.Cliente.PostAsJsonAsync($"/api/v1/rubros/{rubroId}/actividades", new { nombre = "Práctica 1" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        (await g.Cliente.PutAsJsonAsync("/api/v1/calificaciones", new
        {
            celdas = new[] { new { actividadId = actividad.GetProperty("id").GetGuid(), alumnoId = g.Alumnos[0], valor = (decimal?)8.5m, marca = "ninguna", version = 0 } },
        })).EnsureSuccessStatusCode();

        var borrado = await admin.DeleteAsync($"/api/v1/admin/tipos-rubro/{tipoId}");
        var conRubros = await admin.PostAsJsonAsync("/api/v1/admin/tipos-rubro", Tipo(nombre, activo: true));
        var cambioDeComportamiento = await admin.PutAsJsonAsync($"/api/v1/admin/tipos-rubro/{tipoId}", Tipo(nombre, activo: true, "asistencia"));
        var desactivado = await admin.PutAsJsonAsync($"/api/v1/admin/tipos-rubro/{tipoId}", Tipo(nombre, activo: false));

        Assert.False(borrado.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conRubros.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, cambioDeComportamiento.StatusCode);
        Assert.Equal(HttpStatusCode.OK, desactivado.StatusCode);
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM tipo_rubro WHERE id = '{tipoId}'"));
        var catalogo = await g.Cliente.GetFromJsonAsync<JsonElement>("/api/v1/tipos-rubro");
        Assert.DoesNotContain(catalogo.EnumerateArray(), t => t.GetProperty("id").GetGuid() == tipoId);

        // El rubro existente se puede volver a guardar y sigue en el promedio; uno nuevo con el tipo inactivo no.
        var delPeriodo = await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/periodos/{g.PeriodoId}/rubros");
        Assert.Equal(tipoId, Assert.Single(delPeriodo.GetProperty("tiposInactivos").EnumerateArray()).GetProperty("id").GetGuid());
        var otraVez = await GuardarRubrosAsync(g, ("Prácticas", tipoId, rubroId));
        var nuevo = await GuardarRubrosAsync(g, ("Prácticas", tipoId, rubroId), ("Otras prácticas", tipoId, null));
        Assert.True(otraVez.IsSuccessStatusCode, await otraVez.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, nuevo.StatusCode);
        var matriz = await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/periodos/{g.PeriodoId}/calificaciones");
        Assert.Equal(8.5m, matriz.GetProperty("resultados")[0].GetProperty("periodo").GetDecimal());
    }

    [Fact]
    public async Task SPEC09_DocenteSinRolAdmin_RecibeEn404TodoElPanel()
    {
        var (docente, _, _) = await _fabrica.RegistrarDocenteAsync();
        var anonimo = _fabrica.Cliente();
        var tipoId = Guid.NewGuid();
        string[] lecturas = ["resumen", "metricas", "docentes", "costos", "errores", "auditoria", "comportamientos", "tipos-rubro"];

        foreach (var ruta in lecturas)
        {
            Assert.Equal(HttpStatusCode.NotFound, (await docente.GetAsync($"/api/v1/admin/{ruta}")).StatusCode);
        }

        // Ni con un cuerpo inválido se distingue: no llega a validarse.
        Assert.Equal(HttpStatusCode.NotFound, (await docente.PostAsJsonAsync("/api/v1/admin/tipos-rubro", new { nombre = 5 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await docente.PutAsJsonAsync($"/api/v1/admin/tipos-rubro/{tipoId}", Tipo("X", true))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/v1/admin/metricas")).StatusCode);
    }

    [Fact]
    public async Task SPEC09_CambiosEnTiposDeRubro_QuedanEnLaAuditoriaConValoresAntesYDespues()
    {
        var admin = await AdminAsync();
        var nombre = $"Proyecto {Random.Shared.Next(1000, 9999)}";
        var tipo = await (await admin.PostAsJsonAsync("/api/v1/admin/tipos-rubro", Tipo(nombre, activo: true))).Content.ReadFromJsonAsync<JsonElement>();
        var id = tipo.GetProperty("id").GetGuid();
        _fabrica.Reloj.Advance(TimeSpan.FromSeconds(1));
        (await admin.PutAsJsonAsync($"/api/v1/admin/tipos-rubro/{id}", Tipo($"{nombre} final", activo: true))).EnsureSuccessStatusCode();
        _fabrica.Reloj.Advance(TimeSpan.FromSeconds(1));
        (await admin.PutAsJsonAsync($"/api/v1/admin/tipos-rubro/{id}", Tipo($"{nombre} final", activo: false))).EnsureSuccessStatusCode();

        var auditoria = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/auditoria");

        var registros = auditoria.GetProperty("registros").EnumerateArray()
            .Where(r => r.GetProperty("entidadId").GetGuid() == id).ToList();
        Assert.Equal(["tipo_rubro_desactivado", "tipo_rubro_editado", "tipo_rubro_creado"], registros.Select(r => r.GetProperty("accion").GetString()));
        Assert.All(registros, r => Assert.False(string.IsNullOrEmpty(r.GetProperty("correo").GetString())));
        var edicion = Assert.Single(registros[1].GetProperty("cambios").EnumerateArray(), c => c.GetProperty("campo").GetString() == "nombre");
        Assert.Equal(nombre, edicion.GetProperty("antes").GetString());
        Assert.Equal($"{nombre} final", edicion.GetProperty("despues").GetString());
        var baja = Assert.Single(registros[0].GetProperty("cambios").EnumerateArray());
        Assert.Equal(("activo", "true", "false"), (baja.GetProperty("campo").GetString(), baja.GetProperty("antes").GetString(), baja.GetProperty("despues").GetString()));
        Assert.Contains(registros[2].GetProperty("cambios").EnumerateArray(), c => c.GetProperty("campo").GetString() == "nombre"
            && c.GetProperty("antes").ValueKind == JsonValueKind.Null && c.GetProperty("despues").GetString() == nombre);
    }

    [Fact]
    public async Task SPEC09_Panel_MuestraLasSeisMetricasDelPilotoCalculadasDesdeLosEventos()
    {
        var admin = await AdminAsync();
        var antes = Semana(await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/metricas"));
        var g = await GrupoAsync(2, pro: false);
        var tutor = await TutorAsync(g, g.Alumnos[0], "Laura Méndez");

        await BoletasListasAsync(g, editarPrimera: true); // cierra con la prueba Pro (RN-081)
        _fabrica.Reloj.Advance(TimeSpan.FromMinutes(1));
        await PublicarAsync(g);
        await SesionAsync(g, tutor);
        await g.Cliente.ActivarProAsync(g.GrupoId);

        var metricas = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/metricas");
        var despues = Semana(metricas);
        Assert.Equal(30m, metricas.GetProperty("metas").GetProperty("docentesActivos").GetDecimal());
        Assert.Equal(1, despues.GetProperty("docentesActivos").GetInt32() - antes.GetProperty("docentesActivos").GetInt32());
        Assert.Equal(1, despues.GetProperty("docentesQueCierran").GetInt32() - antes.GetProperty("docentesQueCierran").GetInt32());
        Assert.True(despues.GetProperty("minutosCierre").GetDecimal() >= 0m);
        Assert.Equal(2, despues.GetProperty("observacionesAprobadas").GetInt32() - antes.GetProperty("observacionesAprobadas").GetInt32());
        Assert.Equal(1, despues.GetProperty("enlacesAbiertos").GetInt32() - antes.GetProperty("enlacesAbiertos").GetInt32());
        Assert.Equal(1, despues.GetProperty("docentesConPrueba").GetInt32() - antes.GetProperty("docentesConPrueba").GetInt32());
        Assert.Equal(1, despues.GetProperty("docentesQuePagan").GetInt32() - antes.GetProperty("docentesQuePagan").GetInt32());

        // Cada evento guarda solo IDs y números; ninguno lleva nombres (RF-090).
        var eventos = await EventosAsync(g.DocenteId);
        Assert.Equal(
            ["boletas_publicadas", "calificacion_capturada", "cierre_iniciado", "docente_registrado", "enlace_tutor_abierto", "grupo_creado",
                "observacion_aprobada", "observacion_aprobada", "suscripcion_activada"],
            eventos.Select(e => e.Nombre).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(eventos, e => e.Propiedades.Contains("Alumno", StringComparison.Ordinal) || e.Propiedades.Contains("Laura", StringComparison.Ordinal));
        var aprobadas = eventos.Where(e => e.Nombre == "observacion_aprobada").Select(e => JsonDocument.Parse(e.Propiedades).RootElement).ToList();
        Assert.Contains(aprobadas, p => p.GetProperty("distancia_edicion").GetDecimal() == 0m);
        Assert.Contains(aprobadas, p => p.GetProperty("distancia_edicion").GetDecimal() > DistanciaUmbral);
        var publicadas = JsonDocument.Parse(eventos.Single(e => e.Nombre == "boletas_publicadas").Propiedades).RootElement;
        Assert.Equal(2, publicadas.GetProperty("boletas").GetInt32());
        Assert.True(publicadas.GetProperty("minutos_desde_inicio_cierre").GetDecimal() >= 1m);
        var captura = JsonDocument.Parse(eventos.Single(e => e.Nombre == "calificacion_capturada").Propiedades).RootElement;
        Assert.Equal("tabla", captura.GetProperty("vista").GetString());
    }

    [Fact]
    public async Task H6_DocentePagaDosGrupos_ElPanelMuestraSuCostoYSuIngreso()
    {
        var admin = await AdminAsync();
        var g = await GrupoAsync(1);
        var segundo = await OtroGrupoAsync(g, "C");
        await g.Cliente.ActivarProAsync(segundo);
        await BoletasListasAsync(g, editarPrimera: false);

        var costos = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/costos");
        var docentes = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/docentes");

        var suyos = costos.GetProperty("grupos").EnumerateArray().Where(c => c.GetProperty("docenteId").GetGuid() == g.DocenteId).ToList();
        Assert.Equal(2, suyos.Count);
        Assert.All(suyos, c => Assert.Equal("pro", c.GetProperty("plan").GetString()));
        Assert.Equal(108m, suyos.Sum(c => c.GetProperty("ingreso").GetDecimal())); // 79 + 29 (SPEC-08)
        var conIa = suyos.Single(c => c.GetProperty("grupoId").GetGuid() == g.GrupoId);
        Assert.True(conIa.GetProperty("llamadasIa").GetInt32() >= 1);
        Assert.True(conIa.GetProperty("tokensIa").GetInt64() > 0); // centavos: una observación cuesta menos de 0.01 MXN
        Assert.Equal(conIa.GetProperty("costoIa").GetDecimal() + conIa.GetProperty("costoWhatsapp").GetDecimal(), conIa.GetProperty("costo").GetDecimal());
        var docente = Assert.Single(docentes.GetProperty("docentes").EnumerateArray(), d => d.GetProperty("id").GetGuid() == g.DocenteId);
        Assert.Equal("tarjeta", docente.GetProperty("plan").GetString());
        Assert.Equal(2, docente.GetProperty("gruposPro").GetInt32());

        // RN-090: el panel no lleva alumnos, tutores ni observaciones.
        var texto = await admin.GetStringAsync("/api/v1/admin/costos") + await admin.GetStringAsync("/api/v1/admin/docentes");
        Assert.DoesNotContain("Alumno", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Prueba Ríos", texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RF094_Resumen_CuentaUsoActivacionDineroYValorEntregadoSinDatosPersonales()
    {
        var admin = await AdminAsync();
        var antes = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/resumen");
        var g = await GrupoAsync(2, nombres: "Zuriñe"); // Pro pagado: 79 MXN al mes
        await TutorAsync(g, g.Alumnos[0], "Laura Méndez");
        await BoletasListasAsync(g, editarPrimera: false);
        await PublicarAsync(g);
        var aviso = await g.Cliente.PostAsJsonAsync($"/api/v1/grupos/{g.GrupoId}/avisos", new { titulo = "Junta", cuerpo = "El viernes a las 8.", urgente = false });
        Assert.Equal(HttpStatusCode.Created, aviso.StatusCode);

        var despues = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/resumen");
        int Delta(string seccion, string campo) =>
            despues.GetProperty(seccion).GetProperty(campo).GetInt32() - antes.GetProperty(seccion).GetProperty(campo).GetInt32();
        decimal Dinero(JsonElement r, string campo) => r.GetProperty("dinero").GetProperty(campo).GetDecimal();

        Assert.Equal(1, Delta("uso", "docentes"));
        Assert.Equal(1, Delta("uso", "activosDia"));
        Assert.Equal(1, Delta("uso", "activosMes"));
        Assert.Equal(1, Delta("uso", "registrosSemana"));
        Assert.Equal(1, Delta("uso", "grupos"));
        Assert.Equal(2, Delta("uso", "alumnos"));
        Assert.Equal(2, Delta("valor", "boletas"));
        Assert.Equal(2, Delta("valor", "observaciones"));
        Assert.Equal(1, Delta("valor", "avisos"));
        Assert.Equal(1, Delta("valor", "entregas"));
        Assert.Equal(1, Delta("dinero", "docentesQuePagan"));
        Assert.Equal(1, Delta("dinero", "gruposPro"));
        Assert.Equal(79m, Dinero(despues, "mrr") - Dinero(antes, "mrr"));
        Assert.Equal(Dinero(despues, "mrr") - Dinero(despues, "costoMes"), Dinero(despues, "margen"));

        // Cada paso del embudo suma al docente nuevo: registro, grupo, alumnos, calificación, cierre y boletas.
        var pasosAntes = antes.GetProperty("embudo").EnumerateArray().ToList();
        var pasos = despues.GetProperty("embudo").EnumerateArray().ToList();
        Assert.Equal(["registro", "grupo", "alumnos", "calificacion", "cierre", "boletas"], pasos.Select(p => p.GetProperty("paso").GetString()));
        Assert.All(pasos.Zip(pasosAntes), p => Assert.Equal(1, p.First.GetProperty("docentes").GetInt32() - p.Second.GetProperty("docentes").GetInt32()));
        Assert.Equal(100m, pasos[0].GetProperty("porcentaje").GetDecimal());

        var semana = despues.GetProperty("semanas").EnumerateArray().Last();
        var semanaAntes = antes.GetProperty("semanas").EnumerateArray().Last();
        Assert.Equal(12, despues.GetProperty("semanas").GetArrayLength());
        Assert.Equal(1, semana.GetProperty("registros").GetInt32() - semanaAntes.GetProperty("registros").GetInt32());
        Assert.Equal(1, semana.GetProperty("altasPago").GetInt32() - semanaAntes.GetProperty("altasPago").GetInt32());
        Assert.Equal(1, semana.GetProperty("pagan").GetInt32() - semanaAntes.GetProperty("pagan").GetInt32());
        Assert.Equal(2, semana.GetProperty("boletas").GetInt32() - semanaAntes.GetProperty("boletas").GetInt32());

        // RN-090: solo conteos y montos; ni docentes ni alumnos ni tutores.
        var texto = await admin.GetStringAsync("/api/v1/admin/resumen");
        Assert.DoesNotContain("Zuriñe", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Laura", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("@", texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RF094_Retencion_CuentaAlDocenteQueVuelveEnSuSemanaUnoCuandoLaSemanaTermino()
    {
        var admin = await AdminAsync(); // antes de adelantar el reloj: el token se valida con la hora real
        var (docente, _, _) = await _fabrica.RegistrarDocenteAsync();
        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_fabrica.Reloj.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City")).DateTime);
        var cohorte = hoy.AddDays(-(((int)hoy.DayOfWeek + 6) % 7));
        _fabrica.Reloj.Advance(TimeSpan.FromDays(8));
        (await docente.PostAsync("/api/v1/cuentas/sesion/renovar", null)).EnsureSuccessStatusCode();
        _fabrica.Reloj.Advance(TimeSpan.FromDays(14));

        var resumen = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/resumen");

        var suya = Assert.Single(resumen.GetProperty("retencion").EnumerateArray(),
            c => DateOnly.Parse(c.GetProperty("semana").GetString()!, CultureInfo.InvariantCulture) == cohorte);
        Assert.True(suya.GetProperty("docentes").GetInt32() >= 1);
        Assert.True(suya.GetProperty("semana1").GetDecimal() > 0m);
        Assert.Equal(JsonValueKind.Null, suya.GetProperty("semana4").ValueKind); // termina 6 semanas después del lunes
    }

    [Fact]
    public async Task SPEC09_LogsDeLaApi_NoContienenNombresNiTelefonos()
    {
        const string Marca = "Zuriñe-Marcada";
        const string Telefono = "6681234570";
        var g = await GrupoAsync(1, $"{Marca} Alumna");
        var tutor = await TutorAsync(g, g.Alumnos[0], $"{Marca} Tutora", Telefono, $"{Marca.ToLowerInvariant()}@correo.mx");
        await BoletasListasAsync(g, editarPrimera: true);
        await PublicarAsync(g);
        await SesionAsync(g, tutor);
        (await g.Cliente.PostAsJsonAsync($"/api/v1/grupos/{g.GrupoId}/avisos", new { titulo = "Junta", cuerpo = $"Junta con {Marca}", urgente = true }))
            .EnsureSuccessStatusCode();
        (await g.Cliente.PostAsJsonAsync($"/api/v1/grupos/{g.GrupoId}/alumnos", new { nombres = Marca, apellidos = new string('x', 500) }))
            .IsSuccessStatusCode.ToString();
        await g.Cliente.PostAsJsonAsync($"/api/v1/alumnos/{g.Alumnos[0]}/tutores", new { nombre = Marca, telefono = "no-es-telefono" });

        var lineas = _fabrica.Registros.Lineas;

        Assert.NotEmpty(lineas);
        Assert.DoesNotContain(lineas, l => l.Contains("Zuri", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(lineas, l => l.Contains(Telefono, StringComparison.Ordinal) || l.Contains("1234570", StringComparison.Ordinal));
        Assert.DoesNotContain(lineas, l => l.Contains("@correo.mx", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SPEC09_ErroresDeTrabajos_SeVenEnElPanelSinElMensajeDeLaExcepcion()
    {
        var admin = await AdminAsync();
        var (_, docenteId, correo) = await _fabrica.RegistrarDocenteAsync();
        var referencia = Guid.NewGuid();
        await using (var alcance = _fabrica.Services.CreateAsyncScope())
        {
            await alcance.ServiceProvider.GetRequiredService<IRegistroErrores>().RegistrarAsync(
                "whatsapp", "envio_tutores", docenteId, referencia, new HttpRequestException("Falló el envío a 6681234570 de Ana"), CancellationToken.None);
        }

        var errores = await admin.GetStringAsync("/api/v1/admin/errores");

        var error = Assert.Single(JsonDocument.Parse(errores).RootElement.EnumerateArray(), e => e.GetProperty("referenciaId").GetGuid() == referencia);
        Assert.Equal(("whatsapp", correo, "System.Net.Http.HttpRequestException"),
            (error.GetProperty("origen").GetString(), error.GetProperty("correo").GetString(), error.GetProperty("tipoError").GetString()));
        Assert.DoesNotContain("6681234570", errores, StringComparison.Ordinal);
        Assert.DoesNotContain("Ana", errores, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SPEC09_TrabajoFallido_SumaALaMetricaDeSuOrigenParaLaAlerta()
    {
        var (_, docenteId, _) = await _fabrica.RegistrarDocenteAsync();
        using var fallidos = new MetricCollector<long>(
            _fabrica.Services.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>(), MetricasAulaLista.Medidor, "aula_lista.trabajos.fallidos");

        await _fabrica.Services.GetRequiredService<IRegistroErrores>().RegistrarAsync(
            "correo", "envio_tutores", docenteId, Guid.NewGuid(), "correo_rechazado", CancellationToken.None);

        var medicion = Assert.Single(fallidos.GetMeasurementSnapshot());
        Assert.Equal(1, medicion.Value);
        Assert.Equal("correo", medicion.Tags["origen"]);
        Assert.Contains(_fabrica.Registros.Lineas, l => l.Contains("Trabajo fallido", StringComparison.Ordinal) && l.Contains("correo_rechazado", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SPEC09_GrupoArribaDe8Mxn_CuentaEnLaMetricaDeCostoYElLogSoloLlevaSuId()
    {
        var g = await GrupoAsync(1, "Zuriñe");
        var barato = await OtroGrupoAsync(g, "C");
        var hoy = TimeZoneInfo.ConvertTime(_fabrica.Reloj.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City"));
        var mes = new DateOnly(hoy.Year, hoy.Month, 1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        await EjecutarAsync($"""
            INSERT INTO consumo_mensual (docente_id, grupo_id, mes, mensajes_whatsapp, costo_whatsapp)
            VALUES ('{g.DocenteId}', '{g.GrupoId}', '{mes}', 60, 9.60), ('{g.DocenteId}', '{barato}', '{mes}', 10, 1.60)
            ON CONFLICT (grupo_id, mes) DO UPDATE SET mensajes_whatsapp = EXCLUDED.mensajes_whatsapp, costo_whatsapp = EXCLUDED.costo_whatsapp
            """);

        await _fabrica.Services.GetRequiredService<EjecutorTrabajos>().VigilarCostosAsync(CancellationToken.None);

        Assert.True(_fabrica.Services.GetRequiredService<MetricasAulaLista>().GruposCostoAlto >= 1);
        var avisos = _fabrica.Registros.Lineas.Where(l => l.Contains("Costo del mes arriba del umbral", StringComparison.Ordinal)).ToList();
        Assert.Contains(avisos, l => l.Contains(g.GrupoId.ToString(), StringComparison.Ordinal));
        Assert.DoesNotContain(avisos, l => l.Contains(barato.ToString(), StringComparison.Ordinal));
        Assert.DoesNotContain(avisos, l => l.Contains("Zuri", StringComparison.Ordinal) || l.Contains('@', StringComparison.Ordinal));
    }

    [Fact]
    public async Task RNF009_Retencion_BorraGruposArchivadosDeCiclosViejosYConservaLosDemas()
    {
        var viejo = await GrupoAsync(1);
        var tutor = await TutorAsync(viejo, viejo.Alumnos[0], "Tutor del grupo viejo");
        var vigente = await OtroGrupoAsync(viejo, "C");
        (await viejo.Cliente.PostAsync($"/api/v1/grupos/{viejo.GrupoId}/archivar", null)).EnsureSuccessStatusCode();
        var otro = await GrupoAsync(1);
        (await otro.Cliente.PostAsync($"/api/v1/grupos/{otro.GrupoId}/archivar", null)).EnsureSuccessStatusCode();
        await EjecutarAsync($"""
            UPDATE ciclo_escolar SET inicio = '2022-08-29', fin = '2023-07-14'
            WHERE id = (SELECT ciclo_escolar_id FROM grupo WHERE id = '{viejo.GrupoId}')
            """);

        await _fabrica.Services.GetRequiredService<EjecutorTrabajos>().RetencionAsync(CancellationToken.None);

        Assert.Equal(0, await ContarAsync($"SELECT count(*) FROM grupo WHERE id = '{viejo.GrupoId}'"));
        Assert.Equal(0, await ContarAsync($"SELECT count(*) FROM alumno WHERE grupo_id = '{viejo.GrupoId}'"));
        Assert.Equal(0, await ContarAsync($"SELECT count(*) FROM tutor WHERE id = '{tutor}'"));
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM grupo WHERE id = '{vigente}'")); // mismo ciclo, sin archivar
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM grupo WHERE id = '{otro.GrupoId}'")); // archivado, ciclo reciente
    }

    private const decimal DistanciaUmbral = 0.3m;

    private sealed record Grupo(HttpClient Cliente, Guid DocenteId, Guid GrupoId, Guid PeriodoId, Guid CicloId, IReadOnlyList<Guid> Alumnos);

    private sealed record Evento(string Nombre, string Propiedades);

    private static object Tipo(string nombre, bool activo, string comportamiento = "actividades") => new
    {
        nombre,
        descripcion = "Trabajo en clase que se califica por actividad.",
        icono = "lightbulb",
        comportamiento,
        formaSugerida = "numerica",
        nombresSugeridos = new[] { nombre },
        orden = 50,
        activo,
    };

    private static JsonElement Semana(JsonElement metricas) => metricas.GetProperty("semanas").EnumerateArray().Last();

    /// <summary>Un docente verificado que se vuelve administrador con el comando de la API (`--admin`).</summary>
    private async Task<HttpClient> AdminAsync()
    {
        var (cliente, _, correo) = await _fabrica.RegistrarDocenteVerificadoAsync();
        await using var alcance = _fabrica.Services.CreateAsyncScope();
        Assert.True(await alcance.ServiceProvider.GetRequiredService<HacerAdminCasoUso>().EjecutarAsync(correo, CancellationToken.None));
        return cliente;
    }

    private static Task<HttpResponseMessage> GuardarRubrosAsync(Grupo g, params (string Nombre, Guid Tipo, Guid? Id)[] rubros) =>
        g.Cliente.PutAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/rubros", new
        {
            rubros = rubros.Select(r => new
            {
                id = r.Id,
                nombre = r.Nombre,
                tipoRubroId = r.Tipo,
                peso = 100m / rubros.Length,
                forma = "numerica",
                califMinima = 5m,
            }).ToArray(),
            confirmarBorrado = true,
        });

    /// <summary>Un docente con perfil, un grupo "4° B" (en Pro salvo que se pida) y sus alumnos.</summary>
    private async Task<Grupo> GrupoAsync(int alumnos, string nombres = "Alumno", bool pro = true)
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
            var alta = await cliente.PostAsJsonAsync($"/api/v1/grupos/{grupoId}/alumnos", new { nombres = $"{nombres} {i:00}", apellidos = "Prueba Ríos" });
            alta.EnsureSuccessStatusCode();
            ids.Add((await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        }

        if (pro)
        {
            await cliente.ActivarProAsync(grupoId);
        }

        return new Grupo(cliente, docenteId, grupoId, periodo, cicloId, ids);
    }

    private static async Task<Guid> OtroGrupoAsync(Grupo g, string nombre)
    {
        var grupo = await g.Cliente.PostAsJsonAsync("/api/v1/grupos", new { nombre, nivel = "primaria", grado = 4, cicloId = g.CicloId });
        grupo.EnsureSuccessStatusCode();
        return (await grupo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> TutorAsync(Grupo g, Guid alumno, string nombre, string? telefono = null, string? correo = null)
    {
        telefono ??= "668" + Random.Shared.Next(1_000_000, 9_999_999).ToString(CultureInfo.InvariantCulture);
        var vinculo = await g.Cliente.PostAsJsonAsync($"/api/v1/alumnos/{alumno}/tutores", new { nombre, telefono, correo, parentesco = "Mamá" });
        Assert.True(vinculo.IsSuccessStatusCode, await vinculo.Content.ReadAsStringAsync());
        return (await vinculo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tutor").GetProperty("id").GetGuid();
    }

    /// <summary>El tutor abre su enlace en un navegador nuevo.</summary>
    private async Task SesionAsync(Grupo g, Guid tutorId)
    {
        var enlace = await g.Cliente.PostAsJsonAsync($"/api/v1/alumnos/{g.Alumnos[0]}/tutores/{tutorId}/enlace", new { regenerar = false });
        var token = (await enlace.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!.Split('#')[1];
        Assert.Equal(HttpStatusCode.NoContent, (await _fabrica.Cliente().PostAsJsonAsync("/api/v1/tutor/sesion", new { token })).StatusCode);
    }

    /// <summary>
    /// Rubro de 100 capturado desde la tabla, cierre, observaciones de la IA aprobadas (la primera reescrita si se pide) y
    /// boletas generadas.
    /// </summary>
    private static async Task BoletasListasAsync(Grupo g, bool editarPrimera)
    {
        var cliente = g.Cliente;
        var tipos = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/tipos-rubro");
        var tareas = tipos.EnumerateArray().First(t => t.GetProperty("nombre").GetString() == "Tareas").GetProperty("id").GetGuid();
        var rubros = await GuardarRubrosAsync(g, ("Tareas", tareas, null));
        var rubro = (await rubros.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rubros")[0].GetProperty("id").GetGuid();
        var actividad = await cliente.PostAsJsonAsync($"/api/v1/rubros/{rubro}/actividades", new { nombre = "Tarea 1" });
        var actividadId = (await actividad.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await cliente.PutAsJsonAsync("/api/v1/calificaciones", new
        {
            celdas = g.Alumnos.Select(a => new { actividadId, alumnoId = a, valor = (decimal?)9.5m, marca = "ninguna", version = 0, vista = "tabla" }).ToArray(),
        })).EnsureSuccessStatusCode();
        (await cliente.PostAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/cierre", new { usarPrueba = true })).EnsureSuccessStatusCode();
        (await cliente.PostAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/observaciones/generar", new { })).EnsureSuccessStatusCode();
        var cierre = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/periodos/{g.PeriodoId}/cierre");
        var primera = true;
        foreach (var alumno in cierre.GetProperty("alumnos").EnumerateArray())
        {
            var observacion = alumno.GetProperty("observacionId").GetGuid();
            var texto = editarPrimera && primera
                ? "Durante este bimestre cumplió con sus tareas y participó con entusiasmo; le sugiero repasar las tablas en casa."
                : null;
            primera = false;
            (await cliente.PostAsJsonAsync($"/api/v1/observaciones/{observacion}/aprobar", new { texto })).EnsureSuccessStatusCode();
        }

        (await cliente.PostAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/boletas/generar", new { })).EnsureSuccessStatusCode();
    }

    private static async Task PublicarAsync(Grupo g)
    {
        var respuesta = await g.Cliente.PostAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/boletas/publicar", new { alumnoIds = (Guid[]?)null });
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());
    }

    private async Task<List<Evento>> EventosAsync(Guid docenteId)
    {
        await using var conexion = new Npgsql.NpgsqlConnection(baseDeDatos.ConexionAdmin);
        await conexion.OpenAsync();
        await using var comando = new Npgsql.NpgsqlCommand("SELECT nombre, propiedades::text FROM evento_producto WHERE docente_id = @d", conexion);
        comando.Parameters.AddWithValue("d", docenteId);
        await using var lector = await comando.ExecuteReaderAsync();
        var eventos = new List<Evento>();
        while (await lector.ReadAsync())
        {
            eventos.Add(new Evento(lector.GetString(0), lector.GetString(1)));
        }

        return eventos;
    }

    private async Task<long> ContarAsync(string sql)
    {
        await using var conexion = new Npgsql.NpgsqlConnection(baseDeDatos.ConexionAdmin);
        await conexion.OpenAsync();
        await using var comando = new Npgsql.NpgsqlCommand(sql, conexion);
        return (long)(await comando.ExecuteScalarAsync())!;
    }

    private async Task EjecutarAsync(string sql)
    {
        await using var conexion = new Npgsql.NpgsqlConnection(baseDeDatos.ConexionAdmin);
        await conexion.OpenAsync();
        await using var comando = new Npgsql.NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync();
    }
}
