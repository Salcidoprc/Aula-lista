using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aulisima.Application.Operacion;
using Microsoft.Extensions.DependencyInjection;

namespace Aulisima.Pruebas.Integracion;

/// <summary>
/// RF-095 y ADR-0017: el catálogo de productos y precios que edita el administrador, con la pasarela simulada. El catálogo
/// es global y la base se comparte: cada prueba que cambia el producto a la venta lo deja como estaba.
/// </summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class Spec08CatalogoPruebas(BaseDeDatosFixture baseDeDatos) : IDisposable
{
    private static readonly string[] NombresDeGrupo = ["A", "B", "C"];
    private static readonly string[] TodasLasFunciones =
        ["promedios", "exportar_excel", "avisos", "cierre_ia", "boletas", "whatsapp_boletas", "avisos_urgentes"];

    private readonly FabricaApi _fabrica = new(baseDeDatos);

    public void Dispose() => _fabrica.Dispose();

    [Fact]
    public async Task RF095_RN097_MigracionSiembraElProductoPro_Con79Mas29YMeses1_3_6_YLasFuncionesQuePasanAPro()
    {
        var admin = await AdminAsync();

        var catalogo = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/productos");
        var pro = Activo(catalogo);
        var planes = await _fabrica.Cliente().GetFromJsonAsync<JsonElement>("/api/v1/planes");

        Assert.Equal("simulado", catalogo.GetProperty("proveedor").GetString());
        Assert.Equal(TodasLasFunciones, catalogo.GetProperty("funciones").EnumerateArray().Select(f => f.GetString()));
        Assert.True(pro.GetProperty("seVende").GetBoolean());
        Assert.Equal(TodasLasFunciones, pro.GetProperty("funciones").EnumerateArray().Select(f => f.GetString()));
        Assert.True(planes.GetProperty("aLaVenta").GetBoolean());
        Assert.Equal(pro.GetProperty("nombre").GetString(), planes.GetProperty("producto").GetString());
        Assert.Equal(79m, planes.GetProperty("precioPrimerGrupo").GetDecimal());
        Assert.Equal(29m, planes.GetProperty("precioGrupoAdicional").GetDecimal());
        Assert.Equal([1, 3, 6], planes.GetProperty("mesesPrepago").EnumerateArray().Select(m => m.GetInt32()));
    }

    [Fact]
    public async Task RF095_AdminCambiaElPrecio_QuienYaPagabaConservaSuPrecioYLosNuevosPaganElNuevo()
    {
        var admin = await AdminAsync();
        var pro = Activo(await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/productos"));
        var id = pro.GetProperty("id").GetGuid();
        var antes = await DocenteAsync(3);
        await antes.Cliente.ActivarProAsync(antes.Grupos[0], antes.Grupos[1]);

        try
        {
            var cambio = await admin.PostAsJsonAsync($"/api/v1/admin/productos/{id}/precios", new { precioPrimera = 99m, precioAdicional = 39m });
            Assert.True(cambio.StatusCode == HttpStatusCode.OK, await cambio.Content.ReadAsStringAsync());
            var producto = await cambio.Content.ReadFromJsonAsync<JsonElement>();
            var precios = producto.GetProperty("precios").EnumerateArray().ToList();
            Assert.False(producto.GetProperty("porSincronizar").GetBoolean());
            Assert.StartsWith("prod_sim_", producto.GetProperty("idExterno").GetString());
            Assert.Equal("vigente", precios[0].GetProperty("estado").GetString());
            Assert.Equal(99m, precios[0].GetProperty("precioPrimera").GetDecimal());
            Assert.True(precios[0].GetProperty("sincronizado").GetBoolean());
            Assert.Equal("archivado", precios[1].GetProperty("estado").GetString());

            // Quien ya pagaba sigue en 79 + 29, también al agregar un grupo.
            var suya = await antes.Cliente.GetFromJsonAsync<JsonElement>("/api/v1/docente/suscripcion");
            Assert.Equal(108m, suya.GetProperty("totalMensual").GetDecimal());
            Assert.True(suya.GetProperty("conservaPrecio").GetBoolean());
            Assert.Equal(79m, suya.GetProperty("precioPrimerGrupo").GetDecimal());
            await antes.Cliente.ActivarProAsync(antes.Grupos[2]);
            Assert.Equal(137m, (await antes.Cliente.GetFromJsonAsync<JsonElement>("/api/v1/docente/suscripcion")).GetProperty("totalMensual").GetDecimal());

            // Un docente nuevo ve y paga el precio nuevo.
            var planes = await _fabrica.Cliente().GetFromJsonAsync<JsonElement>("/api/v1/planes");
            Assert.Equal(99m, planes.GetProperty("precioPrimerGrupo").GetDecimal());
            var nuevo = await DocenteAsync(2);
            await nuevo.Cliente.ActivarProAsync(nuevo.Grupos[0], nuevo.Grupos[1]);
            var suyaNueva = await nuevo.Cliente.GetFromJsonAsync<JsonElement>("/api/v1/docente/suscripcion");
            Assert.Equal(138m, suyaNueva.GetProperty("totalMensual").GetDecimal());
            Assert.False(suyaNueva.GetProperty("conservaPrecio").GetBoolean());

            // El panel cuenta cuántos docentes paga cada versión.
            var panel = (await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/productos"))
                .GetProperty("productos").EnumerateArray().Single(p => p.GetProperty("id").GetGuid() == id)
                .GetProperty("precios").EnumerateArray().ToList();
            Assert.True(panel[0].GetProperty("suscriptores").GetInt32() >= 1);
            Assert.True(panel[1].GetProperty("suscriptores").GetInt32() >= 1);
            Assert.True(await ContarAsync($"SELECT count(*) FROM auditoria_admin WHERE accion = 'precio_cambiado' AND entidad_id = '{id}'") >= 1);
        }
        finally
        {
            (await admin.PostAsJsonAsync($"/api/v1/admin/productos/{id}/precios", new { precioPrimera = 79m, precioAdicional = 29m }))
                .EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task RF095_ProductoNuevo_QuedaEnBorradorSinStripeYNoSePublicaMientrasSeVendeOtro()
    {
        var admin = await AdminAsync();
        var nombre = $"Escuela {Guid.NewGuid():N}"[..20];

        var alta = await admin.PostAsJsonAsync("/api/v1/admin/productos", Entrada(nombre, 149m, 49m));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var producto = await alta.Content.ReadFromJsonAsync<JsonElement>();
        var id = producto.GetProperty("id").GetGuid();
        Assert.Equal("borrador", producto.GetProperty("estado").GetString());
        Assert.Equal(JsonValueKind.Null, producto.GetProperty("idExterno").ValueKind);
        Assert.False(producto.GetProperty("seVende").GetBoolean());

        var repetido = await admin.PostAsJsonAsync("/api/v1/admin/productos", Entrada(nombre.ToUpperInvariant(), 10m, 5m));
        var malPrecio = await admin.PostAsJsonAsync("/api/v1/admin/productos", Entrada("Otro", 0m, 5m));
        var sinFunciones = await admin.PostAsJsonAsync("/api/v1/admin/productos", Entrada("Otro", 10m, 5m) with { Funciones = [] });
        var publicar = await admin.PostAsync($"/api/v1/admin/productos/{id}/publicar", null);
        var edicion = await admin.PutAsJsonAsync($"/api/v1/admin/productos/{id}", Entrada(nombre, 159m, 49m) with { MesesPrepago = [1] });
        var archivar = await admin.PostAsync($"/api/v1/admin/productos/{id}/archivar", null);
        var planes = await _fabrica.Cliente().GetFromJsonAsync<JsonElement>("/api/v1/planes");

        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
        Assert.Equal("producto_repetido", await CodigoAsync(repetido));
        Assert.Equal("precio_invalido", await CodigoAsync(malPrecio));
        Assert.Equal("producto_datos_invalidos", await CodigoAsync(sinFunciones));
        Assert.Equal(HttpStatusCode.Conflict, publicar.StatusCode);
        Assert.Equal("otro_producto_activo", await CodigoAsync(publicar));
        Assert.Equal(HttpStatusCode.OK, edicion.StatusCode);
        var editado = await edicion.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal([1], editado.GetProperty("mesesPrepago").EnumerateArray().Select(m => m.GetInt32()));
        var versiones = editado.GetProperty("precios").EnumerateArray().ToList();
        Assert.Single(versiones); // en borrador el precio se reemplaza, no se versiona
        Assert.Equal(159m, versiones[0].GetProperty("precioPrimera").GetDecimal());
        Assert.Equal("archivado", (await archivar.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("estado").GetString());
        Assert.Equal(79m, planes.GetProperty("precioPrimerGrupo").GetDecimal());
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM auditoria_admin WHERE accion = 'producto_creado' AND entidad_id = '{id}'"));
    }

    [Fact]
    public async Task RF095_QuitarUnaFuncionDelProducto_LaBloqueaEnLosGruposQueLoPagan()
    {
        var admin = await AdminAsync();
        var pro = Activo(await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/productos"));
        var id = pro.GetProperty("id").GetGuid();
        var d = await DocenteAsync(1);
        await d.Cliente.ActivarProAsync(d.Grupos[0]);
        var aviso = new { titulo = "Suspensión", cuerpo = "Mañana no hay clases.", urgente = true };
        var conFuncion = await d.Cliente.PostAsJsonAsync($"/api/v1/grupos/{d.Grupos[0]}/avisos", aviso);

        try
        {
            (await admin.PutAsJsonAsync($"/api/v1/admin/productos/{id}", Editar(pro, ["promedios", "exportar_excel", "avisos", "cierre_ia", "boletas", "whatsapp_boletas"])))
                .EnsureSuccessStatusCode();
            var sinFuncion = await d.Cliente.PostAsJsonAsync($"/api/v1/grupos/{d.Grupos[0]}/avisos", aviso);

            // Con la función pasa el candado y solo falta elegir tutores; sin ella, el candado lo detiene.
            Assert.Equal("aviso_sin_destinatarios", await CodigoAsync(conFuncion));
            Assert.Equal(HttpStatusCode.Forbidden, sinFuncion.StatusCode);
            Assert.Equal("whatsapp_requiere_pro", await CodigoAsync(sinFuncion));
            Assert.Equal("pro", (await d.Cliente.GetFromJsonAsync<JsonElement>("/api/v1/grupos")).EnumerateArray().Single().GetProperty("planVigente").GetString());
        }
        finally
        {
            (await admin.PutAsJsonAsync($"/api/v1/admin/productos/{id}", Editar(pro, TodasLasFunciones))).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task RF095_DocenteSinRolAdmin_NoVeNiCambiaElCatalogo()
    {
        var (docente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();

        var lista = await docente.GetAsync("/api/v1/admin/productos");
        var alta = await docente.PostAsJsonAsync("/api/v1/admin/productos", Entrada("Pirata", 1m, 1m));

        Assert.Equal(HttpStatusCode.NotFound, lista.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, alta.StatusCode);
        Assert.Equal(0, await ContarAsync("SELECT count(*) FROM producto WHERE nombre = 'Pirata'"));
    }

    private sealed record EntradaProducto(
        string Nombre, string Descripcion, string[] Funciones, int[] MesesPrepago, int Orden, decimal? PrecioPrimera, decimal? PrecioAdicional);

    private static EntradaProducto Entrada(string nombre, decimal primera, decimal adicional) =>
        new(nombre, "Para pruebas.", ["boletas"], [3], 50, primera, adicional);

    private static EntradaProducto Editar(JsonElement p, string[] funciones) => new(
        p.GetProperty("nombre").GetString()!, p.GetProperty("descripcion").GetString()!, funciones,
        [.. p.GetProperty("mesesPrepago").EnumerateArray().Select(m => m.GetInt32())], p.GetProperty("orden").GetInt32(), null, null);

    private static JsonElement Activo(JsonElement catalogo) =>
        catalogo.GetProperty("productos").EnumerateArray().Single(p => p.GetProperty("seVende").GetBoolean());

    private sealed record Docente(HttpClient Cliente, IReadOnlyList<Guid> Grupos);

    private async Task<Docente> DocenteAsync(int grupos)
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
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
        var lista = new List<Guid>();
        foreach (var nombre in NombresDeGrupo.Take(grupos))
        {
            var grupo = await cliente.PostAsJsonAsync("/api/v1/grupos", new { nombre, nivel = "primaria", grado = 4, cicloId });
            grupo.EnsureSuccessStatusCode();
            lista.Add((await grupo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        }

        return new Docente(cliente, lista);
    }

    private async Task<HttpClient> AdminAsync()
    {
        var (cliente, _, correo) = await _fabrica.RegistrarDocenteVerificadoAsync();
        await using var alcance = _fabrica.Services.CreateAsyncScope();
        Assert.True(await alcance.ServiceProvider.GetRequiredService<HacerAdminCasoUso>().EjecutarAsync(correo, CancellationToken.None));
        return cliente;
    }

    private static async Task<string?> CodigoAsync(HttpResponseMessage respuesta) =>
        (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("codigo", out var c) ? c.GetString() : null;

    private async Task<long> ContarAsync(string sql)
    {
        await using var conexion = new Npgsql.NpgsqlConnection(baseDeDatos.ConexionAdmin);
        await conexion.OpenAsync();
        await using var comando = new Npgsql.NpgsqlCommand(sql, conexion);
        return (long)(await comando.ExecuteScalarAsync())!;
    }
}
