using AulaLista.Domain.Comun;

namespace AulaLista.Domain.Planes;

/// <summary>Cómo paga el docente (RN-084): tarjeta cada mes u OXXO por adelantado.</summary>
public static class ModalidadPago
{
    public const string Tarjeta = "tarjeta";
    public const string PrepagoOxxo = "prepago_oxxo";
}

public static class EstadoSuscripcion
{
    /// <summary>Nunca ha pagado, o canceló y ya no tiene grupos Pro.</summary>
    public const string Ninguna = "ninguna";
    public const string Activa = "activa";

    /// <summary>RN-085: un cobro falló o venció el prepago; Pro sigue hasta <see cref="Suscripcion.GraciaHasta"/>.</summary>
    public const string EnGracia = "en_gracia";
    public const string Cancelada = "cancelada";
}

/// <summary>
/// Lo que el docente paga (SPEC-08), una por docente. Su estado solo cambia por eventos verificados del proveedor
/// (RN-087) y por el vencimiento de la gracia. La prueba Pro (RN-081) vive aquí aunque no se pague.
/// </summary>
public sealed class Suscripcion : Entidad
{
    public Suscripcion(Guid docenteId, string proveedor, DateTimeOffset ahora)
        : base(ahora)
    {
        DocenteId = docenteId;
        Proveedor = proveedor;
    }

    private Suscripcion()
    {
    }

    public Guid DocenteId { get; private set; }

    public string Proveedor { get; private set; } = string.Empty;

    public string? Modalidad { get; private set; }

    public string Estado { get; private set; } = EstadoSuscripcion.Ninguna;

    /// <summary>El cliente en el proveedor, para el portal de tarjeta y facturas.</summary>
    public string? ClienteExterno { get; private set; }

    /// <summary>La suscripción con tarjeta en el proveedor.</summary>
    public string? IdExterno { get; private set; }

    /// <summary>Los grupos que el docente paga, en el orden del proveedor.</summary>
    public List<Guid> GruposPro { get; private set; } = [];

    /// <summary>Tarjeta: fin del periodo cobrado; ahí entra el siguiente cobro.</summary>
    public DateTimeOffset? PeriodoActualFin { get; private set; }

    /// <summary>RN-089: hasta cuándo cubre el prepago OXXO.</summary>
    public DateTimeOffset? PagadoHasta { get; private set; }

    public DateTimeOffset? GraciaHasta { get; private set; }

    /// <summary>RN-089: el último aviso de vencimiento enviado (7 o 1 días antes), para no repetirlo.</summary>
    public int? AvisoVencimientoDias { get; private set; }

    public Guid? PruebaGrupoId { get; private set; }

    public Guid? PruebaPeriodoId { get; private set; }

    public DateTimeOffset? PruebaInicio { get; private set; }

    /// <summary>Cuándo se publicaron las boletas del periodo de la prueba (se cerró el periodo).</summary>
    public DateTimeOffset? PruebaPublicadaEn { get; private set; }

    public bool PruebaUsada => PruebaInicio is not null;

    public bool EsTarjetaActiva => Modalidad == ModalidadPago.Tarjeta && IdExterno is not null
        && Estado is EstadoSuscripcion.Activa or EstadoSuscripcion.EnGracia;

    public bool EsPrepagoVigente => Modalidad == ModalidadPago.PrepagoOxxo
        && Estado is EstadoSuscripcion.Activa or EstadoSuscripcion.EnGracia;

    /// <summary>RN-081: una sola vez por docente, en el grupo y el periodo donde inicia su primer cierre.</summary>
    public Resultado IniciarPrueba(Guid grupoId, Guid periodoId, DateTimeOffset ahora)
    {
        if (PruebaUsada)
        {
            return Resultado.Fallo(ErroresPlanes.PruebaUsada);
        }

        PruebaGrupoId = grupoId;
        PruebaPeriodoId = periodoId;
        PruebaInicio = ahora;
        MarcarActualizada(ahora);
        return Resultado.Exito();
    }

    /// <summary>Al cerrarse el periodo de la prueba la cuenta de su fin empieza (RN-081).</summary>
    public void RegistrarPeriodoCerrado(Guid periodoId, DateTimeOffset ahora)
    {
        if (PruebaPeriodoId == periodoId && PruebaPublicadaEn is null)
        {
            PruebaPublicadaEn = ahora;
            MarcarActualizada(ahora);
        }
    }

    public VigenciaPrueba? Prueba(int diasPrueba, int diasMaximo) =>
        PruebaInicio is { } inicio && PruebaGrupoId is { } grupo
            ? new VigenciaPrueba(grupo, inicio, PruebaPublicadaEn, diasPrueba, diasMaximo)
            : null;

    /// <summary>Tarjeta: el proveedor confirmó la suscripción o cambió sus grupos o su periodo.</summary>
    public void AplicarSuscripcion(string? cliente, string idExterno, IReadOnlyList<Guid> grupos, DateTimeOffset? periodoFin, DateTimeOffset ahora)
    {
        Modalidad = ModalidadPago.Tarjeta;
        ClienteExterno = cliente ?? ClienteExterno;
        IdExterno = idExterno;
        GruposPro = [.. grupos];
        PeriodoActualFin = periodoFin ?? PeriodoActualFin;
        if (Estado is EstadoSuscripcion.Ninguna or EstadoSuscripcion.Cancelada)
        {
            Estado = EstadoSuscripcion.Activa;
        }

        PagadoHasta = null;
        AvisoVencimientoDias = null;
        MarcarActualizada(ahora);
    }

    /// <summary>RN-085: el primer cobro fallido abre la gracia; los siguientes no la alargan.</summary>
    public void RegistrarCobroFallido(DateTimeOffset ahora, int diasGracia)
    {
        if (Estado != EstadoSuscripcion.EnGracia)
        {
            Estado = EstadoSuscripcion.EnGracia;
            GraciaHasta = ahora.AddDays(diasGracia);
            MarcarActualizada(ahora);
        }
    }

    public void RegistrarCobroPagado(DateTimeOffset? periodoFin, DateTimeOffset ahora)
    {
        Estado = EstadoSuscripcion.Activa;
        GraciaHasta = null;
        PeriodoActualFin = periodoFin ?? PeriodoActualFin;
        MarcarActualizada(ahora);
    }

    /// <summary>El proveedor terminó la suscripción con tarjeta: no quedan grupos pagados.</summary>
    public void Cancelar(DateTimeOffset ahora)
    {
        Estado = EstadoSuscripcion.Cancelada;
        GruposPro = [];
        IdExterno = null;
        GraciaHasta = null;
        PeriodoActualFin = null;
        MarcarActualizada(ahora);
    }

    /// <summary>
    /// RN-084: un prepago OXXO pagado cubre sus meses desde hoy, o desde el fin del prepago vigente si se renueva
    /// antes de vencer. Un pago para agregar grupos (RN-089) no mueve la fecha.
    /// </summary>
    public DateTimeOffset AplicarPrepago(IReadOnlyList<Guid> grupos, int meses, bool agregar, DateTimeOffset ahora)
    {
        if (agregar && EsPrepagoVigente && PagadoHasta is { } hasta)
        {
            GruposPro = [.. GruposPro, .. grupos.Where(g => !GruposPro.Contains(g))];
            MarcarActualizada(ahora);
            return hasta;
        }

        var desde = EsPrepagoVigente && PagadoHasta is { } vigente && vigente > ahora ? vigente : ahora;
        Modalidad = ModalidadPago.PrepagoOxxo;
        GruposPro = [.. grupos];
        PagadoHasta = desde.AddMonths(meses);
        Estado = EstadoSuscripcion.Activa;
        GraciaHasta = null;
        AvisoVencimientoDias = null;
        IdExterno = null;
        PeriodoActualFin = null;
        MarcarActualizada(ahora);
        return PagadoHasta.Value;
    }

    /// <summary>RN-089: quitar grupos de un prepago no se reembolsa; dejan de contar para la renovación.</summary>
    public void QuitarGruposDePrepago(IReadOnlyList<Guid> quedan, DateTimeOffset ahora)
    {
        GruposPro = [.. GruposPro.Where(quedan.Contains)];
        MarcarActualizada(ahora);
    }

    /// <summary>RN-089 y RN-085: al vencer el prepago empieza la gracia.</summary>
    public bool VencerPrepago(DateTimeOffset ahora, int diasGracia)
    {
        if (Modalidad != ModalidadPago.PrepagoOxxo || Estado != EstadoSuscripcion.Activa || PagadoHasta is not { } hasta || ahora < hasta)
        {
            return false;
        }

        Estado = EstadoSuscripcion.EnGracia;
        GraciaHasta = hasta.AddDays(diasGracia);
        MarcarActualizada(ahora);
        return true;
    }

    /// <summary>RN-085: terminó la gracia sin pago. Los grupos pasan a Gratis; los datos no se tocan.</summary>
    public bool TerminarGracia(DateTimeOffset ahora)
    {
        if (Estado != EstadoSuscripcion.EnGracia || GraciaHasta is not { } fin || ahora < fin)
        {
            return false;
        }

        Estado = EstadoSuscripcion.Cancelada;
        GruposPro = [];
        GraciaHasta = null;
        MarcarActualizada(ahora);
        return true;
    }

    /// <summary>RN-089: el aviso de 7 o de 1 día que toca hoy, si no se ha enviado.</summary>
    public int? AvisoVencimientoPendiente(DateTimeOffset ahora, IReadOnlyList<int> diasAviso)
    {
        if (Modalidad != ModalidadPago.PrepagoOxxo || Estado != EstadoSuscripcion.Activa || PagadoHasta is not { } hasta || ahora >= hasta)
        {
            return null;
        }

        var faltan = (hasta - ahora).TotalDays;
        var toca = diasAviso.Where(d => faltan <= d).Order().Cast<int?>().FirstOrDefault();
        return toca is { } dias && (AvisoVencimientoDias is null || dias < AvisoVencimientoDias) ? dias : null;
    }

    public void MarcarAvisoVencimiento(int dias, DateTimeOffset ahora)
    {
        AvisoVencimientoDias = dias;
        MarcarActualizada(ahora);
    }
}

/// <summary>
/// RN-081: la prueba dura <c>diasPrueba</c> desde que inicia el cierre o hasta publicar las boletas de ese periodo, lo
/// que ocurra después, y nunca más de <c>diasMaximo</c>.
/// </summary>
public sealed record VigenciaPrueba(Guid GrupoId, DateTimeOffset Inicio, DateTimeOffset? PublicadaEn, int DiasPrueba, int DiasMaximo)
{
    public DateTimeOffset FinMinimo => Inicio.AddDays(DiasPrueba);

    public DateTimeOffset FinMaximo => Inicio.AddDays(DiasMaximo);

    /// <summary>Sin boletas publicadas, la prueba sigue hasta el máximo.</summary>
    public DateTimeOffset Fin
    {
        get
        {
            if (PublicadaEn is not { } publicada)
            {
                return FinMaximo;
            }

            var fin = publicada > FinMinimo ? publicada : FinMinimo;
            return fin > FinMaximo ? FinMaximo : fin;
        }
    }

    public bool Vigente(DateTimeOffset ahora) => ahora < Fin;

    /// <summary>Pasaron los días de prueba y sigue porque las boletas no se han publicado.</summary>
    public bool TerminaAlPublicar(DateTimeOffset ahora) => PublicadaEn is null && ahora >= FinMinimo && Vigente(ahora);

    /// <summary>Lo que se le dice al docente: días hasta el fin mínimo, o hasta el máximo si ya sigue por las boletas.</summary>
    public int DiasRestantes(DateTimeOffset ahora)
    {
        if (!Vigente(ahora))
        {
            return 0;
        }

        var hasta = PublicadaEn is null && ahora < FinMinimo ? FinMinimo : Fin;
        return (int)Math.Ceiling((hasta - ahora).TotalDays);
    }
}

/// <summary>RN-082: el primer grupo a un precio y cada adicional a otro, en un solo cobro.</summary>
public static class Tarifa
{
    public static decimal Mensual(int grupos, decimal primerGrupo, decimal adicional) =>
        grupos <= 0 ? 0m : primerGrupo + (adicional * (grupos - 1));
}

public static class ErroresPlanes
{
    public static readonly ErrorNegocio PruebaUsada = new("prueba_usada", "Ya usaste tu prueba Pro. Activa Pro en este grupo para cerrar con observaciones.");
    public static readonly ErrorNegocio SinGrupos = new("sin_grupos_pro", "Elige al menos un grupo para Pro.");
    public static readonly ErrorNegocio GrupoNoDisponible = new("grupo_no_disponible", "Uno de los grupos ya no existe o está archivado.");
    public static readonly ErrorNegocio ModalidadInvalida = new("modalidad_invalida", "Elige tarjeta, u OXXO por 3 o 6 meses.");
    public static readonly ErrorNegocio OxxoNoDisponible = new("oxxo_no_disponible", "El pago en OXXO no está disponible por ahora. Paga con tarjeta.");
    public static readonly ErrorNegocio MontoFueraDeRango = new("monto_fuera_de_rango", "El pago debe ser de 10 a 10,000 MXN. Cambia los grupos o los meses.");
    public static readonly ErrorNegocio YaTieneTarjeta = new("ya_tiene_tarjeta", "Ya pagas con tarjeta. Cambia tus grupos Pro sin volver a pagar.");
    public static readonly ErrorNegocio SinTarjeta = new("sin_tarjeta", "Para cambiar los grupos Pro así, primero paga con tarjeta.");
    public static readonly ErrorNegocio SinCambios = new("sin_cambios_plan", "Esos ya son tus grupos Pro.");
    public static readonly ErrorNegocio PrepagoVigente = new("prepago_vigente", "Tu prepago OXXO sigue vigente. Agrega grupos o cambia a tarjeta cuando venza.");
    public static readonly ErrorNegocio WhatsappRequierePro = new("whatsapp_requiere_pro", "Los avisos urgentes por WhatsApp son de Pro. Envíalo sin urgente o activa Pro en este grupo.");
    public static readonly ErrorNegocio BoletasRequierenPro = new("boletas_requieren_pro", "Las boletas son del plan Pro. Activa Pro en este grupo para generarlas y publicarlas.");
    public static readonly ErrorNegocio AgregarRequierePago = new("agregar_requiere_pago", "Para agregar grupos a tu prepago, paga esos grupos hasta el fin de tu prepago.");
    public static readonly ErrorNegocio PagoSimuladoNoDisponible = new("pago_simulado_no_disponible", "El pago de prueba solo existe sin Stripe.");
    public static readonly ErrorNegocio PagoSimuladoTerminado = new("pago_simulado_terminado", "Este pago de prueba ya terminó.");

    public static readonly IReadOnlyList<ErrorNegocio> Todos =
    [
        PruebaUsada, SinGrupos, GrupoNoDisponible, ModalidadInvalida, OxxoNoDisponible, MontoFueraDeRango, YaTieneTarjeta,
        SinTarjeta, SinCambios, PrepagoVigente, WhatsappRequierePro, BoletasRequierenPro, AgregarRequierePago,
        PagoSimuladoNoDisponible, PagoSimuladoTerminado,
    ];
}
