using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AulaLista.Application.Comun;
using AulaLista.Application.Planes;
using AulaLista.Infrastructure.Trabajos;
using Microsoft.Extensions.DependencyInjection;

namespace AulaLista.Pruebas.Integracion;

/// <summary>Criterios de aceptación de SPEC-08 (planes y pagos) con la pasarela simulada.</summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class Spec08PlanesPruebas(BaseDeDatosFixture baseDeDatos) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] NombresDeGrupo = ["A", "B", "C", "D"];

    private readonly FabricaApi _fabrica = new(baseDeDatos);

    public void Dispose() => _fabrica.Dispose();

    [Fact]
    public async Task RN082_DocenteCon3GruposPro_PagaAlMes79Mas2Por29_137()
    {
        var d = await DocenteAsync(3);

        await d.Cliente.ActivarProAsync([.. d.Grupos.Select(g => g.Id)]);

        var suscripcion = await SuscripcionAsync(d);
        Assert.Equal("activa", suscripcion.GetProperty("estado").GetString());
        Assert.Equal("tarjeta", suscripcion.GetProperty("modalidad").GetString());
        Assert.Equal(137m, suscripcion.GetProperty("totalMensual").GetDecimal());
        Assert.All(suscripcion.GetProperty("grupos").EnumerateArray(), g => Assert.Equal("pro", g.GetProperty("plan").GetProperty("plan").GetString()));
        Assert.Equal(3, await ContarAsync($"SELECT count(*) FROM grupo WHERE docente_id = '{d.DocenteId}' AND plan = 'pro' AND pro_hasta IS NULL"));
    }

    [Fact]
    public async Task RN080_GrupoGratis_NoIniciaElCierreNiGeneraBoletasNiMandaUrgentes()
    {
        var d = await DocenteAsync(1);
        var g = d.Grupos[0];
        await ListoParaCierreAsync(d, g);

        var cierre = await d.Cliente.PostAsync($"/api/v1/periodos/{g.PeriodoId}/cierre", null);
        var urgente = await d.Cliente.PostAsJsonAsync($"/api/v1/grupos/{g.Id}/avisos", new { titulo = "Suspensión", cuerpo = "Mañana no hay clases.", urgente = true });
        var normal = await d.Cliente.PostAsJsonAsync($"/api/v1/grupos/{g.Id}/avisos", new { titulo = "Junta", cuerpo = "El viernes hay junta.", urgente = false });
        var destinatarios = await d.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{g.Id}/avisos/destinatarios");
        var excel = await d.Cliente.GetAsync($"/api/v1/periodos/{g.PeriodoId}/exportacion.xlsx");

        Assert.Equal(HttpStatusCode.Forbidden, cierre.StatusCode);
        Assert.Equal("requiere_pro", await CodigoAsync(cierre));
        Assert.Equal(HttpStatusCode.Forbidden, urgente.StatusCode);
        Assert.Equal("whatsapp_requiere_pro", await CodigoAsync(urgente));
        Assert.Equal(HttpStatusCode.Created, normal.StatusCode);
        Assert.False(destinatarios.GetProperty("urgentesDisponibles").GetBoolean());
        Assert.All(destinatarios.GetProperty("tutores").EnumerateArray(), t => Assert.Equal(0, t.GetProperty("urgentesRestantes").GetInt32()));
        Assert.Equal(HttpStatusCode.OK, excel.StatusCode);
        Assert.Empty(_fabrica.Whatsapp.Enviados);
    }

    [Fact]
    public async Task RN081_DocenteNuevoCon3Grupos_EligeElGrupoDePruebaYSoloEseTieneProConSusDias()
    {
        var d = await DocenteAsync(3);
        var elegido = d.Grupos[1];
        await ListoParaCierreAsync(d, elegido);

        var respuesta = await d.Cliente.PostAsJsonAsync($"/api/v1/periodos/{elegido.PeriodoId}/cierre", new { usarPrueba = true });

        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());
        var cierre = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("en_cierre", cierre.GetProperty("estado").GetString());
        Assert.Equal("prueba", cierre.GetProperty("plan").GetProperty("plan").GetString());
        Assert.Equal(14, cierre.GetProperty("plan").GetProperty("diasPrueba").GetInt32());
        var grupos = await d.Cliente.GetFromJsonAsync<JsonElement>("/api/v1/grupos");
        Assert.Equal(
            ["gratis", "prueba", "gratis"],
            d.Grupos.Select(g => grupos.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == g.Id).GetProperty("planVigente").GetString()));
        var suscripcion = await SuscripcionAsync(d);
        Assert.Equal(elegido.Id, suscripcion.GetProperty("prueba").GetProperty("grupoId").GetGuid());
        Assert.False(suscripcion.GetProperty("pruebaDisponible").GetBoolean());
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM docente WHERE id = '{d.DocenteId}' AND prueba_pro_usada"));
        Assert.Equal(3, await ContarAsync($"SELECT count(*) FROM grupo WHERE docente_id = '{d.DocenteId}' AND plan = 'gratis'"));
    }

    [Fact]
    public async Task RN081_DocenteQueYaUsoSuPrueba_AlIniciarOtroCierreNoSeActivaOtra()
    {
        var d = await DocenteAsync(2);
        await ListoParaCierreAsync(d, d.Grupos[0]);
        await ListoParaCierreAsync(d, d.Grupos[1]);
        (await d.Cliente.PostAsJsonAsync($"/api/v1/periodos/{d.Grupos[0].PeriodoId}/cierre", new { usarPrueba = true })).EnsureSuccessStatusCode();

        var conPrueba = await d.Cliente.PostAsJsonAsync($"/api/v1/periodos/{d.Grupos[1].PeriodoId}/cierre", new { usarPrueba = true });
        var sinPrueba = await d.Cliente.PostAsync($"/api/v1/periodos/{d.Grupos[1].PeriodoId}/cierre", null);

        Assert.Equal(HttpStatusCode.Conflict, conPrueba.StatusCode);
        Assert.Equal("prueba_usada", await CodigoAsync(conPrueba));
        Assert.Equal(HttpStatusCode.Forbidden, sinPrueba.StatusCode);
        var cierre = await d.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/periodos/{d.Grupos[1].PeriodoId}/cierre");
        Assert.Equal("abierto", cierre.GetProperty("estado").GetString());
        Assert.Equal("gratis", cierre.GetProperty("plan").GetProperty("plan").GetString());
        Assert.False(cierre.GetProperty("plan").GetProperty("pruebaDisponible").GetBoolean());
    }

    [Fact]
    public async Task RN081_PruebaSinPublicar_SigueHasta30DiasYAlPublicarTerminaALos14()
    {
        var d = await DocenteAsync(1);
        var g = d.Grupos[0];
        await ListoParaCierreAsync(d, g);
        (await d.Cliente.PostAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/cierre", new { usarPrueba = true })).EnsureSuccessStatusCode();

        _fabrica.Reloj.Advance(TimeSpan.FromDays(20));
        var aLos20 = await PlanAsync(d.DocenteId, g.Id);
        _fabrica.Reloj.Advance(TimeSpan.FromDays(11));
        var aLos31 = await PlanAsync(d.DocenteId, g.Id);

        Assert.Equal(PlanVigente.Prueba, aLos20.Plan);
        Assert.True(aLos20.PruebaHastaPublicar);
        Assert.Equal(PlanVigente.Gratis, aLos31.Plan);
        Assert.False(aLos31.PruebaDisponible);
    }

    [Fact]
    public async Task RN084_DocenteCon2GruposPagaOxxo3Meses_Paga324YPagadoHastaQueda3MesesDespuesDeConfirmar()
    {
        var d = await DocenteAsync(2);

        var sesion = await PlanesDePrueba.PagoSimuladoAsync(d.Cliente, new { grupoIds = d.Grupos.Select(g => g.Id), modalidad = "oxxo", meses = 3 });
        var ficha = await d.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/pagos-simulados/{sesion}");
        var pendiente = await SuscripcionAsync(d);
        _fabrica.Reloj.Advance(TimeSpan.FromDays(2));
        var confirmacion = _fabrica.Reloj.GetUtcNow();
        (await d.Cliente.PostAsync($"/api/v1/pagos-simulados/{sesion}/pagar", null)).EnsureSuccessStatusCode();

        Assert.Equal(324m, ficha.GetProperty("monto").GetDecimal());
        Assert.True(ficha.GetProperty("oxxo").GetBoolean());
        Assert.Equal(14, ficha.GetProperty("referencia").GetString()!.Length);
        Assert.Equal(324m, pendiente.GetProperty("pagoPendiente").GetProperty("monto").GetDecimal());
        Assert.All(pendiente.GetProperty("grupos").EnumerateArray(), g => Assert.Equal("gratis", g.GetProperty("plan").GetProperty("plan").GetString()));
        var pagado = await SuscripcionAsync(d);
        Assert.Equal("prepago_oxxo", pagado.GetProperty("modalidad").GetString());
        Assert.Equal(confirmacion.AddMonths(3), pagado.GetProperty("pagadoHasta").GetDateTimeOffset(), TimeSpan.FromSeconds(1));
        Assert.Equal("pagado", pagado.GetProperty("pagos")[0].GetProperty("estado").GetString());
        Assert.All(pagado.GetProperty("grupos").EnumerateArray(), g => Assert.Equal("pro", g.GetProperty("plan").GetProperty("plan").GetString()));
    }

    [Fact]
    public async Task RN084_FichaOxxoQueVenceSinPago_LosGruposSiguenEnSuPlanAnterior()
    {
        var d = await DocenteAsync(2);
        await d.Cliente.ActivarProAsync(d.Grupos[0].Id);
        (await d.Cliente.PostAsync("/api/v1/docente/suscripcion/portal", null)).EnsureSuccessStatusCode();
        var otro = await DocenteAsync(1);

        var sesion = await PlanesDePrueba.PagoSimuladoAsync(otro.Cliente, new { grupoIds = new[] { otro.Grupos[0].Id }, modalidad = "oxxo", meses = 6 });
        var ajeno = await d.Cliente.PostAsync($"/api/v1/pagos-simulados/{sesion}/pagar", null);
        (await otro.Cliente.PostAsync($"/api/v1/pagos-simulados/{sesion}/rechazar", null)).EnsureSuccessStatusCode();
        var otraVez = await otro.Cliente.PostAsync($"/api/v1/pagos-simulados/{sesion}/pagar", null);

        Assert.Equal(HttpStatusCode.NotFound, ajeno.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, otraVez.StatusCode);
        var suscripcion = await SuscripcionAsync(otro);
        Assert.Equal("ninguna", suscripcion.GetProperty("estado").GetString());
        Assert.Equal("vencido", suscripcion.GetProperty("pagos")[0].GetProperty("estado").GetString());
        Assert.Equal(JsonValueKind.Null, suscripcion.GetProperty("pagoPendiente").ValueKind);
        Assert.Equal("gratis", suscripcion.GetProperty("grupos")[0].GetProperty("plan").GetProperty("plan").GetString());
        Assert.Equal("pro", (await SuscripcionAsync(d)).GetProperty("grupos").EnumerateArray()
            .Single(g => g.GetProperty("grupoId").GetGuid() == d.Grupos[0].Id).GetProperty("plan").GetProperty("plan").GetString());
    }

    [Fact]
    public async Task RN089_PrepagoQueVenceEn7Dias_AvisaEnLaAppYPorCorreoUnaVez()
    {
        var d = await DocenteAsync(1);
        var sesion = await PlanesDePrueba.PagoSimuladoAsync(d.Cliente, new { grupoIds = new[] { d.Grupos[0].Id }, modalidad = "oxxo", meses = 3 });
        (await d.Cliente.PostAsync($"/api/v1/pagos-simulados/{sesion}/pagar", null)).EnsureSuccessStatusCode();
        var hasta = (await SuscripcionAsync(d)).GetProperty("pagadoHasta").GetDateTimeOffset();

        _fabrica.Reloj.SetUtcNow(hasta.AddDays(-7).AddHours(1));
        await RevisarAsync();
        await RevisarAsync();
        var suscripcion = await ComoDocenteAsync(d.DocenteId, sp => sp.GetRequiredService<ObtenerSuscripcionCasoUso>().EjecutarAsync(default));

        Assert.Single(_fabrica.Buzon.Correos, c => c.Para == d.Correo && c.Asunto.Contains("vence en 7 días", StringComparison.Ordinal));
        Assert.Equal(TipoAvisoPago.PrepagoPorVencer, suscripcion.Aviso!.Tipo);
        Assert.Equal(7, suscripcion.Aviso.Dias);
        Assert.True(suscripcion.RenovarPrepago);
    }

    [Fact]
    public async Task RN085_CobroFallidoYPasan7DiasSinPago_LosGruposPasanAGratisSinBorrarLoPublicado()
    {
        var d = await DocenteAsync(1);
        var g = d.Grupos[0];
        await d.Cliente.ActivarProAsync(g.Id);
        await ListoParaCierreAsync(d, g);
        await PublicarTodoAsync(d, g);

        (await d.Cliente.PostAsJsonAsync("/api/v1/pagos-simulados/renovacion", new { pagada = false })).EnsureSuccessStatusCode();
        var enGracia = await SuscripcionAsync(d);
        _fabrica.Reloj.Advance(TimeSpan.FromDays(6));
        await RevisarAsync();
        var alDia6 = await PlanAsync(d.DocenteId, g.Id);
        _fabrica.Reloj.Advance(TimeSpan.FromDays(1).Add(TimeSpan.FromMinutes(1)));
        await RevisarAsync();
        var alDia7 = await PlanAsync(d.DocenteId, g.Id);

        Assert.Equal("en_gracia", enGracia.GetProperty("estado").GetString());
        Assert.Equal(TipoAvisoPago.CobroFallido, enGracia.GetProperty("aviso").GetProperty("tipo").GetString());
        Assert.Equal(PlanVigente.Pro, alDia6.Plan);
        Assert.Equal(PlanVigente.Gratis, alDia7.Plan);
        Assert.Equal("gratis", await TextoAsync($"SELECT plan FROM grupo WHERE id = '{g.Id}'"));
        Assert.Equal("cancelada", await TextoAsync($"SELECT estado FROM suscripcion WHERE docente_id = '{d.DocenteId}'"));

        // Lo publicado no se toca: el portal del tutor solo pide boletas publicadas y vigentes (RN-064).
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM boleta WHERE periodo_id = '{g.PeriodoId}' AND publicada_en IS NOT NULL"));
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM observacion WHERE periodo_id = '{g.PeriodoId}' AND estado = 'publicada'"));
    }

    [Fact]
    public async Task RN087_ElMismoWebhookDosVeces_CambiaElEstadoUnaSolaVez()
    {
        var d = await DocenteAsync(1);
        var sesion = await PlanesDePrueba.PagoSimuladoAsync(d.Cliente, new { grupoIds = new[] { d.Grupos[0].Id }, modalidad = "oxxo", meses = 3 });
        var evento = new EventoPagoRecibido($"evt_{Guid.NewGuid():N}", TipoEventoPago.PagoUnicoPagado, d.DocenteId, Sesion: sesion);
        var cuerpo = JsonSerializer.SerializeToUtf8Bytes(evento, Json);

        var sinFirma = await WebhookAsync(cuerpo, null);
        var firmaAjena = await WebhookAsync(cuerpo, FirmaPagos.Firmar(cuerpo, "otro-secreto", _fabrica.Reloj.GetUtcNow()));
        var vieja = await WebhookAsync(cuerpo, FirmaPagos.Firmar(cuerpo, FabricaApi.SecretoPagos, _fabrica.Reloj.GetUtcNow().AddMinutes(-10)));
        var primera = await WebhookAsync(cuerpo, FirmaPagos.Firmar(cuerpo, FabricaApi.SecretoPagos, _fabrica.Reloj.GetUtcNow()));
        var hasta = (await SuscripcionAsync(d)).GetProperty("pagadoHasta").GetDateTimeOffset();
        _fabrica.Reloj.Advance(TimeSpan.FromMinutes(1));
        var segunda = await WebhookAsync(cuerpo, FirmaPagos.Firmar(cuerpo, FabricaApi.SecretoPagos, _fabrica.Reloj.GetUtcNow()));

        Assert.Equal(HttpStatusCode.Unauthorized, sinFirma.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, firmaAjena.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, vieja.StatusCode);
        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        Assert.Equal(hasta, (await SuscripcionAsync(d)).GetProperty("pagadoHasta").GetDateTimeOffset());
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM evento_pago WHERE id_externo = '{evento.Id}'"));
    }

    [Fact]
    public async Task RN083_AgregarUnGrupoEsInmediatoYQuitarloLoDejaEnProHastaElCorte()
    {
        var d = await DocenteAsync(3);
        await d.Cliente.ActivarProAsync(d.Grupos[0].Id, d.Grupos[1].Id);
        var corte = (await SuscripcionAsync(d)).GetProperty("proximoCobro").GetDateTimeOffset();

        var cambio = await d.Cliente.PutAsJsonAsync("/api/v1/docente/suscripcion/grupos", new { grupoIds = new[] { d.Grupos[0].Id, d.Grupos[2].Id } });
        var igual = await d.Cliente.PutAsJsonAsync("/api/v1/docente/suscripcion/grupos", new { grupoIds = new[] { d.Grupos[2].Id, d.Grupos[0].Id } });
        var otraTarjeta = await d.Cliente.PostAsJsonAsync("/api/v1/docente/suscripcion/checkout", new { grupoIds = new[] { d.Grupos[1].Id }, modalidad = "tarjeta" });

        Assert.True(cambio.StatusCode == HttpStatusCode.OK, await cambio.Content.ReadAsStringAsync());
        var suscripcion = await cambio.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(108m, suscripcion.GetProperty("totalMensual").GetDecimal());
        var planes = suscripcion.GetProperty("grupos").EnumerateArray().ToDictionary(g => g.GetProperty("grupoId").GetGuid(), g => g.GetProperty("plan"));
        Assert.Equal("pro", planes[d.Grupos[2].Id].GetProperty("plan").GetString());
        Assert.Equal("pro", planes[d.Grupos[1].Id].GetProperty("plan").GetString());
        Assert.Equal(corte, planes[d.Grupos[1].Id].GetProperty("proHasta").GetDateTimeOffset());
        Assert.Equal(HttpStatusCode.Conflict, igual.StatusCode);
        Assert.Equal("sin_cambios_plan", await CodigoAsync(igual));
        Assert.Equal("ya_tiene_tarjeta", await CodigoAsync(otraTarjeta));

        _fabrica.Reloj.SetUtcNow(corte.AddMinutes(1));
        Assert.Equal(PlanVigente.Gratis, (await PlanAsync(d.DocenteId, d.Grupos[1].Id)).Plan);
    }

    [Fact]
    public async Task RN089_GrupoAgregadoDuranteUnPrepago_SePagaSoloHastaElFinDelPrepago()
    {
        var d = await DocenteAsync(2);
        var sesion = await PlanesDePrueba.PagoSimuladoAsync(d.Cliente, new { grupoIds = new[] { d.Grupos[0].Id }, modalidad = "oxxo", meses = 3 });
        (await d.Cliente.PostAsync($"/api/v1/pagos-simulados/{sesion}/pagar", null)).EnsureSuccessStatusCode();
        var antes = await SuscripcionAsync(d);
        var hasta = antes.GetProperty("pagadoHasta").GetDateTimeOffset();

        var conTarjeta = await PlanesDePrueba.PagoSimuladoAsync(d.Cliente, new { grupoIds = new[] { d.Grupos[0].Id, d.Grupos[1].Id }, modalidad = "tarjeta" });
        var tarjeta = await d.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/pagos-simulados/{conTarjeta}");
        var agregar = await d.Cliente.PutAsJsonAsync("/api/v1/docente/suscripcion/grupos", new { grupoIds = new[] { d.Grupos[0].Id, d.Grupos[1].Id } });
        var extra = await PlanesDePrueba.PagoSimuladoAsync(d.Cliente, new { grupoIds = new[] { d.Grupos[0].Id, d.Grupos[1].Id }, modalidad = "oxxo", meses = 3 });
        var ficha = await d.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/pagos-simulados/{extra}");
        (await d.Cliente.PostAsync($"/api/v1/pagos-simulados/{extra}/pagar", null)).EnsureSuccessStatusCode();

        Assert.True(antes.GetProperty("agregarAPrepago").GetBoolean());
        Assert.Equal("agregar_grupos", tarjeta.GetProperty("tipo").GetString());
        Assert.False(tarjeta.GetProperty("oxxo").GetBoolean());
        Assert.Equal("agregar_requiere_pago", await CodigoAsync(agregar));
        var dias = (int)Math.Ceiling((hasta - _fabrica.Reloj.GetUtcNow()).TotalDays);
        Assert.Equal(Math.Round(29m * dias / 30, 2, MidpointRounding.AwayFromZero), ficha.GetProperty("monto").GetDecimal());
        Assert.Equal("agregar_grupos", ficha.GetProperty("tipo").GetString());
        var despues = await SuscripcionAsync(d);
        Assert.Equal(hasta, despues.GetProperty("pagadoHasta").GetDateTimeOffset());
        Assert.Equal(108m, despues.GetProperty("totalMensual").GetDecimal());
        Assert.All(despues.GetProperty("grupos").EnumerateArray(), g => Assert.Equal("pro", g.GetProperty("plan").GetProperty("plan").GetString()));
    }

    [Fact]
    public async Task RN084_MontosYModalidades_SeValidanAntesDeAbrirElCheckout()
    {
        var d = await DocenteAsync(1);
        var (intruso, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();

        var sinGrupos = await d.Cliente.PostAsJsonAsync("/api/v1/docente/suscripcion/checkout", new { grupoIds = Array.Empty<Guid>(), modalidad = "tarjeta" });
        var cuatroMeses = await d.Cliente.PostAsJsonAsync("/api/v1/docente/suscripcion/checkout", new { grupoIds = new[] { d.Grupos[0].Id }, modalidad = "oxxo", meses = 4 });
        var ajeno = await intruso.PostAsJsonAsync("/api/v1/docente/suscripcion/checkout", new { grupoIds = new[] { d.Grupos[0].Id }, modalidad = "tarjeta" });
        var sinTarjeta = await d.Cliente.PostAsync("/api/v1/docente/suscripcion/portal", null);
        var planes = await _fabrica.Cliente().GetFromJsonAsync<JsonElement>("/api/v1/planes");

        Assert.Equal("sin_grupos_pro", await CodigoAsync(sinGrupos));
        Assert.Equal("modalidad_invalida", await CodigoAsync(cuatroMeses));
        Assert.Equal("grupo_no_disponible", await CodigoAsync(ajeno));
        Assert.Equal("sin_tarjeta", await CodigoAsync(sinTarjeta));
        Assert.Equal(79m, planes.GetProperty("precioPrimerGrupo").GetDecimal());
        Assert.Equal(29m, planes.GetProperty("precioGrupoAdicional").GetDecimal());
        Assert.Equal([3, 6], planes.GetProperty("mesesPrepago").EnumerateArray().Select(m => m.GetInt32()));
        Assert.Equal(0, await ContarAsync($"SELECT count(*) FROM pago WHERE docente_id = '{d.DocenteId}'"));
    }

    private sealed record GrupoDePrueba(Guid Id, Guid PeriodoId);

    private sealed record Docente(HttpClient Cliente, Guid DocenteId, string Correo, IReadOnlyList<GrupoDePrueba> Grupos);

    /// <summary>Un docente con su perfil y <paramref name="grupos"/> grupos de 4°, sin alumnos.</summary>
    private async Task<Docente> DocenteAsync(int grupos)
    {
        var (cliente, docenteId, correo) = await _fabrica.RegistrarDocenteVerificadoAsync();
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
        var lista = new List<GrupoDePrueba>();
        foreach (var nombre in NombresDeGrupo.Take(grupos))
        {
            var grupo = await cliente.PostAsJsonAsync("/api/v1/grupos", new { nombre, nivel = "primaria", grado = 4, cicloId });
            grupo.EnsureSuccessStatusCode();
            var id = (await grupo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var periodo = (await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{id}/periodos"))[0].GetProperty("id").GetGuid();
            lista.Add(new GrupoDePrueba(id, periodo));
        }

        return new Docente(cliente, docenteId, correo, lista);
    }

    /// <summary>Un alumno con tutor, un rubro de 100 y su calificación: lo que pide iniciar el cierre (RN-040).</summary>
    private static async Task ListoParaCierreAsync(Docente d, GrupoDePrueba g)
    {
        var cliente = d.Cliente;
        var alta = await cliente.PostAsJsonAsync($"/api/v1/grupos/{g.Id}/alumnos", new { nombres = "Ana", apellidos = "López Ríos" });
        alta.EnsureSuccessStatusCode();
        var alumno = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var telefono = "668" + Random.Shared.Next(1_000_000, 9_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        (await cliente.PostAsJsonAsync($"/api/v1/alumnos/{alumno}/tutores", new { nombre = "Rosa López", telefono, correo = (string?)null, parentesco = "Mamá" }))
            .EnsureSuccessStatusCode();
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
            celdas = new[] { new { actividadId, alumnoId = alumno, valor = (decimal?)9.5m, marca = "ninguna", version = 0 } },
        })).EnsureSuccessStatusCode();
    }

    /// <summary>Cierre, observación aprobada, boleta generada y publicada.</summary>
    private static async Task PublicarTodoAsync(Docente d, GrupoDePrueba g)
    {
        var cliente = d.Cliente;
        (await cliente.PostAsync($"/api/v1/periodos/{g.PeriodoId}/cierre", null)).EnsureSuccessStatusCode();
        (await cliente.PostAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/observaciones/generar", new { })).EnsureSuccessStatusCode();
        var cierre = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/periodos/{g.PeriodoId}/cierre");
        foreach (var alumno in cierre.GetProperty("alumnos").EnumerateArray())
        {
            (await cliente.PostAsJsonAsync($"/api/v1/observaciones/{alumno.GetProperty("observacionId").GetGuid()}/aprobar", new { texto = (string?)null }))
                .EnsureSuccessStatusCode();
        }

        (await cliente.PostAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/boletas/generar", new { })).EnsureSuccessStatusCode();
        (await cliente.PostAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/boletas/publicar", new { })).EnsureSuccessStatusCode();
    }

    private static async Task<JsonElement> SuscripcionAsync(Docente d) =>
        await d.Cliente.GetFromJsonAsync<JsonElement>("/api/v1/docente/suscripcion");

    private Task<HttpResponseMessage> WebhookAsync(byte[] cuerpo, string? firma)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/pagos") { Content = new ByteArrayContent(cuerpo) };
        if (firma is not null)
        {
            solicitud.Headers.Add(FirmaPagos.Encabezado, firma);
        }

        return _fabrica.Cliente().SendAsync(solicitud);
    }

    /// <summary>El trabajo diario de RN-085 y RN-089, como lo correría Hangfire.</summary>
    private Task RevisarAsync() => _fabrica.Services.GetRequiredService<EjecutorTrabajos>().RevisarSuscripcionesAsync(default);

    /// <summary>Lee como el docente sin pasar por HTTP, para no depender del token tras mover el reloj semanas.</summary>
    private async Task<T> ComoDocenteAsync<T>(Guid docenteId, Func<IServiceProvider, Task<T>> lectura)
    {
        await using var alcance = _fabrica.Services.CreateAsyncScope();
        var sp = alcance.ServiceProvider;
        sp.GetRequiredService<IContextoUsuario>().AsumirDocente(docenteId);
        return await sp.GetRequiredService<IUnidadDeTrabajo>().EnTransaccionAsync(() => lectura(sp), default);
    }

    private Task<PlanGrupoDto> PlanAsync(Guid docenteId, Guid grupoId) => ComoDocenteAsync(docenteId, async sp =>
    {
        var grupo = await sp.GetRequiredService<Application.Grupos.IRepositorioGrupos>().GrupoAsync(grupoId, default);
        return await sp.GetRequiredService<AccesoPro>().PlanAsync(grupo!, default);
    });

    private static async Task<string?> CodigoAsync(HttpResponseMessage respuesta) =>
        (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetString();

    private async Task<long> ContarAsync(string sql) => Convert.ToInt64(await EscalarAsync(sql), System.Globalization.CultureInfo.InvariantCulture);

    private async Task<string> TextoAsync(string sql) => (string)(await EscalarAsync(sql))!;

    private async Task<object?> EscalarAsync(string sql)
    {
        await using var conexion = new Npgsql.NpgsqlConnection(baseDeDatos.ConexionAdmin);
        await conexion.OpenAsync();
        await using var comando = new Npgsql.NpgsqlCommand(sql, conexion);
        return await comando.ExecuteScalarAsync();
    }
}
