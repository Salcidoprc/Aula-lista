using System.Text.Json;
using Aulisima.Application.Planes;
using Aulisima.Domain.Planes;
using Aulisima.Infrastructure.Planes;
using Microsoft.Extensions.Options;

namespace Aulisima.Pruebas.Unit.Planes;

/// <summary>RF-095 y ADR-0017: versiones de precio, publicación y sincronización con el proveedor.</summary>
public sealed class CatalogoPruebas
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 14, 18, 0, 0, TimeSpan.Zero);

    private static readonly DatosProducto Datos = new("Pro", "Cierre con IA y boletas.", [FuncionCatalogo.Boletas, FuncionCatalogo.CierreIa], [6, 1, 3, 3], 1);

    [Fact]
    public void RF095_ProductoNuevo_OrdenaFuncionesYMesesYEnBorradorElPrecioSeReemplaza()
    {
        var p = new Producto(Datos, Ahora);
        p.CambiarPrecio(79m, 29m, "MXN", Ahora);
        p.CambiarPrecio(89m, 35m, "MXN", Ahora);

        Assert.Equal(EstadoProducto.Borrador, p.Estado);
        Assert.Equal([FuncionCatalogo.CierreIa, FuncionCatalogo.Boletas], p.Funciones);
        Assert.Equal([1, 3, 6], p.MesesPrepago);
        var precio = Assert.Single(p.Precios);
        Assert.Equal(89m, precio.PrecioPrimera);
        Assert.False(p.SeVende);
        Assert.False(p.PorSincronizar);
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("Pro", 0)]
    [InlineData("Pro", 1000)]
    public void RF095_DatosInvalidos_SeRechazan(string nombre, int orden) =>
        Assert.Equal(ErroresCatalogo.DatosInvalidos, Producto.Validar(Datos with { Nombre = nombre, Orden = orden }));

    [Fact]
    public void RF095_FuncionOMesesDesconocidos_SeRechazan()
    {
        Assert.Equal(ErroresCatalogo.DatosInvalidos, Producto.Validar(Datos with { Funciones = ["teletransportar"] }));
        Assert.Equal(ErroresCatalogo.DatosInvalidos, Producto.Validar(Datos with { MesesPrepago = [13] }));
        Assert.Null(Producto.Validar(Datos with { MesesPrepago = [] }));
    }

    [Theory]
    [InlineData(0.5, 29, false)]
    [InlineData(79, -1, false)]
    [InlineData(79.999, 29, false)]
    [InlineData(79, 0, true)]
    [InlineData(10000, 10000, true)]
    public void RF095_Precio_EsDe1A10000ConDosDecimales(double primera, double adicional, bool valido) =>
        Assert.Equal(valido, Producto.ValidarPrecio((decimal)primera, (decimal)adicional) is null);

    [Fact]
    public async Task ADR0017_Publicar_CreaProductoYPreciosEnElProveedorYLaVersionPasaAVigente()
    {
        var (p, proveedor, sincronizacion) = Publicado();

        Assert.True(await sincronizacion.SincronizarAsync(p, default));

        Assert.True(p.SeVende);
        Assert.False(p.PorSincronizar);
        Assert.Equal("prod_1", p.IdExterno);
        Assert.Equal(("price_1", "price_2"), (p.PrecioVigente!.IdExternoPrimera, p.PrecioVigente.IdExternoAdicional));
        Assert.Equal([$"precio-{p.PrecioVigente.Id}-primera", $"precio-{p.PrecioVigente.Id}-adicional"], proveedor.Claves);
    }

    [Fact]
    public async Task ADR0017_CambiarPrecio_CreaVersionNuevaYArchivaLaAnteriorEnElProveedor()
    {
        var (p, proveedor, sincronizacion) = Publicado();
        await sincronizacion.SincronizarAsync(p, default);
        var v1 = p.PrecioVigente!;

        p.CambiarPrecio(99m, 39m, "MXN", Ahora.AddDays(1));
        Assert.Same(v1, p.PrecioVigente); // hasta que la nueva quede en el proveedor, se sigue vendiendo la anterior
        Assert.True(p.PorSincronizar);
        await sincronizacion.SincronizarAsync(p, default);

        Assert.Equal(2, p.PrecioVigente!.Version);
        Assert.Equal(99m, p.PrecioVigente.Mensual(1));
        Assert.Equal(EstadoPrecio.Archivado, v1.Estado);
        Assert.False(v1.ArchivoPendiente);
        Assert.Equal(["price_1", "price_2"], proveedor.PreciosArchivados);
        Assert.Equal(108m, v1.Mensual(2)); // quien la paga sigue en 79 + 29
    }

    [Fact]
    public async Task ADR0017_SiElProveedorFalla_QuedaElErrorSeSigueVendiendoLoAnteriorYAlReintentarNoDuplica()
    {
        var (p, proveedor, sincronizacion) = Publicado();
        await sincronizacion.SincronizarAsync(p, default);
        p.CambiarPrecio(99m, 39m, "MXN", Ahora);
        proveedor.FallarEnPrecio = 4; // el precio adicional de la versión 2 falla

        Assert.False(await sincronizacion.SincronizarAsync(p, default));
        Assert.Contains("Stripe respondió 500", p.ErrorSincronizacion);
        Assert.Equal(1, p.PrecioVigente!.Version);
        Assert.NotNull(p.PrecioPendiente!.IdExternoPrimera);
        Assert.True(p.PorSincronizar);

        proveedor.FallarEnPrecio = null;
        Assert.True(await sincronizacion.SincronizarAsync(p, default));

        Assert.Null(p.ErrorSincronizacion);
        Assert.Equal(2, p.PrecioVigente!.Version);
        Assert.Equal(4, proveedor.Claves.Count); // la primera del v2 no se volvió a crear
        Assert.Equal(proveedor.Claves.Count, proveedor.Claves.Distinct().Count());
    }

    [Fact]
    public async Task ADR0017_ProductoSembrado_AdoptaLosPreciosDeLaConfiguracionYSuProductoDeStripe()
    {
        var p = new Producto(CatalogoInicial.ProductoProId, Datos, EstadoProducto.Activo, Ahora);
        var proveedor = new ProveedorDePrueba();
        var pagos = Options.Create(new OpcionesPagos { PrecioPrimerGrupoId = "price_viejo_1", PrecioGrupoAdicionalId = "price_viejo_2" });
        var catalogo = new CatalogoPlanes(new RepositorioDePrueba(p, CatalogoInicial.PrecioProV1Id), pagos);
        var sincronizacion = new SincronizacionCatalogo(proveedor, catalogo, TimeProvider.System);
        var v1 = (await catalogo.ProductosAsync(default))[0].Precios[0];

        Assert.Equal(("price_viejo_1", "price_viejo_2"), catalogo.IdsExternos(v1));
        Assert.NotNull(await catalogo.ALaVentaAsync(default));

        p.CambiarPrecio(99m, 39m, "MXN", Ahora);
        await sincronizacion.SincronizarAsync(p, default);

        Assert.Equal("prod_de_price_viejo_1", p.IdExterno);
        Assert.Equal(["price_viejo_1", "price_viejo_2"], proveedor.PreciosArchivados);
        Assert.Equal(2, p.PrecioVigente!.Version);
    }

    [Fact]
    public void ADR0017_EventoDeSuscripcionDeStripe_TraeLaVersionDePrecioDeSusMetadatos()
    {
        var precio = Guid.NewGuid();
        var json = "{\"id\":\"evt_1\",\"type\":\"customer.subscription.created\",\"data\":{\"object\":{\"id\":\"sub_1\","
            + "\"customer\":\"cus_1\",\"status\":\"active\",\"current_period_end\":1790000000,\"metadata\":{\"docente_id\":\""
            + Guid.NewGuid() + "\",\"precio_id\":\"" + precio + "\"}}}}";
        var evento = JsonDocument.Parse(json).RootElement;

        Assert.Equal(precio, PasarelaStripe.Traducir(evento)!.PrecioId);
    }

    [Fact]
    public void ADR0017_SuscripcionConVersion_NoLaPierdeConUnEventoSinVersion()
    {
        var s = new Suscripcion(Guid.NewGuid(), "simulado", Ahora);
        var v1 = Guid.NewGuid();
        s.AplicarSuscripcion("cus_1", "sub_1", [Guid.NewGuid()], Ahora.AddMonths(1), v1, Ahora);
        s.AplicarSuscripcion("cus_1", "sub_1", [Guid.NewGuid(), Guid.NewGuid()], Ahora.AddMonths(1), null, Ahora);

        Assert.Equal(v1, s.PrecioId);
    }

    private static (Producto Producto, ProveedorDePrueba Proveedor, SincronizacionCatalogo Sincronizacion) Publicado()
    {
        var p = new Producto(Datos, Ahora);
        p.CambiarPrecio(79m, 29m, "MXN", Ahora);
        Assert.True(p.Publicar(Ahora).EsExito);
        var proveedor = new ProveedorDePrueba();
        var catalogo = new CatalogoPlanes(new RepositorioDePrueba(p, null), Options.Create(new OpcionesPagos()));
        return (p, proveedor, new SincronizacionCatalogo(proveedor, catalogo, TimeProvider.System));
    }

    private sealed class ProveedorDePrueba : ICatalogoPagos
    {
        private readonly Dictionary<string, string> _porClave = [];

        public List<string> Claves { get; } = [];

        public List<string> PreciosArchivados { get; } = [];

        /// <summary>El número de llamada a crear precio que responde con error.</summary>
        public int? FallarEnPrecio { get; set; }

        private int _llamadas;

        public Task<string> CrearProductoAsync(Guid productoId, string nombre, string descripcion, CancellationToken cancellationToken) =>
            Task.FromResult("prod_1");

        public Task ActualizarProductoAsync(string idExterno, string nombre, string descripcion, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ArchivarProductoAsync(string idExterno, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<string> CrearPrecioAsync(
            string producto, decimal monto, string moneda, string apodo, string claveIdempotencia, CancellationToken cancellationToken)
        {
            if (++_llamadas == FallarEnPrecio)
            {
                throw new HttpRequestException("Stripe respondió 500 (api_error/).");
            }

            if (!_porClave.TryGetValue(claveIdempotencia, out var id))
            {
                Claves.Add(claveIdempotencia);
                id = $"price_{Claves.Count}";
                _porClave[claveIdempotencia] = id;
            }

            return Task.FromResult(id);
        }

        public Task ArchivarPrecioAsync(string idExterno, CancellationToken cancellationToken)
        {
            PreciosArchivados.Add(idExterno);
            return Task.CompletedTask;
        }

        public Task<string> ProductoDePrecioAsync(string precio, CancellationToken cancellationToken) => Task.FromResult($"prod_de_{precio}");
    }

    /// <summary>Un catálogo de un producto; con <paramref name="v1"/>, siembra su versión 1 vigente con ese id, como la migración.</summary>
    private sealed class RepositorioDePrueba : IRepositorioCatalogo
    {
        private readonly Producto _producto;

        public RepositorioDePrueba(Producto producto, Guid? v1)
        {
            _producto = producto;
            if (v1 is { } id)
            {
                var precio = new PrecioProducto(id, producto.Id, 1, 79m, 29m, "MXN", EstadoPrecio.Vigente, Ahora);
                var lista = (List<PrecioProducto>)typeof(Producto).GetField("_precios", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .GetValue(producto)!;
                lista.Add(precio);
            }
        }

        public Task<IReadOnlyList<Producto>> ProductosAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Producto>>([_producto]);

        public Task<Producto?> ProductoAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Producto?>(_producto);

        public void Agregar(Producto producto)
        {
        }

        public void Agregar(PrecioProducto precio)
        {
        }

        public Task<IReadOnlyList<FilaPrecioSuscripcion>> PreciosSuscripcionAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FilaPrecioSuscripcion>>([]);
    }
}
