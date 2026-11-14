using Aulisima.Application.Avisos;
using Aulisima.Application.Comun;
using Aulisima.Application.Cuentas;
using Aulisima.Application.Grupos;
using Aulisima.Domain.Comun;
using Aulisima.Domain.Grupos;
using Aulisima.Domain.Planes;
using Microsoft.Extensions.Options;

namespace Aulisima.Application.Planes;

/// <summary>
/// Precios y límites vigentes (SPEC-08, <c>GET /planes</c>). Nombre, funciones, precios y meses de OXXO salen del producto
/// a la venta en el catálogo (ADR-0017); <see cref="ALaVenta"/> es falso si no hay uno listo para cobrarse.
/// </summary>
public sealed record PlanesDto(
    bool ALaVenta,
    string Producto,
    string Descripcion,
    IReadOnlyList<string> Funciones,
    decimal PrecioPrimerGrupo,
    decimal PrecioGrupoAdicional,
    string Moneda,
    int DiasPrueba,
    int DiasMaximoPrueba,
    int DiasGracia,
    IReadOnlyList<int> MesesPrepago,
    bool OxxoHabilitado,
    decimal MontoMinimoOxxo,
    decimal MontoMaximoOxxo,
    int UrgentesPorTutorPorMes,
    int BoletasPorTutorPorPeriodo,
    bool PagoSimulado);

/// <summary>Un grupo activo en la pantalla Plan. <see cref="EnSuscripcion"/>: el docente lo está pagando.</summary>
public sealed record GrupoPlanDto(
    Guid GrupoId, string Nombre, string Nivel, int Grado, string? Materia, string CicloNombre, int AlumnosActivos, bool EnSuscripcion,
    PlanGrupoDto Plan);

/// <summary>RN-081: la prueba Pro del docente, si ya la usó.</summary>
public sealed record PruebaDto(Guid GrupoId, string GrupoNombre, bool Vigente, int DiasRestantes, bool HastaPublicar, DateTimeOffset Fin);

/// <summary>Un pago único (OXXO o grupos agregados a un prepago): el historial sin portal (SPEC-08, paso 5).</summary>
public sealed record PagoDto(
    Guid Id, string Tipo, int Meses, decimal Monto, int Grupos, string Estado, DateTimeOffset CreadoEn, DateTimeOffset? VenceEn,
    DateTimeOffset? PagadoEn, string? UrlFicha);

/// <summary>Qué banda de pago muestra la app (RN-085, RN-089).</summary>
public static class TipoAvisoPago
{
    /// <summary>El prepago vence pronto (7 o 1 días).</summary>
    public const string PrepagoPorVencer = "prepago_por_vencer";

    /// <summary>Un cobro con tarjeta falló: corre la gracia.</summary>
    public const string CobroFallido = "cobro_fallido";

    /// <summary>El prepago venció: corre la gracia.</summary>
    public const string PrepagoVencido = "prepago_vencido";
}

public sealed record AvisoPagoDto(string Tipo, DateTimeOffset Fecha, int Dias);

/// <summary>
/// La pantalla Plan. <see cref="TotalMensual"/>: lo que cuestan hoy los grupos pagados. <see cref="ProximoCobro"/>:
/// la fecha del siguiente cargo con tarjeta. <see cref="RenovarPrepago"/>: el prepago está por vencer o venció y se
/// puede pagar otro periodo o pasar a tarjeta. <see cref="AgregarAPrepago"/>: lo que se elija de más se cobra hasta
/// <see cref="PagadoHasta"/> (RN-089).
/// </summary>
/// <see cref="PrecioPrimerGrupo"/> y <see cref="PrecioGrupoAdicional"/>: la versión que paga (ADR-0017), que puede ser
/// anterior a la que se vende hoy (<see cref="ConservaPrecio"/>).
public sealed record SuscripcionDto(
    string Estado,
    string? Modalidad,
    IReadOnlyList<GrupoPlanDto> Grupos,
    decimal TotalMensual,
    decimal PrecioPrimerGrupo,
    decimal PrecioGrupoAdicional,
    bool ConservaPrecio,
    DateTimeOffset? ProximoCobro,
    DateTimeOffset? PagadoHasta,
    DateTimeOffset? GraciaHasta,
    PruebaDto? Prueba,
    bool PruebaDisponible,
    AvisoPagoDto? Aviso,
    bool PuedeAdministrarTarjeta,
    bool RenovarPrepago,
    bool AgregarAPrepago,
    PagoDto? PagoPendiente,
    IReadOnlyList<PagoDto> Pagos);

/// <summary>RN-084: <c>tarjeta</c>, u <c>oxxo</c> con <see cref="Meses"/> de los que ofrece el producto.</summary>
public sealed record CrearCheckout(IReadOnlyList<Guid>? GrupoIds, string? Modalidad, int? Meses);

public sealed record CheckoutDto(string Url);

public sealed record CambiarGruposPro(IReadOnlyList<Guid>? GrupoIds);

public sealed record PortalDto(string Url);

/// <summary>Lo que se calcula igual en la consulta y al pagar.</summary>
internal static class ReglasPlanes
{
    public const string ModalidadOxxo = "oxxo";

    public static bool PrepagoActivo(Suscripcion? s, DateTimeOffset ahora) =>
        s is { Modalidad: ModalidadPago.PrepagoOxxo, Estado: EstadoSuscripcion.Activa, PagadoHasta: { } hasta } && hasta > ahora;

    /// <summary>RN-089: a partir del primer aviso de vencimiento (o ya en gracia) el prepago se renueva.</summary>
    public static bool RenovarPrepago(Suscripcion? s, DateTimeOffset ahora, OpcionesPlanes opciones) =>
        s is { Modalidad: ModalidadPago.PrepagoOxxo }
        && (s.Estado == EstadoSuscripcion.EnGracia
            || (PrepagoActivo(s, ahora) && s.PagadoHasta!.Value - ahora <= TimeSpan.FromDays(opciones.DiasAvisoPrepago.Max())));

    /// <summary>RN-089: durante el prepago, un grupo más se paga solo hasta su fin.</summary>
    public static bool AgregarAPrepago(Suscripcion? s, DateTimeOffset ahora, OpcionesPlanes opciones) =>
        PrepagoActivo(s, ahora) && !RenovarPrepago(s, ahora, opciones);

    /// <summary>RN-089: la diferencia del total mensual por los días que faltan del prepago, en centavos exactos.</summary>
    public static decimal MontoAgregar(
        int actuales, int agregados, DateTimeOffset pagadoHasta, DateTimeOffset ahora, PrecioProducto precio, OpcionesPlanes opciones)
    {
        var dias = (int)Math.Ceiling((pagadoHasta - ahora).TotalDays);
        var diferencia = precio.Mensual(actuales + agregados) - precio.Mensual(actuales);
        return Math.Round(diferencia * dias / opciones.DiasPorMes, 2, MidpointRounding.AwayFromZero);
    }

    public static AvisoPagoDto? Aviso(Suscripcion? s, DateTimeOffset ahora, OpcionesPlanes opciones)
    {
        static int Dias(DateTimeOffset fecha, DateTimeOffset ahora) => Math.Max(0, (int)Math.Ceiling((fecha - ahora).TotalDays));

        return s switch
        {
            { Estado: EstadoSuscripcion.EnGracia, GraciaHasta: { } fin } => new AvisoPagoDto(
                s.Modalidad == ModalidadPago.PrepagoOxxo ? TipoAvisoPago.PrepagoVencido : TipoAvisoPago.CobroFallido, fin, Dias(fin, ahora)),
            { PagadoHasta: { } hasta } when PrepagoActivo(s, ahora) && hasta - ahora <= TimeSpan.FromDays(opciones.DiasAvisoPrepago.Max())
                => new AvisoPagoDto(TipoAvisoPago.PrepagoPorVencer, hasta, Dias(hasta, ahora)),
            _ => null,
        };
    }

    public static PagoDto Pago(Pago p) =>
        new(p.Id, p.Tipo, p.Meses, p.Monto, p.Grupos.Count, p.Estado, p.CreatedAt, p.VenceEn, p.PagadoEn, p.Estado == EstadoPago.Pendiente ? p.UrlFicha : null);

    /// <summary>Los grupos pedidos, sin repetir, todos del docente y sin archivar.</summary>
    public static async Task<Resultado<IReadOnlyList<Grupo>>> GruposAsync(IReadOnlyList<Guid>? ids, IRepositorioGrupos grupos, CancellationToken ct)
    {
        var pedidos = (ids ?? []).Distinct().ToList();
        if (pedidos.Count == 0)
        {
            return Resultado.Fallo<IReadOnlyList<Grupo>>(ErroresPlanes.SinGrupos);
        }

        var encontrados = new List<Grupo>();
        foreach (var id in pedidos)
        {
            if (await grupos.GrupoAsync(id, ct) is not { EstaArchivado: false } grupo)
            {
                return Resultado.Fallo<IReadOnlyList<Grupo>>(ErroresPlanes.GrupoNoDisponible);
            }

            encontrados.Add(grupo);
        }

        return Resultado.Exito<IReadOnlyList<Grupo>>(encontrados);
    }
}

public sealed class ObtenerPlanesCasoUso(
    CatalogoPlanes catalogo, IOptions<OpcionesPlanes> planes, IOptions<OpcionesWhatsapp> whatsapp, IOptions<OpcionesPagos> pagos)
{
    public async Task<PlanesDto> EjecutarAsync(CancellationToken ct)
    {
        var p = planes.Value;
        var oferta = await catalogo.ALaVentaAsync(ct);
        return new PlanesDto(
            oferta is not null, oferta?.Producto.Nombre ?? string.Empty, oferta?.Producto.Descripcion ?? string.Empty,
            oferta?.Producto.Funciones ?? [], oferta?.Precio.PrecioPrimera ?? 0m, oferta?.Precio.PrecioAdicional ?? 0m,
            oferta?.Precio.Moneda ?? p.Moneda, p.DiasPrueba, p.DiasMaximoPrueba, p.DiasGracia, oferta?.Producto.MesesPrepago ?? [],
            p.OxxoHabilitado && oferta?.Producto.MesesPrepago.Count > 0, p.MontoMinimoOxxo, p.MontoMaximoOxxo, whatsapp.Value.UrgentesPorTutorPorMes,
            whatsapp.Value.BoletasPorTutorPorPeriodo, pagos.Value.Proveedor == OpcionesPagos.ProveedorSimulado);
    }
}

/// <summary>SPEC-08, pantalla Plan: estado, grupos con su plan, próximo cobro, prueba, banda de pago e historial.</summary>
public sealed class ObtenerSuscripcionCasoUso(
    IRepositorioGrupos grupos, IRepositorioPlanes planes, AccesoPro acceso, CatalogoPlanes catalogo, TimeProvider reloj)
{
    public async Task<SuscripcionDto> EjecutarAsync(CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var opciones = acceso.Opciones;
        var s = await acceso.SuscripcionAsync(ct);
        var activos = await grupos.GruposAsync(archivados: false, ct);
        var pagados = s?.GruposPro.ToHashSet() ?? [];
        var lista = new List<GrupoPlanDto>();
        foreach (var g in activos)
        {
            lista.Add(new GrupoPlanDto(
                g.Grupo.Id, g.Grupo.Nombre, g.Grupo.Nivel, g.Grupo.Grado, g.Grupo.Materia, g.CicloNombre, g.AlumnosActivos,
                pagados.Contains(g.Grupo.Id), await acceso.PlanAsync(g.Grupo, ct)));
        }

        PruebaDto? prueba = null;
        if (s?.Prueba(opciones.DiasPrueba, opciones.DiasMaximoPrueba) is { } vigencia)
        {
            var grupo = await grupos.GrupoAsync(vigencia.GrupoId, ct);
            prueba = new PruebaDto(
                vigencia.GrupoId, grupo?.Nombre ?? string.Empty, vigencia.Vigente(ahora), vigencia.DiasRestantes(ahora),
                vigencia.TerminaAlPublicar(ahora), vigencia.Fin);
        }

        var pagos = (await planes.PagosAsync(ct)).OrderByDescending(p => p.CreatedAt).ToList();
        var pendiente = pagos.FirstOrDefault(p => p.Estado == EstadoPago.Pendiente && (p.VenceEn is null || p.VenceEn > ahora));
        var cuenta = pagados.Count(id => activos.Any(g => g.Grupo.Id == id));
        var paga = s is { Estado: EstadoSuscripcion.Activa or EstadoSuscripcion.EnGracia };
        var oferta = await catalogo.ALaVentaAsync(ct);
        var precio = paga ? await catalogo.PrecioAsync(s!.PrecioId, ct) : oferta?.Precio;
        return new SuscripcionDto(
            s?.Estado ?? EstadoSuscripcion.Ninguna,
            s?.Modalidad,
            lista,
            paga ? precio?.Mensual(cuenta) ?? 0m : 0m,
            precio?.PrecioPrimera ?? 0m,
            precio?.PrecioAdicional ?? 0m,
            paga && precio is not null && precio.Id != oferta?.Precio.Id,
            s is { EsTarjetaActiva: true } ? s.PeriodoActualFin : null,
            s is { EsPrepagoVigente: true } ? s.PagadoHasta : null,
            s?.GraciaHasta,
            prueba,
            s?.PruebaUsada != true,
            ReglasPlanes.Aviso(s, ahora, opciones),
            s is { EsTarjetaActiva: true, ClienteExterno: not null },
            ReglasPlanes.RenovarPrepago(s, ahora, opciones),
            ReglasPlanes.AgregarAPrepago(s, ahora, opciones),
            pendiente is null ? null : ReglasPlanes.Pago(pendiente),
            pagos.Select(ReglasPlanes.Pago).ToList());
    }
}

/// <summary>
/// RF-084, RN-082 a RN-084, RN-089: abre el Checkout del proveedor con los grupos elegidos. Tarjeta crea la
/// suscripción mensual; OXXO, un pago único por 3 o 6 meses (o, durante un prepago, solo los grupos agregados hasta su
/// fin). Nada cambia de plan aquí: lo hace el evento del proveedor (RN-087).
/// </summary>
public sealed class CrearCheckoutCasoUso(
    IRepositorioGrupos grupos,
    IRepositorioPlanes planes,
    IRepositorioCuentas cuentas,
    IPasarelaPagos pasarela,
    AccesoPro acceso,
    CatalogoPlanes catalogo,
    IContextoUsuario usuario,
    IUnidadDeTrabajo unidad,
    IOptions<OpcionesCuentas> opcionesCuentas,
    TimeProvider reloj)
{
    public async Task<Resultado<CheckoutDto>> EjecutarAsync(CrearCheckout comando, CancellationToken ct)
    {
        var docenteId = usuario.DocenteId ?? throw new InvalidOperationException("Pagar requiere un docente.");
        var opciones = acceso.Opciones;
        var oxxo = comando.Modalidad == ReglasPlanes.ModalidadOxxo;
        if (await catalogo.ALaVentaAsync(ct) is not { } oferta)
        {
            return Resultado.Fallo<CheckoutDto>(ErroresCatalogo.SinPlanALaVenta);
        }

        if (comando.Modalidad is not (ModalidadPago.Tarjeta or ReglasPlanes.ModalidadOxxo)
            || (oxxo && (comando.Meses is not { } m || !oferta.Producto.MesesPrepago.Contains(m))))
        {
            return Resultado.Fallo<CheckoutDto>(ErroresPlanes.ModalidadInvalida);
        }

        if (oxxo && !opciones.OxxoHabilitado)
        {
            return Resultado.Fallo<CheckoutDto>(ErroresPlanes.OxxoNoDisponible);
        }

        var elegidos = await ReglasPlanes.GruposAsync(comando.GrupoIds, grupos, ct);
        if (elegidos.EsFallo)
        {
            return Resultado.Fallo<CheckoutDto>(elegidos.Error);
        }

        var ids = elegidos.Valor.Select(g => g.Id).ToList();
        var ahora = reloj.GetUtcNow();
        var s = await acceso.SuscripcionOCrearAsync(docenteId, ct);
        if (s.EsTarjetaActiva)
        {
            return Resultado.Fallo<CheckoutDto>(ErroresPlanes.YaTieneTarjeta);
        }

        var correo = (await cuentas.PorIdAsync(docenteId, ct))?.Correo ?? throw new InvalidOperationException("El docente no tiene cuenta.");
        var web = opcionesCuentas.Value.UrlWeb.TrimEnd('/');
        var exito = $"{web}/plan?pago=listo";
        var cancelar = $"{web}/plan?pago=cancelado";

        SesionPago sesion;
        Pago? pago = null;
        if (ReglasPlanes.AgregarAPrepago(s, ahora, opciones))
        {
            var nuevos = ids.Where(id => !s.GruposPro.Contains(id)).ToList();
            if (nuevos.Count == 0)
            {
                return Resultado.Fallo<CheckoutDto>(ErroresPlanes.SinCambios);
            }

            var precio = await catalogo.PrecioAsync(s.PrecioId, ct) ?? oferta.Precio;
            var monto = ReglasPlanes.MontoAgregar(s.GruposPro.Count, nuevos.Count, s.PagadoHasta!.Value, ahora, precio, opciones);
            if (monto < opciones.MontoMinimoOxxo || monto > opciones.MontoMaximoOxxo)
            {
                return Resultado.Fallo<CheckoutDto>(ErroresPlanes.MontoFueraDeRango);
            }

            var concepto = nuevos.Count == 1 ? $"Aulísima {oferta.Producto.Nombre}: 1 grupo más" : $"Aulísima {oferta.Producto.Nombre}: {nuevos.Count} grupos más";
            sesion = await pasarela.CheckoutPagoUnicoAsync(
                new PagoUnico(
                    docenteId, correo, s.ClienteExterno, TipoPago.AgregarGrupos, nuevos, 0, monto, concepto, oxxo, opciones.DiasFichaOxxo, precio.Id,
                    exito, cancelar),
                ct);
            pago = new Pago(docenteId, TipoPago.AgregarGrupos, 0, monto, nuevos, sesion.Id, sesion.UrlFicha, sesion.VenceEn, precio.Id, ahora);
        }
        else if (oxxo)
        {
            var meses = comando.Meses!.Value;
            var monto = oferta.Precio.Mensual(ids.Count) * meses;
            if (monto < opciones.MontoMinimoOxxo || monto > opciones.MontoMaximoOxxo)
            {
                return Resultado.Fallo<CheckoutDto>(ErroresPlanes.MontoFueraDeRango);
            }

            var plazo = meses == 1 ? "1 mes" : $"{meses} meses";
            var concepto = $"Aulísima {oferta.Producto.Nombre}: {(ids.Count == 1 ? "1 grupo" : $"{ids.Count} grupos")} por {plazo}";
            sesion = await pasarela.CheckoutPagoUnicoAsync(
                new PagoUnico(
                    docenteId, correo, s.ClienteExterno, TipoPago.Prepago, ids, meses, monto, concepto, true, opciones.DiasFichaOxxo, oferta.Precio.Id,
                    exito, cancelar),
                ct);
            pago = new Pago(docenteId, TipoPago.Prepago, meses, monto, ids, sesion.Id, sesion.UrlFicha, sesion.VenceEn, oferta.Precio.Id, ahora);
        }
        else
        {
            // RN-089: con el prepago vigente se cambia a tarjeta al vencer; en gracia ya se puede.
            if (ReglasPlanes.PrepagoActivo(s, ahora))
            {
                return Resultado.Fallo<CheckoutDto>(ErroresPlanes.PrepagoVigente);
            }

            sesion = await pasarela.CheckoutSuscripcionAsync(
                new NuevaSuscripcion(
                    docenteId, correo, s.ClienteExterno, ids, oferta.Precio.Id, oferta.IdPrimera, oferta.IdAdicional, oferta.Precio.Mensual(ids.Count),
                    exito, cancelar),
                ct);
        }

        if (pago is not null)
        {
            planes.Agregar(pago);
        }

        await unidad.GuardarAsync(ct);
        return Resultado.Exito(new CheckoutDto(sesion.Url));
    }
}

/// <summary>
/// RF-083, RN-083: cambia los grupos de la suscripción con tarjeta. Agregar se prorratea; quitar aplica al siguiente
/// corte. El cambio llega por el evento del proveedor. Durante un prepago solo se quitan grupos (sin reembolso, siguen
/// en Pro hasta su fin); agregar se paga con el Checkout (RN-089).
/// </summary>
public sealed class CambiarGruposProCasoUso(
    IRepositorioGrupos grupos, IPasarelaPagos pasarela, AccesoPro acceso, CatalogoPlanes catalogo, ObtenerSuscripcionCasoUso consulta,
    IContextoUsuario usuario, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<SuscripcionDto>> EjecutarAsync(CambiarGruposPro comando, CancellationToken ct)
    {
        var docenteId = usuario.DocenteId ?? throw new InvalidOperationException("Cambiar grupos requiere un docente.");
        var elegidos = await ReglasPlanes.GruposAsync(comando.GrupoIds, grupos, ct);
        if (elegidos.EsFallo)
        {
            return Resultado.Fallo<SuscripcionDto>(elegidos.Error);
        }

        var ids = elegidos.Valor.Select(g => g.Id).ToList();
        var ahora = reloj.GetUtcNow();
        var s = await acceso.SuscripcionAsync(ct);
        if (s is null || (!s.EsTarjetaActiva && !ReglasPlanes.PrepagoActivo(s, ahora)))
        {
            return Resultado.Fallo<SuscripcionDto>(ErroresPlanes.SinTarjeta);
        }

        if (ids.ToHashSet().SetEquals(s.GruposPro))
        {
            return Resultado.Fallo<SuscripcionDto>(ErroresPlanes.SinCambios);
        }

        if (s.EsTarjetaActiva)
        {
            var agrega = ids.Any(id => !s.GruposPro.Contains(id));
            // ADR-0017: con los precios de su versión, aunque hoy se venda otra.
            var precio = await catalogo.PrecioAsync(s.PrecioId, ct) ?? throw new InvalidOperationException("La suscripción no tiene precio.");
            var (primera, adicional) = catalogo.IdsExternos(precio);
            await pasarela.ActualizarGruposAsync(docenteId, s.IdExterno!, ids, new PreciosSuscripcion(primera, adicional), agrega, ct);
        }
        else
        {
            if (ids.Any(id => !s.GruposPro.Contains(id)))
            {
                return Resultado.Fallo<SuscripcionDto>(ErroresPlanes.AgregarRequierePago);
            }

            foreach (var quitado in s.GruposPro.Except(ids).ToList())
            {
                if (await grupos.GrupoAsync(quitado, ct) is { } grupo)
                {
                    grupo.TerminarProEn(s.PagadoHasta!.Value, ahora);
                }
            }

            s.QuitarGruposDePrepago(ids, ahora);
        }

        await unidad.GuardarAsync(ct);
        return Resultado.Exito(await consulta.EjecutarAsync(ct));
    }
}

/// <summary>SPEC-08, paso 5: el portal del proveedor para la tarjeta y las facturas (solo con tarjeta).</summary>
public sealed class PortalPagosCasoUso(IPasarelaPagos pasarela, AccesoPro acceso, IOptions<OpcionesCuentas> cuentas)
{
    public async Task<Resultado<PortalDto>> EjecutarAsync(CancellationToken ct)
    {
        if (await acceso.SuscripcionAsync(ct) is not { ClienteExterno: { } cliente, EsTarjetaActiva: true })
        {
            return Resultado.Fallo<PortalDto>(ErroresPlanes.SinTarjeta);
        }

        return Resultado.Exito(new PortalDto(await pasarela.PortalAsync(cliente, $"{cuentas.Value.UrlWeb.TrimEnd('/')}/plan", ct)));
    }
}
