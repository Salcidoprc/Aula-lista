using System.Text.Json;
using Aulisima.Application.Comun;
using Aulisima.Domain.Comun;
using Aulisima.Domain.Operacion;
using Aulisima.Domain.Planes;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Aulisima.Application.Planes;

/// <summary>Una suscripción del panel: qué versión de precio paga cada docente (solo ids, RN-090).</summary>
public sealed record FilaPrecioSuscripcion(Guid DocenteId, Guid? PrecioId, string Estado);

/// <summary>El catálogo de productos y precios (ADR-0017). Se lee sin docente; solo el administrador lo escribe (RLS).</summary>
public interface IRepositorioCatalogo
{
    Task<IReadOnlyList<Producto>> ProductosAsync(CancellationToken cancellationToken);

    Task<Producto?> ProductoAsync(Guid id, CancellationToken cancellationToken);

    void Agregar(Producto producto);

    void Agregar(PrecioProducto precio);

    /// <summary>Para el panel, con la función <c>admin_precios_suscripcion</c> (exige administrador).</summary>
    Task<IReadOnlyList<FilaPrecioSuscripcion>> PreciosSuscripcionAsync(CancellationToken cancellationToken);
}

/// <summary>
/// El catálogo en el proveedor de pagos (Stripe o el simulado). Cada alta lleva una clave de idempotencia, así un
/// reintento nunca duplica un producto ni un precio. Los errores salen como <see cref="HttpRequestException"/>.
/// </summary>
public interface ICatalogoPagos
{
    Task<string> CrearProductoAsync(Guid productoId, string nombre, string descripcion, CancellationToken cancellationToken);

    Task ActualizarProductoAsync(string idExterno, string nombre, string descripcion, CancellationToken cancellationToken);

    Task ArchivarProductoAsync(string idExterno, CancellationToken cancellationToken);

    /// <summary>Un precio mensual recurrente, en centavos exactos.</summary>
    Task<string> CrearPrecioAsync(
        string producto, decimal monto, string moneda, string apodo, string claveIdempotencia, CancellationToken cancellationToken);

    Task ArchivarPrecioAsync(string idExterno, CancellationToken cancellationToken);

    /// <summary>El producto al que pertenece un precio, para adoptar los precios que ya estaban en Stripe.</summary>
    Task<string> ProductoDePrecioAsync(string precio, CancellationToken cancellationToken);
}

/// <summary>
/// ADR-0017: cuánto paga al mes cada docente por sus grupos, con la versión de precio de su suscripción (o la que se
/// vende hoy si no tiene). Para el ingreso del panel (RF-091, RF-094).
/// </summary>
public sealed class TarifasPanel(IRepositorioCatalogo repositorio, CatalogoPlanes catalogo)
{
    public async Task<Func<Guid, int, decimal>> MensualAsync(CancellationToken ct)
    {
        var porDocente = (await repositorio.PreciosSuscripcionAsync(ct)).ToDictionary(f => f.DocenteId, f => f.PrecioId);
        var precios = (await catalogo.ProductosAsync(ct)).SelectMany(p => p.Precios).ToDictionary(p => p.Id);
        var vigente = (await catalogo.ALaVentaAsync(ct))?.Precio;
        return (docente, grupos) =>
        {
            var precio = porDocente.GetValueOrDefault(docente) is { } id && precios.TryGetValue(id, out var propio) ? propio : vigente;
            return precio?.Mensual(grupos) ?? 0m;
        };
    }
}

/// <summary>Lo que se vende hoy, con los ids que usa el proveedor.</summary>
public sealed record OfertaPlan(Producto Producto, PrecioProducto Precio, string? IdPrimera, string? IdAdicional);

/// <summary>
/// ADR-0017: el producto por grupo a la venta y la versión de precio de cada suscripción. Lee el catálogo una vez por
/// solicitud. La versión sembrada (79 + 29) toma sus ids de Stripe de <c>Pagos__PrecioPrimerGrupoId</c> y
/// <c>Pagos__PrecioGrupoAdicionalId</c> mientras no se sincronice, para no romper una instalación que ya los usa.
/// </summary>
public sealed class CatalogoPlanes(IRepositorioCatalogo repositorio, IOptions<OpcionesPagos> pagos)
{
    private IReadOnlyList<Producto>? _productos;

    public async Task<IReadOnlyList<Producto>> ProductosAsync(CancellationToken ct) => _productos ??= await repositorio.ProductosAsync(ct);

    /// <summary>El producto por grupo que se ofrece en Plan, si hay uno listo para cobrarse.</summary>
    public async Task<OfertaPlan?> ALaVentaAsync(CancellationToken ct)
    {
        foreach (var p in (await ProductosAsync(ct)).Where(p => p.SeVende && p.Alcance == AlcanceProducto.Grupo).OrderBy(p => p.Orden))
        {
            var precio = p.PrecioVigente!;
            var (primera, adicional) = IdsExternos(precio);
            if (pagos.Value.Proveedor == OpcionesPagos.ProveedorSimulado || (primera is not null && adicional is not null))
            {
                return new OfertaPlan(p, precio, primera, adicional);
            }
        }

        return null;
    }

    /// <summary>La versión que paga la suscripción; sin versión, la que se vende hoy.</summary>
    public async Task<PrecioProducto?> PrecioAsync(Guid? precioId, CancellationToken ct)
    {
        var productos = await ProductosAsync(ct);
        if (precioId is { } id && productos.SelectMany(p => p.Precios).FirstOrDefault(p => p.Id == id) is { } propio)
        {
            return propio;
        }

        return (await ALaVentaAsync(ct))?.Precio;
    }

    public async Task<Producto?> ProductoDeAsync(Guid? precioId, CancellationToken ct)
    {
        var productos = await ProductosAsync(ct);
        return precioId is { } id && productos.FirstOrDefault(p => p.Precios.Any(v => v.Id == id)) is { } propio
            ? propio
            : (await ALaVentaAsync(ct))?.Producto;
    }

    public (string? Primera, string? Adicional) IdsExternos(PrecioProducto precio)
    {
        if (precio.Sincronizado || precio.Id != CatalogoInicial.PrecioProV1Id)
        {
            return (precio.IdExternoPrimera, precio.IdExternoAdicional);
        }

        var o = pagos.Value;
        return (precio.IdExternoPrimera ?? Vacio(o.PrecioPrimerGrupoId), precio.IdExternoAdicional ?? Vacio(o.PrecioGrupoAdicionalId));
    }

    private static string? Vacio(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor;
}

// ───────────── Panel de administración ─────────────

public sealed record PrecioAdminDto(
    Guid Id, int Version, decimal PrecioPrimera, decimal PrecioAdicional, string Moneda, string Estado, DateTimeOffset CreadoEn,
    DateTimeOffset? VigenteDesde, DateTimeOffset? ArchivadoEn, bool Sincronizado, int Suscriptores);

/// <summary>
/// Un producto del catálogo. <see cref="SeVende"/>: es el que ven los docentes en Plan. <see cref="PorSincronizar"/>:
/// algo no llegó a Stripe todavía; <see cref="ErrorSincronizacion"/> dice por qué.
/// </summary>
public sealed record ProductoAdminDto(
    Guid Id, string Nombre, string Descripcion, string Alcance, IReadOnlyList<string> Funciones, IReadOnlyList<int> MesesPrepago,
    int Orden, string Estado, bool SeVende, string? IdExterno, bool PorSincronizar, string? ErrorSincronizacion,
    DateTimeOffset? SincronizadoEn, int Suscriptores, IReadOnlyList<PrecioAdminDto> Precios);

/// <summary><see cref="Funciones"/>: las que el código sabe bloquear, para elegirlas en el formulario.</summary>
public sealed record CatalogoAdminDto(string Proveedor, string Moneda, IReadOnlyList<string> Funciones, IReadOnlyList<ProductoAdminDto> Productos);

public sealed record ProductoEntrada(
    string Nombre, string? Descripcion, IReadOnlyList<string> Funciones, IReadOnlyList<int>? MesesPrepago, int Orden,
    decimal? PrecioPrimera, decimal? PrecioAdicional);

public sealed record PrecioEntrada(decimal PrecioPrimera, decimal PrecioAdicional);

internal sealed class ProductoEntradaValidador : AbstractValidator<ProductoEntrada>
{
    public ProductoEntradaValidador()
    {
        RuleFor(p => p.Nombre).NotNull().MaximumLength(Producto.LongitudNombre * 2);
        RuleFor(p => p.Descripcion).MaximumLength(Producto.LongitudDescripcion * 2);
        RuleFor(p => p.Funciones).NotNull().Must(f => f is null || f.Count <= FuncionCatalogo.Todas.Count * 2);
        RuleFor(p => p.MesesPrepago).Must(m => m is null || m.Count <= Producto.MaximoMesesPrepago);
    }
}

internal static class AdminCatalogo
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static DatosProducto Datos(ProductoEntrada e) =>
        new(e.Nombre ?? string.Empty, e.Descripcion ?? string.Empty, e.Funciones ?? [], e.MesesPrepago ?? [], e.Orden);

    public static ProductoAdminDto Dto(Producto p, IReadOnlyList<FilaPrecioSuscripcion> suscripciones)
    {
        var vigentes = suscripciones.Where(s => s.Estado is EstadoSuscripcion.Activa or EstadoSuscripcion.EnGracia).ToList();
        var precios = p.Precios.OrderByDescending(v => v.Version).Select(v => new PrecioAdminDto(
            v.Id, v.Version, v.PrecioPrimera, v.PrecioAdicional, v.Moneda, v.Estado, v.CreatedAt, v.VigenteDesde, v.ArchivadoEn, v.Sincronizado,
            vigentes.Count(s => s.PrecioId == v.Id))).ToList();
        return new ProductoAdminDto(
            p.Id, p.Nombre, p.Descripcion, p.Alcance, p.Funciones, p.MesesPrepago, p.Orden, p.Estado, p.SeVende, p.IdExterno,
            p.PorSincronizar, p.ErrorSincronizacion, p.SincronizadoEn, precios.Sum(v => v.Suscriptores), precios);
    }

    /// <summary>La foto del producto para la auditoría (RN-095): solo datos del catálogo.</summary>
    public static string Foto(Producto p) => JsonSerializer.Serialize(
        new
        {
            p.Nombre,
            p.Descripcion,
            p.Funciones,
            p.MesesPrepago,
            p.Orden,
            p.Estado,
            // Planos, para que la auditoría muestre cada cifra como un campo que cambió.
            VersionPrecio = (p.PrecioPendiente ?? p.PrecioVigente)?.Version,
            (p.PrecioPendiente ?? p.PrecioVigente)?.PrecioPrimera,
            (p.PrecioPendiente ?? p.PrecioVigente)?.PrecioAdicional,
        },
        Json);

    public static bool NombreRepetido(IEnumerable<Producto> productos, string nombre, Guid? excepto) =>
        productos.Any(p => p.Id != excepto && string.Equals(p.Nombre.Trim(), nombre.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// ADR-0017: lleva al proveedor lo que falte del producto: crearlo (o adoptar el de los precios de la configuración),
/// su nombre y descripción, los precios de la versión pendiente (que entonces pasa a vigente y archiva la anterior) y lo
/// archivado. Cada paso que sale bien queda guardado; si uno falla, el error queda en el producto para reintentar.
/// </summary>
public sealed class SincronizacionCatalogo(ICatalogoPagos proveedor, CatalogoPlanes catalogo, TimeProvider reloj)
{
    public async Task<bool> SincronizarAsync(Producto producto, CancellationToken ct)
    {
        if (producto.Estado == EstadoProducto.Borrador)
        {
            return true;
        }

        var ahora = reloj.GetUtcNow();
        try
        {
            // La versión sembrada adopta los precios que ya existían en Stripe por configuración.
            if (producto.Precios.FirstOrDefault(p => p.Id == CatalogoInicial.PrecioProV1Id) is { Sincronizado: false } inicial
                && catalogo.IdsExternos(inicial) is { Primera: { } primera, Adicional: { } adicional })
            {
                inicial.RegistrarIdsExternos(primera, adicional, ahora);
                if (producto.IdExterno is null)
                {
                    var adoptado = await proveedor.ProductoDePrecioAsync(primera, ct);
                    await proveedor.ActualizarProductoAsync(adoptado, producto.Nombre, producto.Descripcion, ct);
                    producto.RegistrarIdExterno(adoptado, ahora);
                }
            }

            var activo = producto.Estado == EstadoProducto.Activo;
            if (activo && producto.IdExterno is null)
            {
                producto.RegistrarIdExterno(await proveedor.CrearProductoAsync(producto.Id, producto.Nombre, producto.Descripcion, ct), ahora);
            }
            else if (producto.CambiosPendientes && producto.IdExterno is not null)
            {
                await proveedor.ActualizarProductoAsync(producto.IdExterno, producto.Nombre, producto.Descripcion, ct);
                producto.RegistrarCambiosSincronizados(ahora);
            }

            if (activo && producto.PrecioPendiente is { } pendiente)
            {
                var apodo = $"{producto.Nombre} v{pendiente.Version}";
                var idPrimera = pendiente.IdExternoPrimera ?? await proveedor.CrearPrecioAsync(
                    producto.IdExterno!, pendiente.PrecioPrimera, pendiente.Moneda, $"{apodo} · primero", $"precio-{pendiente.Id}-primera", ct);
                pendiente.RegistrarIdsExternos(idPrimera, null, ahora);
                var idAdicional = pendiente.IdExternoAdicional ?? await proveedor.CrearPrecioAsync(
                    producto.IdExterno!, pendiente.PrecioAdicional, pendiente.Moneda, $"{apodo} · adicional", $"precio-{pendiente.Id}-adicional", ct);
                pendiente.RegistrarIdsExternos(null, idAdicional, ahora);
                producto.ActivarPrecio(pendiente, ahora);
            }

            foreach (var viejo in producto.Precios.Where(p => p.ArchivoPendiente))
            {
                var (viejaPrimera, viejaAdicional) = catalogo.IdsExternos(viejo);
                foreach (var id in new[] { viejaPrimera, viejaAdicional }.OfType<string>())
                {
                    await proveedor.ArchivarPrecioAsync(id, ct);
                }

                viejo.RegistrarArchivoSincronizado(ahora);
            }

            if (producto.ArchivoPendiente && producto.IdExterno is not null)
            {
                await proveedor.ArchivarProductoAsync(producto.IdExterno!, ct);
                producto.RegistrarArchivoSincronizado(ahora);
            }

            producto.RegistrarSincronizacion(null, ahora);
            return true;
        }
        catch (HttpRequestException ex)
        {
            producto.RegistrarSincronizacion(ex.Message.Length > 300 ? ex.Message[..300] : ex.Message, ahora);
            return false;
        }
    }
}

/// <summary>RF-095: el catálogo completo con sus versiones de precio y cuántos docentes paga cada una.</summary>
public sealed class ListarCatalogoAdminCasoUso(
    IRepositorioCatalogo repositorio, IOptions<OpcionesPagos> pagos, IOptions<OpcionesPlanes> planes)
{
    public async Task<CatalogoAdminDto> EjecutarAsync(CancellationToken ct)
    {
        var suscripciones = await repositorio.PreciosSuscripcionAsync(ct);
        var productos = (await repositorio.ProductosAsync(ct))
            .OrderBy(p => p.Estado == EstadoProducto.Archivado).ThenBy(p => p.Orden).ThenBy(p => p.Nombre, StringComparer.CurrentCulture)
            .Select(p => AdminCatalogo.Dto(p, suscripciones)).ToList();
        return new CatalogoAdminDto(pagos.Value.Proveedor, planes.Value.Moneda, FuncionCatalogo.Todas, productos);
    }
}

/// <summary>
/// Base de los cambios del panel al catálogo: aplica el cambio, lo guarda con su auditoría (RN-095) y, si el producto
/// ya está publicado, lo lleva a Stripe. Un fallo del proveedor no deshace el cambio: queda por sincronizar.
/// </summary>
public abstract class CambioCatalogo(
    IRepositorioCatalogo repositorio, SincronizacionCatalogo sincronizacion, IContextoUsuario usuario, Operacion.IRepositorioAdmin admin,
    IUnidadDeTrabajo unidad)
{
    protected IRepositorioCatalogo Repositorio => repositorio;

    protected async Task<Resultado<ProductoAdminDto>> GuardarYSincronizarAsync(
        Producto producto, string accion, string? antes, IReadOnlyList<PrecioProducto> preciosAntes, DateTimeOffset ahora, CancellationToken ct)
    {
        var despues = AdminCatalogo.Foto(producto);
        await unidad.EnTransaccionAsync(
            () =>
            {
                foreach (var nuevo in producto.Precios.Except(preciosAntes))
                {
                    repositorio.Agregar(nuevo);
                }

                if (antes != despues)
                {
                    admin.Agregar(new RegistroAuditoriaAdmin(usuario.AdminId!.Value, accion, "producto", producto.Id, antes, despues, ahora));
                }

                return Task.FromResult(true);
            },
            ct);

        if (producto.PorSincronizar)
        {
            await sincronizacion.SincronizarAsync(producto, ct);
            await unidad.EnTransaccionAsync(() => Task.FromResult(true), ct);
        }

        return Resultado.Exito(AdminCatalogo.Dto(producto, await repositorio.PreciosSuscripcionAsync(ct)));
    }
}

/// <summary>RF-095: un producto nuevo queda en borrador; no existe en Stripe ni se vende hasta publicarlo.</summary>
public sealed class CrearProductoCasoUso(
    IRepositorioCatalogo repositorio, SincronizacionCatalogo sincronizacion, IContextoUsuario usuario, Operacion.IRepositorioAdmin admin,
    IUnidadDeTrabajo unidad, IOptions<OpcionesPlanes> planes, TimeProvider reloj)
    : CambioCatalogo(repositorio, sincronizacion, usuario, admin, unidad)
{
    public async Task<Resultado<ProductoAdminDto>> EjecutarAsync(ProductoEntrada entrada, CancellationToken ct)
    {
        var datos = AdminCatalogo.Datos(entrada);
        if (Producto.Validar(datos) is { } invalido)
        {
            return Resultado.Fallo<ProductoAdminDto>(invalido);
        }

        if (entrada is { PrecioPrimera: { } p1, PrecioAdicional: { } p2 } && Producto.ValidarPrecio(p1, p2) is { } malPrecio)
        {
            return Resultado.Fallo<ProductoAdminDto>(malPrecio);
        }

        if (AdminCatalogo.NombreRepetido(await Repositorio.ProductosAsync(ct), datos.Nombre, null))
        {
            return Resultado.Fallo<ProductoAdminDto>(ErroresCatalogo.NombreRepetido);
        }

        var ahora = reloj.GetUtcNow();
        var producto = new Producto(datos, ahora);
        if (entrada is { PrecioPrimera: { } primera, PrecioAdicional: { } adicional })
        {
            producto.CambiarPrecio(primera, adicional, planes.Value.Moneda, ahora);
        }

        Repositorio.Agregar(producto);
        return await GuardarYSincronizarAsync(producto, AccionAuditoriaAdmin.ProductoCreado, null, [], ahora, ct);
    }
}

/// <summary>RF-095: nombre, descripción, funciones, meses de OXXO y orden. Las funciones aplican al instante a quien lo paga.</summary>
public sealed class EditarProductoCasoUso(
    IRepositorioCatalogo repositorio, SincronizacionCatalogo sincronizacion, IContextoUsuario usuario, Operacion.IRepositorioAdmin admin,
    IUnidadDeTrabajo unidad, IOptions<OpcionesPlanes> planes, TimeProvider reloj)
    : CambioCatalogo(repositorio, sincronizacion, usuario, admin, unidad)
{
    public async Task<Resultado<ProductoAdminDto>?> EjecutarAsync(Guid id, ProductoEntrada entrada, CancellationToken ct)
    {
        if (await Repositorio.ProductoAsync(id, ct) is not { } producto)
        {
            return null;
        }

        var datos = AdminCatalogo.Datos(entrada);
        if (Producto.Validar(datos) is { } invalido)
        {
            return Resultado.Fallo<ProductoAdminDto>(invalido);
        }

        if (AdminCatalogo.NombreRepetido(await Repositorio.ProductosAsync(ct), datos.Nombre, id))
        {
            return Resultado.Fallo<ProductoAdminDto>(ErroresCatalogo.NombreRepetido);
        }

        var ahora = reloj.GetUtcNow();
        var antes = AdminCatalogo.Foto(producto);
        var precios = producto.Precios.ToList();
        producto.Editar(datos, ahora);

        // En borrador el formulario también trae el precio; publicado, el precio cambia por su propia acción.
        if (producto.Estado == EstadoProducto.Borrador && entrada is { PrecioPrimera: { } primera, PrecioAdicional: { } adicional })
        {
            if (Producto.ValidarPrecio(primera, adicional) is { } malPrecio)
            {
                return Resultado.Fallo<ProductoAdminDto>(malPrecio);
            }

            if (producto.PrecioPendiente is not { } actual || actual.PrecioPrimera != primera || actual.PrecioAdicional != adicional)
            {
                producto.CambiarPrecio(primera, adicional, planes.Value.Moneda, ahora);
            }
        }

        return await GuardarYSincronizarAsync(producto, AccionAuditoriaAdmin.ProductoEditado, antes, precios, ahora, ct);
    }
}

/// <summary>
/// RF-095: en Stripe un precio no se edita. Se crea una versión nueva; al quedar en Stripe pasa a vigente y la anterior
/// se archiva. Quien ya paga conserva su versión.
/// </summary>
public sealed class CambiarPrecioProductoCasoUso(
    IRepositorioCatalogo repositorio, SincronizacionCatalogo sincronizacion, IContextoUsuario usuario, Operacion.IRepositorioAdmin admin,
    IUnidadDeTrabajo unidad, IOptions<OpcionesPlanes> planes, TimeProvider reloj)
    : CambioCatalogo(repositorio, sincronizacion, usuario, admin, unidad)
{
    public async Task<Resultado<ProductoAdminDto>?> EjecutarAsync(Guid id, PrecioEntrada entrada, CancellationToken ct)
    {
        if (await Repositorio.ProductoAsync(id, ct) is not { } producto)
        {
            return null;
        }

        if (Producto.ValidarPrecio(entrada.PrecioPrimera, entrada.PrecioAdicional) is { } invalido)
        {
            return Resultado.Fallo<ProductoAdminDto>(invalido);
        }

        if (producto.Estado == EstadoProducto.Archivado)
        {
            return Resultado.Fallo<ProductoAdminDto>(ErroresCatalogo.YaArchivado);
        }

        var referencia = producto.PrecioPendiente ?? producto.PrecioVigente;
        if (referencia is not null && referencia.PrecioPrimera == entrada.PrecioPrimera && referencia.PrecioAdicional == entrada.PrecioAdicional)
        {
            return Resultado.Fallo<ProductoAdminDto>(ErroresCatalogo.SinCambiosPrecio);
        }

        var ahora = reloj.GetUtcNow();
        var antes = AdminCatalogo.Foto(producto);
        var precios = producto.Precios.ToList();
        producto.CambiarPrecio(entrada.PrecioPrimera, entrada.PrecioAdicional, planes.Value.Moneda, ahora);
        return await GuardarYSincronizarAsync(producto, AccionAuditoriaAdmin.PrecioCambiado, antes, precios, ahora, ct);
    }
}

/// <summary>RF-095: publica el borrador en Stripe. Solo un producto por grupo se vende a la vez.</summary>
public sealed class PublicarProductoCasoUso(
    IRepositorioCatalogo repositorio, SincronizacionCatalogo sincronizacion, IContextoUsuario usuario, Operacion.IRepositorioAdmin admin,
    IUnidadDeTrabajo unidad, TimeProvider reloj)
    : CambioCatalogo(repositorio, sincronizacion, usuario, admin, unidad)
{
    public async Task<Resultado<ProductoAdminDto>?> EjecutarAsync(Guid id, CancellationToken ct)
    {
        if (await Repositorio.ProductoAsync(id, ct) is not { } producto)
        {
            return null;
        }

        if ((await Repositorio.ProductosAsync(ct)).Any(p => p.Id != id && p.Estado == EstadoProducto.Activo && p.Alcance == producto.Alcance))
        {
            return Resultado.Fallo<ProductoAdminDto>(ErroresCatalogo.OtroActivo);
        }

        var ahora = reloj.GetUtcNow();
        var antes = AdminCatalogo.Foto(producto);
        var publicado = producto.Publicar(ahora);
        if (publicado.EsFallo)
        {
            return Resultado.Fallo<ProductoAdminDto>(publicado.Error);
        }

        return await GuardarYSincronizarAsync(producto, AccionAuditoriaAdmin.ProductoPublicado, antes, producto.Precios.ToList(), ahora, ct);
    }
}

/// <summary>RF-095: deja de venderse aquí y en Stripe. Quien ya lo paga lo conserva con su precio.</summary>
public sealed class ArchivarProductoCasoUso(
    IRepositorioCatalogo repositorio, SincronizacionCatalogo sincronizacion, IContextoUsuario usuario, Operacion.IRepositorioAdmin admin,
    IUnidadDeTrabajo unidad, TimeProvider reloj)
    : CambioCatalogo(repositorio, sincronizacion, usuario, admin, unidad)
{
    public async Task<Resultado<ProductoAdminDto>?> EjecutarAsync(Guid id, CancellationToken ct)
    {
        if (await Repositorio.ProductoAsync(id, ct) is not { } producto)
        {
            return null;
        }

        var ahora = reloj.GetUtcNow();
        var antes = AdminCatalogo.Foto(producto);
        var archivado = producto.Archivar(ahora);
        if (archivado.EsFallo)
        {
            return Resultado.Fallo<ProductoAdminDto>(archivado.Error);
        }

        return await GuardarYSincronizarAsync(producto, AccionAuditoriaAdmin.ProductoArchivado, antes, producto.Precios.ToList(), ahora, ct);
    }
}

/// <summary>RF-095: reintenta llevar a Stripe lo que quedó pendiente (o adopta los precios de la configuración).</summary>
public sealed class SincronizarProductoCasoUso(
    IRepositorioCatalogo repositorio, SincronizacionCatalogo sincronizacion, IUnidadDeTrabajo unidad)
{
    public async Task<Resultado<ProductoAdminDto>?> EjecutarAsync(Guid id, CancellationToken ct)
    {
        if (await repositorio.ProductoAsync(id, ct) is not { } producto)
        {
            return null;
        }

        await sincronizacion.SincronizarAsync(producto, ct);
        await unidad.EnTransaccionAsync(() => Task.FromResult(true), ct);
        return Resultado.Exito(AdminCatalogo.Dto(producto, await repositorio.PreciosSuscripcionAsync(ct)));
    }
}
