using Aulisima.Domain.Comun;

namespace Aulisima.Domain.Planes;

/// <summary>
/// ADR-0017: lo que el código sabe bloquear o desbloquear. Cada función necesita su candado en el código; el
/// administrador solo elige cuáles incluye cada producto.
/// </summary>
public static class FuncionCatalogo
{
    /// <summary>Promedios por rubro, del periodo y final en la tabla y en la ficha del alumno (RN-097).</summary>
    public const string Promedios = "promedios";

    /// <summary>Exportación del periodo a Excel (RN-097).</summary>
    public const string ExportarExcel = "exportar_excel";

    /// <summary>Avisos a tutores por push y correo (RN-097); los urgentes por WhatsApp van aparte.</summary>
    public const string Avisos = "avisos";

    /// <summary>Cierre de periodo con observaciones redactadas por IA.</summary>
    public const string CierreIa = "cierre_ia";

    /// <summary>Boletas PDF publicadas en el portal de tutores.</summary>
    public const string Boletas = "boletas";

    /// <summary>Aviso de boletas por el WhatsApp del sistema.</summary>
    public const string WhatsappBoletas = "whatsapp_boletas";

    /// <summary>Avisos urgentes que también llegan por WhatsApp.</summary>
    public const string AvisosUrgentes = "avisos_urgentes";

    public static readonly IReadOnlyList<string> Todas = [Promedios, ExportarExcel, Avisos, CierreIa, Boletas, WhatsappBoletas, AvisosUrgentes];
}

public static class EstadoProducto
{
    /// <summary>Se puede revisar y cambiar; no existe en Stripe ni se vende.</summary>
    public const string Borrador = "borrador";

    /// <summary>Se ofrece en Plan cuando su precio vigente está sincronizado con Stripe.</summary>
    public const string Activo = "activo";

    /// <summary>Ya no se vende; quien lo paga lo conserva.</summary>
    public const string Archivado = "archivado";
}

/// <summary>A qué se le cobra el producto. Por ahora solo por grupo (RN-082).</summary>
public static class AlcanceProducto
{
    public const string Grupo = "grupo";

    public static readonly IReadOnlyList<string> Todos = [Grupo];
}

public static class EstadoPrecio
{
    /// <summary>Guardado aquí y aún sin sus precios en Stripe; la versión anterior sigue vigente.</summary>
    public const string Pendiente = "pendiente";
    public const string Vigente = "vigente";

    /// <summary>Ya no se vende; quien la tiene la sigue pagando.</summary>
    public const string Archivado = "archivado";
}

/// <summary>
/// Un producto del catálogo (ADR-0017): qué se vende, qué funciones desbloquea y sus versiones de precio. Lo crea y lo
/// cambia el administrador; Aulísima lo publica en el proveedor de pagos.
/// </summary>
public sealed class Producto : Entidad
{
    public const int LongitudNombre = 60;
    public const int LongitudDescripcion = 300;
    public const int MaximoMesesPrepago = 12;

    private readonly List<PrecioProducto> _precios = [];

    public Producto(DatosProducto datos, DateTimeOffset ahora)
        : base(ahora)
    {
        Alcance = AlcanceProducto.Grupo;
        Aplicar(datos);
    }

    /// <summary>Para el producto sembrado por la migración, con id fijo.</summary>
    public Producto(Guid id, DatosProducto datos, string estado, DateTimeOffset ahora)
        : base(id, ahora)
    {
        Alcance = AlcanceProducto.Grupo;
        Estado = estado;
        Aplicar(datos);
    }

    private Producto()
    {
    }

    public string Nombre { get; private set; } = string.Empty;

    /// <summary>Lo que el docente lee en Plan.</summary>
    public string Descripcion { get; private set; } = string.Empty;

    public string Alcance { get; private set; } = AlcanceProducto.Grupo;

    public List<string> Funciones { get; private set; } = [];

    /// <summary>RN-084: meses que se pueden pagar por adelantado en OXXO (por ejemplo 1, 3 y 6).</summary>
    public List<int> MesesPrepago { get; private set; } = [];

    public int Orden { get; private set; }

    public string Estado { get; private set; } = EstadoProducto.Borrador;

    /// <summary>El producto en el proveedor (<c>prod_…</c>).</summary>
    public string? IdExterno { get; private set; }

    /// <summary>El nombre o la descripción cambiaron y falta llevarlos al proveedor.</summary>
    public bool CambiosPendientes { get; private set; }

    /// <summary>Archivado aquí y falta archivarlo en el proveedor.</summary>
    public bool ArchivoPendiente { get; private set; }

    /// <summary>El último error al sincronizar (solo tipo y código del proveedor); nulo si la última vez salió bien.</summary>
    public string? ErrorSincronizacion { get; private set; }

    public DateTimeOffset? SincronizadoEn { get; private set; }

    public IReadOnlyList<PrecioProducto> Precios => _precios;

    public PrecioProducto? PrecioVigente => _precios.FirstOrDefault(p => p.Estado == EstadoPrecio.Vigente);

    public PrecioProducto? PrecioPendiente => _precios.FirstOrDefault(p => p.Estado == EstadoPrecio.Pendiente);

    /// <summary>Se ofrece a los docentes: activo y con un precio vigente.</summary>
    public bool SeVende => Estado == EstadoProducto.Activo && PrecioVigente is not null;

    /// <summary>Hay algo que llevar al proveedor.</summary>
    public bool PorSincronizar =>
        (Estado == EstadoProducto.Activo && (IdExterno is null || CambiosPendientes || PrecioPendiente is not null))
        || (ArchivoPendiente && IdExterno is not null) || _precios.Any(p => p.ArchivoPendiente);

    public bool Incluye(string funcion) => Funciones.Contains(funcion);

    public static ErrorNegocio? Validar(DatosProducto d) =>
        string.IsNullOrWhiteSpace(d.Nombre) || d.Nombre.Trim().Length > LongitudNombre
        || d.Descripcion.Trim().Length > LongitudDescripcion
        || d.Funciones.Count == 0 || d.Funciones.Any(f => !FuncionCatalogo.Todas.Contains(f))
        || d.MesesPrepago.Any(m => m < 1 || m > MaximoMesesPrepago)
        || d.Orden is < 1 or > 999
            ? ErroresCatalogo.DatosInvalidos
            : null;

    public static ErrorNegocio? ValidarPrecio(decimal primera, decimal adicional) =>
        primera is < 1m or > 10_000m || adicional is < 0m or > 10_000m || decimal.Round(primera, 2) != primera
        || decimal.Round(adicional, 2) != adicional
            ? ErroresCatalogo.PrecioInvalido
            : null;

    public void Editar(DatosProducto datos, DateTimeOffset ahora)
    {
        var cambiaTexto = datos.Nombre.Trim() != Nombre || datos.Descripcion.Trim() != Descripcion;
        Aplicar(datos);
        if (cambiaTexto && IdExterno is not null)
        {
            CambiosPendientes = true;
        }

        MarcarActualizada(ahora);
    }

    /// <summary>
    /// En borrador el precio se reemplaza (no existe en el proveedor). Publicado, se agrega una versión pendiente; la
    /// vigente sigue hasta que la nueva quede en el proveedor. Una pendiente anterior sin sincronizar se reemplaza.
    /// </summary>
    public PrecioProducto CambiarPrecio(decimal primera, decimal adicional, string moneda, DateTimeOffset ahora)
    {
        foreach (var aMedias in _precios.Where(p => p.Estado == EstadoPrecio.Pendiente).ToList())
        {
            if (aMedias.IdExternoPrimera is null && aMedias.IdExternoAdicional is null)
            {
                _precios.Remove(aMedias);
            }
            else
            {
                aMedias.Archivar(ahora);
            }
        }

        if (Estado == EstadoProducto.Borrador)
        {
            _precios.RemoveAll(p => p.IdExternoPrimera is null);
        }

        var version = _precios.Count == 0 ? 1 : _precios.Max(p => p.Version) + 1;
        var precio = new PrecioProducto(Id, version, primera, adicional, moneda, ahora);
        _precios.Add(precio);
        MarcarActualizada(ahora);
        return precio;
    }

    public Resultado Publicar(DateTimeOffset ahora)
    {
        if (Estado != EstadoProducto.Borrador)
        {
            return Resultado.Fallo(ErroresCatalogo.NoEsBorrador);
        }

        if (PrecioPendiente is null)
        {
            return Resultado.Fallo(ErroresCatalogo.SinPrecio);
        }

        Estado = EstadoProducto.Activo;
        MarcarActualizada(ahora);
        return Resultado.Exito();
    }

    public Resultado Archivar(DateTimeOffset ahora)
    {
        if (Estado == EstadoProducto.Archivado)
        {
            return Resultado.Fallo(ErroresCatalogo.YaArchivado);
        }

        ArchivoPendiente = IdExterno is not null;
        Estado = EstadoProducto.Archivado;
        foreach (var pendiente in _precios.Where(p => p.Estado == EstadoPrecio.Pendiente).ToList())
        {
            pendiente.Archivar(ahora);
        }

        MarcarActualizada(ahora);
        return Resultado.Exito();
    }

    public void RegistrarIdExterno(string idExterno, DateTimeOffset ahora)
    {
        IdExterno = idExterno;
        CambiosPendientes = false;
        MarcarActualizada(ahora);
    }

    public void RegistrarCambiosSincronizados(DateTimeOffset ahora)
    {
        CambiosPendientes = false;
        MarcarActualizada(ahora);
    }

    public void RegistrarArchivoSincronizado(DateTimeOffset ahora)
    {
        ArchivoPendiente = false;
        MarcarActualizada(ahora);
    }

    /// <summary>La versión pendiente ya tiene sus precios en el proveedor: pasa a vigente y la anterior se archiva.</summary>
    public void ActivarPrecio(PrecioProducto precio, DateTimeOffset ahora)
    {
        var anterior = PrecioVigente;
        if (anterior is not null && anterior != precio)
        {
            anterior.Archivar(ahora);
        }

        precio.Activar(ahora);
        MarcarActualizada(ahora);
    }

    public void RegistrarSincronizacion(string? error, DateTimeOffset ahora)
    {
        ErrorSincronizacion = error;
        if (error is null)
        {
            SincronizadoEn = ahora;
        }

        MarcarActualizada(ahora);
    }

    private void Aplicar(DatosProducto d)
    {
        Nombre = d.Nombre.Trim();
        Descripcion = d.Descripcion.Trim();
        Funciones = [.. FuncionCatalogo.Todas.Where(d.Funciones.Contains)];
        MesesPrepago = [.. d.MesesPrepago.Distinct().Order()];
        Orden = d.Orden;
    }
}

public sealed record DatosProducto(string Nombre, string Descripcion, IReadOnlyList<string> Funciones, IReadOnlyList<int> MesesPrepago, int Orden);

/// <summary>
/// Una versión de precio de un producto (ADR-0017). En el proveedor un precio no se edita: cambiarlo crea otra versión y
/// archiva la anterior, que siguen pagando quienes ya la tenían.
/// </summary>
public sealed class PrecioProducto : Entidad
{
    public PrecioProducto(Guid productoId, int version, decimal primera, decimal adicional, string moneda, DateTimeOffset ahora)
        : base(ahora)
    {
        ProductoId = productoId;
        Version = version;
        PrecioPrimera = primera;
        PrecioAdicional = adicional;
        Moneda = moneda;
    }

    /// <summary>Para la versión sembrada por la migración, con id fijo.</summary>
    public PrecioProducto(Guid id, Guid productoId, int version, decimal primera, decimal adicional, string moneda, string estado, DateTimeOffset ahora)
        : base(id, ahora)
    {
        ProductoId = productoId;
        Version = version;
        PrecioPrimera = primera;
        PrecioAdicional = adicional;
        Moneda = moneda;
        Estado = estado;
        VigenteDesde = estado == EstadoPrecio.Vigente ? ahora : null;
    }

    private PrecioProducto()
    {
    }

    public Guid ProductoId { get; private set; }

    public int Version { get; private set; }

    /// <summary>RN-082: la primera unidad (el primer grupo), en MXN con IVA.</summary>
    public decimal PrecioPrimera { get; private set; }

    /// <summary>RN-082: cada unidad adicional.</summary>
    public decimal PrecioAdicional { get; private set; }

    public string Moneda { get; private set; } = "MXN";

    public string Estado { get; private set; } = EstadoPrecio.Pendiente;

    /// <summary>El precio recurrente de la primera unidad en el proveedor (<c>price_…</c>).</summary>
    public string? IdExternoPrimera { get; private set; }

    public string? IdExternoAdicional { get; private set; }

    public DateTimeOffset? VigenteDesde { get; private set; }

    public DateTimeOffset? ArchivadoEn { get; private set; }

    /// <summary>Archivado aquí y falta archivar sus precios en el proveedor.</summary>
    public bool ArchivoPendiente { get; private set; }

    public bool Sincronizado => IdExternoPrimera is not null && IdExternoAdicional is not null;

    public decimal Mensual(int unidades) => Tarifa.Mensual(unidades, PrecioPrimera, PrecioAdicional);

    public void RegistrarIdsExternos(string? primera, string? adicional, DateTimeOffset ahora)
    {
        IdExternoPrimera = primera ?? IdExternoPrimera;
        IdExternoAdicional = adicional ?? IdExternoAdicional;
        MarcarActualizada(ahora);
    }

    public void RegistrarArchivoSincronizado(DateTimeOffset ahora)
    {
        ArchivoPendiente = false;
        MarcarActualizada(ahora);
    }

    internal void Activar(DateTimeOffset ahora)
    {
        Estado = EstadoPrecio.Vigente;
        VigenteDesde = ahora;
        MarcarActualizada(ahora);
    }

    internal void Archivar(DateTimeOffset ahora)
    {
        Estado = EstadoPrecio.Archivado;
        ArchivadoEn = ahora;
        ArchivoPendiente = IdExternoPrimera is not null || IdExternoAdicional is not null;
        MarcarActualizada(ahora);
    }
}

/// <summary>El producto que la migración crea con los precios del piloto (79 + 29 MXN, ADR-0006).</summary>
public static class CatalogoInicial
{
    public static readonly Guid ProductoProId = Guid.Parse("0199a3c0-0000-7000-8000-000000000001");
    public static readonly Guid PrecioProV1Id = Guid.Parse("0199a3c0-0000-7000-8000-000000000002");
}

public static class ErroresCatalogo
{
    public static readonly ErrorNegocio DatosInvalidos = new(
        "producto_datos_invalidos",
        "El producto lleva nombre de hasta 60 caracteres, descripción de hasta 300, al menos una función, meses de prepago de 1 a 12 y un orden de 1 a 999.");

    public static readonly ErrorNegocio PrecioInvalido = new(
        "precio_invalido", "El primer grupo cuesta de 1 a 10,000 MXN y cada adicional de 0 a 10,000, con hasta 2 decimales.");

    public static readonly ErrorNegocio SinPrecio = new("producto_sin_precio", "Ponle precio al producto antes de publicarlo.");
    public static readonly ErrorNegocio NoEsBorrador = new("producto_no_es_borrador", "Este producto ya está publicado.");
    public static readonly ErrorNegocio YaArchivado = new("producto_archivado", "Este producto está archivado y ya no se vende.");
    public static readonly ErrorNegocio NombreRepetido = new("producto_repetido", "Ya hay un producto con ese nombre.");

    public static readonly ErrorNegocio OtroActivo = new(
        "otro_producto_activo", "Ya se vende otro producto por grupo. Archívalo antes de publicar este.");

    public static readonly ErrorNegocio SinCambiosPrecio = new("precio_sin_cambios", "Ese ya es el precio vigente.");

    public static readonly ErrorNegocio SinPlanALaVenta = new(
        "sin_plan_a_la_venta", "Por ahora no hay un plan Pro a la venta. Inténtalo más tarde.");

    public static readonly ErrorNegocio Sincronizacion = new(
        "sincronizacion_fallida", "Stripe no respondió. El cambio quedó guardado; reintenta la sincronización.");

    public static readonly IReadOnlyList<ErrorNegocio> Todos =
    [
        DatosInvalidos, PrecioInvalido, SinPrecio, NoEsBorrador, YaArchivado, NombreRepetido, OtroActivo, SinCambiosPrecio,
        SinPlanALaVenta, Sincronizacion,
    ];
}
