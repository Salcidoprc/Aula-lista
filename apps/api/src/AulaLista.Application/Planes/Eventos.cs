using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AulaLista.Application.Comun;
using AulaLista.Application.Cuentas;
using AulaLista.Application.Grupos;
using AulaLista.Domain.Grupos;
using AulaLista.Domain.Planes;
using Microsoft.Extensions.Options;

namespace AulaLista.Application.Planes;

/// <summary>
/// <c>Stripe-Signature: t=&lt;segundos&gt;,v1=&lt;hex&gt;</c>: HMAC-SHA256 de <c>"t.cuerpo"</c> con el secreto del webhook.
/// La pasarela simulada firma igual, para que las pruebas recorran el mismo camino.
/// </summary>
public static class FirmaPagos
{
    public const string Encabezado = "Stripe-Signature";

    public static string Firmar(byte[] cuerpo, string secreto, DateTimeOffset ahora)
    {
        var t = ahora.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        return $"t={t},v1={Convert.ToHexStringLower(Calcular(t, cuerpo, secreto))}";
    }

    public static bool Valida(byte[] cuerpo, string? encabezado, string? secreto, DateTimeOffset ahora, int toleranciaSegundos)
    {
        if (string.IsNullOrEmpty(secreto) || string.IsNullOrEmpty(encabezado))
        {
            return false;
        }

        string? t = null;
        var firmas = new List<byte[]>();
        foreach (var parte in encabezado.Split(','))
        {
            var (clave, valor) = parte.Split('=', 2) is [var k, var v] ? (k.Trim(), v.Trim()) : (string.Empty, string.Empty);
            if (clave == "t")
            {
                t = valor;
            }
            else if (clave == "v1")
            {
                try
                {
                    firmas.Add(Convert.FromHexString(valor));
                }
                catch (FormatException)
                {
                    // Una firma mal formada no cuenta; puede haber otra válida.
                }
            }
        }

        if (t is null || !long.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var segundos)
            || Math.Abs(ahora.ToUnixTimeSeconds() - segundos) > toleranciaSegundos)
        {
            return false;
        }

        var esperada = Calcular(t, cuerpo, secreto);
        return firmas.Any(f => CryptographicOperations.FixedTimeEquals(f, esperada));
    }

    private static byte[] Calcular(string t, byte[] cuerpo, string secreto)
    {
        var prefijo = Encoding.UTF8.GetBytes($"{t}.");
        byte[] mensaje = [.. prefijo, .. cuerpo];
        return HMACSHA256.HashData(Encoding.UTF8.GetBytes(secreto), mensaje);
    }
}

/// <summary>
/// RN-087: aplica un evento verificado del proveedor una sola vez (su id queda en <c>evento_pago</c>). Corre como el
/// docente del evento, dentro de la transacción de quien lo llama.
/// </summary>
public sealed class ProcesadorEventosPago(
    IRepositorioPlanes planes, IRepositorioGrupos grupos, AccesoPro acceso, TimeProvider reloj)
{
    /// <summary>Falso si el evento ya se había aplicado.</summary>
    public async Task<bool> AplicarAsync(EventoPagoRecibido evento, CancellationToken ct)
    {
        if (await planes.EventoAplicadoAsync(evento.Id, ct))
        {
            return false;
        }

        var ahora = reloj.GetUtcNow();
        var opciones = acceso.Opciones;
        var s = await acceso.SuscripcionOCrearAsync(evento.DocenteId, ct);
        switch (evento.Tipo)
        {
            case TipoEventoPago.SuscripcionActualizada when evento.Suscripcion is { } id:
                var antes = s.GruposPro.ToList();
                s.AplicarSuscripcion(evento.Cliente, id, evento.Grupos ?? [], evento.PeriodoFin, ahora);
                foreach (var grupo in await GruposAsync(s.GruposPro, ct))
                {
                    grupo.ActivarPro(s.Estado == EstadoSuscripcion.EnGracia ? s.GraciaHasta : null, ahora);
                }

                // RN-083: el grupo quitado sigue en Pro hasta el corte, sin reembolso.
                foreach (var grupo in await GruposAsync(antes.Except(s.GruposPro), ct))
                {
                    grupo.TerminarProEn(s.PeriodoActualFin ?? ahora, ahora);
                }

                break;

            case TipoEventoPago.SuscripcionCancelada:
                var cancelados = s.GruposPro.ToList();
                s.Cancelar(ahora);
                foreach (var grupo in await GruposAsync(cancelados, ct))
                {
                    grupo.PasarAGratis(ahora);
                }

                break;

            case TipoEventoPago.CobroFallido when s.EsTarjetaActiva:
                s.RegistrarCobroFallido(ahora, opciones.DiasGracia);
                foreach (var grupo in await GruposAsync(s.GruposPro, ct))
                {
                    grupo.TerminarProEn(s.GraciaHasta!.Value, ahora);
                }

                break;

            case TipoEventoPago.CobroPagado when s.EsTarjetaActiva:
                s.RegistrarCobroPagado(evento.PeriodoFin, ahora);
                foreach (var grupo in await GruposAsync(s.GruposPro, ct))
                {
                    grupo.ActivarPro(null, ahora);
                }

                break;

            case TipoEventoPago.PagoUnicoPendiente when evento.Sesion is { } sesion:
                (await planes.PagoPorSesionAsync(sesion, ct))?.FijarVencimiento(evento.VenceEn, ahora);
                break;

            case TipoEventoPago.PagoUnicoPagado when evento.Sesion is { } sesion:
                if (await planes.PagoPorSesionAsync(sesion, ct) is { } pago && pago.MarcarPagado(ahora))
                {
                    var previos = s.GruposPro.ToList();
                    var hastaAnterior = s.PagadoHasta;
                    var hasta = s.AplicarPrepago(pago.Grupos, pago.Meses, pago.Tipo == TipoPago.AgregarGrupos, ahora);

                    // RN-089 y RN-085: el prepago cubre hasta su fin más la gracia; al vencer empieza la gracia.
                    foreach (var grupo in await GruposAsync(s.GruposPro, ct))
                    {
                        grupo.ActivarPro(hasta.AddDays(opciones.DiasGracia), ahora);
                    }

                    foreach (var grupo in await GruposAsync(previos.Except(s.GruposPro), ct))
                    {
                        grupo.TerminarProEn(hastaAnterior ?? ahora, ahora);
                    }
                }

                break;

            case TipoEventoPago.PagoUnicoFallido when evento.Sesion is { } sesion:
                // RN-084: la ficha vencida o el pago fallido no cambian ningún grupo.
                (await planes.PagoPorSesionAsync(sesion, ct))?.MarcarVencido(ahora);
                break;
        }

        planes.Agregar(new EventoPago(evento.DocenteId, evento.Id, evento.Tipo, ahora));
        return true;
    }

    private async Task<List<Grupo>> GruposAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var lista = new List<Grupo>();
        foreach (var id in ids.Distinct())
        {
            if (await grupos.GrupoAsync(id, ct) is { } grupo)
            {
                lista.Add(grupo);
            }
        }

        return lista;
    }
}

/// <summary>
/// <c>POST /webhooks/pagos</c>: sin firma válida no se toca nada (nulo, 401). Un evento que no nos interesa se
/// contesta 200 para que el proveedor no lo repita.
/// </summary>
public sealed class WebhookPagosCasoUso(
    IPasarelaPagos pasarela, ProcesadorEventosPago procesador, IRepositorioDocentes docentes, IContextoUsuario usuario, IUnidadDeTrabajo unidad)
{
    public async Task<bool?> EjecutarAsync(byte[] cuerpo, string? firma, CancellationToken ct)
    {
        if (pasarela.LeerEvento(cuerpo, firma) is not { } leido)
        {
            return null;
        }

        if (leido.Evento is not { } evento)
        {
            return false;
        }

        usuario.AsumirDocente(evento.DocenteId);
        // Una cuenta ya borrada no tiene a quién aplicarle el evento.
        return await unidad.EnTransaccionAsync(
            async () => await docentes.PorIdAsync(evento.DocenteId, ct) is not null && await procesador.AplicarAsync(evento, ct), ct);
    }
}

/// <summary>RN-085, RN-089: el trabajo diario lista a los docentes con algo de plan que revisar.</summary>
public sealed class RevisarSuscripcionesTrabajo(IRepositorioPlanes planes)
{
    public Task<IReadOnlyList<Guid>> DocentesAsync(CancellationToken ct) => planes.DocentesConPlanAsync(ct);
}

/// <summary>
/// RN-085, RN-089, por docente: vence el prepago (empieza la gracia), termina la gracia (grupos a Gratis sin borrar
/// nada), avisa por correo 7 y 1 días antes del fin del prepago y pasa a Gratis los grupos cuyo Pro ya terminó.
/// La banda de la app sale del estado, así que se ve cada día de la gracia.
/// </summary>
public sealed class RevisarSuscripcionDocenteTrabajo(
    IRepositorioGrupos grupos,
    IRepositorioCuentas cuentas,
    AccesoPro acceso,
    IEnvioCorreo correo,
    IUnidadDeTrabajo unidad,
    IOptions<OpcionesCuentas> opcionesCuentas,
    TimeProvider reloj)
{
    public async Task EjecutarAsync(Guid docenteId, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var opciones = acceso.Opciones;
        var web = opcionesCuentas.Value.UrlWeb;
        var correos = new List<CorreoSaliente>();
        var para = (await cuentas.PorIdAsync(docenteId, ct))?.Correo;

        if (await acceso.SuscripcionAsync(ct) is { } s)
        {
            if (s.VencerPrepago(ahora, opciones.DiasGracia) && para is not null)
            {
                correos.Add(CorreosPlanes.PrepagoVencido(para, web, s.GraciaHasta!.Value));
            }

            if (s.AvisoVencimientoPendiente(ahora, opciones.DiasAvisoPrepago) is { } dias)
            {
                s.MarcarAvisoVencimiento(dias, ahora);
                if (para is not null)
                {
                    correos.Add(CorreosPlanes.PrepagoPorVencer(para, web, s.PagadoHasta!.Value, dias));
                }
            }

            s.TerminarGracia(ahora);
        }

        foreach (var g in await grupos.GruposAsync(archivados: true, ct))
        {
            if (g.Grupo.Plan == PlanGrupo.Pro && g.Grupo.ProHasta is { } fin && fin <= ahora)
            {
                g.Grupo.PasarAGratis(ahora);
            }
        }

        await unidad.GuardarAsync(ct);
        foreach (var c in correos)
        {
            await correo.EnviarAsync(c, ct);
        }
    }
}

/// <summary>Correos de pago al docente (RN-089, RN-085).</summary>
internal static class CorreosPlanes
{
    private static readonly CultureInfo Mexico = CultureInfo.GetCultureInfo("es-MX");

    public static CorreoSaliente PrepagoPorVencer(string para, string urlWeb, DateTimeOffset hasta, int dias) => CorreosCuentas.Armar(
        para,
        dias <= 1 ? "Tu prepago de Aula Lista vence mañana" : $"Tu prepago de Aula Lista vence en {dias} días",
        $"Tus grupos Pro están pagados hasta el {Fecha(hasta)}. Renueva en OXXO o pasa a tarjeta para no perder el cierre con observaciones ni las boletas.",
        "Renovar mi plan",
        $"{urlWeb.TrimEnd('/')}/plan",
        "Si no renuevas, tienes 7 días de gracia. Después tus grupos pasan a Gratis sin borrar nada.");

    public static CorreoSaliente PrepagoVencido(string para, string urlWeb, DateTimeOffset graciaHasta) => CorreosCuentas.Armar(
        para,
        "Tu prepago de Aula Lista venció",
        $"Tus grupos siguen en Pro hasta el {Fecha(graciaHasta)}. Renueva para no perder el cierre con observaciones ni las boletas.",
        "Renovar mi plan",
        $"{urlWeb.TrimEnd('/')}/plan",
        "Después de esa fecha tus grupos pasan a Gratis. No se borra nada y los tutores siguen viendo lo publicado.");

    private static string Fecha(DateTimeOffset fecha) =>
        TimeZoneInfo.ConvertTime(fecha, Zona()).ToString("d 'de' MMMM 'de' yyyy", Mexico);

    private static TimeZoneInfo Zona() =>
        TimeZoneInfo.TryFindSystemTimeZoneById(Domain.Cuentas.Docente.ZonaHorariaPorDefecto, out var zona) ? zona : TimeZoneInfo.Utc;
}
