using AulaLista.Domain.Comun;

namespace AulaLista.Domain.Planes;

public static class TipoPago
{
    /// <summary>RN-084: 3 o 6 meses del total mensual.</summary>
    public const string Prepago = "prepago";

    /// <summary>RN-089: un grupo Pro más durante un prepago, hasta <see cref="Suscripcion.PagadoHasta"/>.</summary>
    public const string AgregarGrupos = "agregar_grupos";
}

public static class EstadoPago
{
    public const string Pendiente = "pendiente";
    public const string Pagado = "pagado";

    /// <summary>La ficha OXXO venció o el pago falló: no cambia ningún grupo.</summary>
    public const string Vencido = "vencido";
}

/// <summary>Un pago único en Checkout (OXXO o tarjeta): el historial que el docente ve sin portal (SPEC-08, paso 5).</summary>
public sealed class Pago : Entidad
{
    public Pago(
        Guid docenteId, string tipo, int meses, decimal monto, IReadOnlyList<Guid> grupos, string sesionExterna,
        string? urlFicha, DateTimeOffset? venceEn, DateTimeOffset ahora)
        : base(ahora)
    {
        DocenteId = docenteId;
        Tipo = tipo;
        Meses = meses;
        Monto = monto;
        Grupos = [.. grupos];
        SesionExterna = sesionExterna;
        UrlFicha = urlFicha;
        VenceEn = venceEn;
    }

    private Pago()
    {
    }

    public Guid DocenteId { get; private set; }

    public string Tipo { get; private set; } = TipoPago.Prepago;

    public int Meses { get; private set; }

    /// <summary>En MXN con IVA incluido.</summary>
    public decimal Monto { get; private set; }

    public List<Guid> Grupos { get; private set; } = [];

    public string SesionExterna { get; private set; } = string.Empty;

    /// <summary>Dónde ver la ficha OXXO mientras está pendiente, si el proveedor la da.</summary>
    public string? UrlFicha { get; private set; }

    public string Estado { get; private set; } = EstadoPago.Pendiente;

    /// <summary>Cuándo vence la ficha OXXO, si el proveedor lo dice.</summary>
    public DateTimeOffset? VenceEn { get; private set; }

    public DateTimeOffset? PagadoEn { get; private set; }

    public bool MarcarPagado(DateTimeOffset ahora)
    {
        if (Estado == EstadoPago.Pagado)
        {
            return false;
        }

        Estado = EstadoPago.Pagado;
        PagadoEn = ahora;
        MarcarActualizada(ahora);
        return true;
    }

    public void MarcarVencido(DateTimeOffset ahora)
    {
        if (Estado == EstadoPago.Pendiente)
        {
            Estado = EstadoPago.Vencido;
            MarcarActualizada(ahora);
        }
    }

    public void FijarVencimiento(DateTimeOffset? venceEn, DateTimeOffset ahora)
    {
        if (venceEn is not null && Estado == EstadoPago.Pendiente)
        {
            VenceEn = venceEn;
            MarcarActualizada(ahora);
        }
    }
}

/// <summary>RN-087: cada evento del proveedor se aplica una sola vez; su id queda aquí.</summary>
public sealed class EventoPago : Entidad
{
    public EventoPago(Guid docenteId, string idExterno, string tipo, DateTimeOffset ahora)
        : base(ahora)
    {
        DocenteId = docenteId;
        IdExterno = idExterno;
        Tipo = tipo;
    }

    private EventoPago()
    {
    }

    public Guid DocenteId { get; private set; }

    public string IdExterno { get; private set; } = string.Empty;

    public string Tipo { get; private set; } = string.Empty;
}
