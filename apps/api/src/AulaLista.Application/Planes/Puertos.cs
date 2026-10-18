using System.ComponentModel.DataAnnotations;
using AulaLista.Domain.Planes;

namespace AulaLista.Application.Planes;

/// <summary>Precios, prueba y gracia de SPEC-08. Viven en configuración (y en el catálogo de Stripe), nunca en el código.</summary>
public sealed class OpcionesPlanes
{
    public const string Seccion = "Planes";

    /// <summary>RN-082: precio mensual del primer grupo Pro, en MXN con IVA.</summary>
    [Range(typeof(decimal), "1", "10000")]
    public decimal PrecioPrimerGrupo { get; init; } = 79m;

    [Range(typeof(decimal), "1", "10000")]
    public decimal PrecioGrupoAdicional { get; init; } = 29m;

    [Required]
    public string Moneda { get; init; } = "MXN";

    /// <summary>RN-081.</summary>
    [Range(1, 90)]
    public int DiasPrueba { get; init; } = 14;

    [Range(1, 90)]
    public int DiasMaximoPrueba { get; init; } = 30;

    /// <summary>RN-085.</summary>
    [Range(0, 60)]
    public int DiasGracia { get; init; } = 7;

    /// <summary>RN-084: meses que se pueden pagar por adelantado en OXXO (3 y 6). Sin valor por defecto: el enlace de
    /// configuración agrega a un arreglo, no lo reemplaza.</summary>
    [MinLength(1)]
    public int[] MesesPrepago { get; init; } = [];

    /// <summary>RN-089: días antes del fin del prepago en que se avisa (7 y 1).</summary>
    [MinLength(1)]
    public int[] DiasAvisoPrepago { get; init; } = [];

    /// <summary>Días que vive la ficha OXXO (Stripe acepta de 1 a 7).</summary>
    [Range(1, 7)]
    public int DiasFichaOxxo { get; init; } = 3;

    /// <summary>Montos que Stripe acepta en OXXO.</summary>
    [Range(typeof(decimal), "1", "100000")]
    public decimal MontoMinimoOxxo { get; init; } = 10m;

    [Range(typeof(decimal), "1", "100000")]
    public decimal MontoMaximoOxxo { get; init; } = 10_000m;

    /// <summary>Se apaga si Stripe no aprueba OXXO para el giro de la cuenta (ADR-0009).</summary>
    public bool OxxoHabilitado { get; init; } = true;

    /// <summary>Los días de un mes al prorratear un grupo agregado a un prepago.</summary>
    [Range(28, 31)]
    public int DiasPorMes { get; init; } = 30;

    public decimal Mensual(int grupos) => Tarifa.Mensual(grupos, PrecioPrimerGrupo, PrecioGrupoAdicional);
}

/// <summary>
/// Stripe (ADR-0009). Claves solo por variables de entorno; sin ellas corre la pasarela simulada, que confirma los
/// pagos desde una página de prueba de la app con los mismos eventos que mandaría Stripe.
/// </summary>
public sealed class OpcionesPagos
{
    public const string Seccion = "Pagos";
    public const string ProveedorSimulado = "simulado";
    public const string ProveedorStripe = "stripe";

    [Required]
    [AllowedValues(ProveedorSimulado, ProveedorStripe)]
    public string Proveedor { get; init; } = ProveedorSimulado;

    /// <summary>Firma de los webhooks (Stripe: <c>whsec_…</c>). Sin él, todo webhook se rechaza.</summary>
    public string? SecretoWebhook { get; init; }

    /// <summary><c>sk_…</c> de Stripe.</summary>
    public string? ClaveSecreta { get; init; }

    /// <summary>Precio recurrente del primer grupo en el catálogo de Stripe (<c>price_…</c>).</summary>
    public string? PrecioPrimerGrupoId { get; init; }

    public string? PrecioGrupoAdicionalId { get; init; }

    /// <summary>Un evento con más antigüedad que esto se rechaza aunque la firma sea buena (repetición).</summary>
    [Range(1, 3600)]
    public int ToleranciaFirmaSegundos { get; init; } = 300;
}

/// <summary>
/// La página de pago del proveedor (o la simulada) a la que se manda al docente. <see cref="UrlFicha"/>: dónde volver a
/// ver la ficha OXXO mientras no se paga, si el proveedor la conserva.
/// </summary>
public sealed record SesionPago(string Id, string Url, DateTimeOffset? VenceEn, string? UrlFicha = null);

/// <summary>Suscripción mensual con tarjeta por los grupos elegidos (RN-082).</summary>
public sealed record NuevaSuscripcion(Guid DocenteId, string Correo, string? Cliente, IReadOnlyList<Guid> Grupos, string UrlExito, string UrlCancelar);

/// <summary>Pago único: prepago OXXO (RN-084) o grupos agregados a un prepago (RN-089).</summary>
public sealed record PagoUnico(
    Guid DocenteId, string Correo, string? Cliente, string Tipo, IReadOnlyList<Guid> Grupos, int Meses, decimal Monto,
    string Concepto, bool SoloOxxo, int DiasFicha, string UrlExito, string UrlCancelar);

/// <summary>Tipos de evento que la app entiende, ya traducidos del proveedor.</summary>
public static class TipoEventoPago
{
    /// <summary>La suscripción con tarjeta se creó o cambió (grupos, periodo o estado).</summary>
    public const string SuscripcionActualizada = "suscripcion_actualizada";
    public const string SuscripcionCancelada = "suscripcion_cancelada";
    public const string CobroFallido = "cobro_fallido";
    public const string CobroPagado = "cobro_pagado";

    /// <summary>Se creó la ficha OXXO (aún sin pagar).</summary>
    public const string PagoUnicoPendiente = "pago_unico_pendiente";
    public const string PagoUnicoPagado = "pago_unico_pagado";

    /// <summary>La ficha OXXO venció o el pago falló: no cambia nada salvo el historial.</summary>
    public const string PagoUnicoFallido = "pago_unico_fallido";
}

/// <summary>Un evento verificado del proveedor (RN-087). Solo IDs; nada de tarjetas ni datos de alumnos.</summary>
public sealed record EventoPagoRecibido(
    string Id,
    string Tipo,
    Guid DocenteId,
    string? Cliente = null,
    string? Suscripcion = null,
    IReadOnlyList<Guid>? Grupos = null,
    DateTimeOffset? PeriodoFin = null,
    bool? Activa = null,
    string? Sesion = null,
    string? TipoPago = null,
    int? Meses = null,
    DateTimeOffset? VenceEn = null);

/// <summary>Stripe o la pasarela simulada (02-arquitectura, Integraciones). La app nunca ve datos de tarjeta.</summary>
public interface IPasarelaPagos
{
    string Proveedor { get; }

    Task<SesionPago> CheckoutSuscripcionAsync(NuevaSuscripcion datos, CancellationToken cancellationToken);

    Task<SesionPago> CheckoutPagoUnicoAsync(PagoUnico datos, CancellationToken cancellationToken);

    /// <summary>
    /// RN-083: cambia los grupos de la suscripción. Al agregar se prorratea; al quitar no hay reembolso y el grupo sigue
    /// hasta el corte. El cambio llega a la base por el evento del proveedor, no por esta llamada.
    /// </summary>
    Task ActualizarGruposAsync(Guid docenteId, string suscripcion, IReadOnlyList<Guid> grupos, bool prorratear, CancellationToken cancellationToken);

    /// <summary>Portal de clientes para la tarjeta y las facturas (solo tarjeta).</summary>
    Task<string> PortalAsync(string cliente, string urlRegreso, CancellationToken cancellationToken);

    /// <summary>Verifica la firma y traduce el evento; nulo si la firma no es válida.</summary>
    EventoLeido? LeerEvento(byte[] cuerpo, string? firma);
}

/// <summary>Resultado de leer un webhook: el evento traducido, o nulo si es un evento que no nos interesa.</summary>
public sealed record EventoLeido(EventoPagoRecibido? Evento);

/// <summary>Suscripción, pagos y eventos del docente, con RLS por docente.</summary>
public interface IRepositorioPlanes
{
    Task<Suscripcion?> SuscripcionAsync(CancellationToken cancellationToken);

    void Agregar(Suscripcion suscripcion);

    Task<IReadOnlyList<Pago>> PagosAsync(CancellationToken cancellationToken);

    Task<Pago?> PagoPorSesionAsync(string sesion, CancellationToken cancellationToken);

    void Agregar(Pago pago);

    Task<bool> EventoAplicadoAsync(string idExterno, CancellationToken cancellationToken);

    void Agregar(EventoPago evento);

    /// <summary>
    /// Para la revisión diaria, sin docente: los que tienen suscripción activa o en gracia, prueba en curso o algún
    /// grupo con fin de Pro, por la función <c>docentes_con_plan</c>.
    /// </summary>
    Task<IReadOnlyList<Guid>> DocentesConPlanAsync(CancellationToken cancellationToken);
}
