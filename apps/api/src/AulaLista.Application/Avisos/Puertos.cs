using System.ComponentModel.DataAnnotations;
using AulaLista.Domain.Avisos;

namespace AulaLista.Application.Avisos;

/// <summary>
/// WhatsApp del sistema (Cloud API de Meta, ADR-0005). Credenciales solo por variables de entorno; costo y límites
/// de configuración (reglas/01). Mientras no haya credenciales corre el proveedor simulado.
/// </summary>
public sealed class OpcionesWhatsapp
{
    public const string Seccion = "Whatsapp";
    public const string ProveedorSimulado = "simulado";
    public const string ProveedorMeta = "meta";

    [Required]
    [AllowedValues(ProveedorSimulado, ProveedorMeta)]
    public string Proveedor { get; init; } = ProveedorSimulado;

    public string? Token { get; init; }

    /// <summary>Id del número de teléfono del sistema en Meta.</summary>
    public string? IdNumero { get; init; }

    [Required]
    public string VersionApi { get; init; } = "v23.0";

    [Required]
    public string Idioma { get; init; } = "es_MX";

    /// <summary>Firma los webhooks (<c>X-Hub-Signature-256</c>). Sin él, todo webhook se rechaza.</summary>
    public string? SecretoApp { get; init; }

    /// <summary>Lo que Meta manda al registrar el webhook (<c>hub.verify_token</c>).</summary>
    public string? TokenVerificacion { get; init; }

    /// <summary>Costo estimado por mensaje de utilidad, en MXN, para vigilar RNF-022.</summary>
    [Range(typeof(decimal), "0", "100")]
    public decimal CostoPorMensaje { get; init; } = 0.16m;

    /// <summary>RN-086: mensajes de boleta por tutor por periodo publicado.</summary>
    [Range(0, 10)]
    public int BoletasPorTutorPorPeriodo { get; init; } = 1;

    /// <summary>RN-086: avisos urgentes por WhatsApp por tutor, por grupo y mes.</summary>
    [Range(0, 100)]
    public int UrgentesPorTutorPorMes { get; init; } = 4;

    /// <summary>El mes del límite se cuenta en esta zona.</summary>
    [Required]
    public string ZonaHoraria { get; init; } = "America/Mexico_City";
}

/// <summary>Web Push con VAPID (RF-064). La clave privada solo por variable de entorno; sin claves corre simulado.</summary>
public sealed class OpcionesPush
{
    public const string Seccion = "Push";
    public const string ProveedorSimulado = "simulado";
    public const string ProveedorVapid = "vapid";

    [Required]
    [AllowedValues(ProveedorSimulado, ProveedorVapid)]
    public string Proveedor { get; init; } = ProveedorSimulado;

    public string? ClavePublica { get; init; }

    public string? ClavePrivada { get; init; }

    /// <summary>Contacto del remitente que piden los servicios de push (<c>mailto:</c> o URL).</summary>
    [Required]
    public string Sujeto { get; init; } = "mailto:soporte@aulalista.mx";

    /// <summary>Dominios de los servicios de push de los navegadores; una suscripción a otro lado se rechaza.</summary>
    [MinLength(1)]
    public string[] ServiciosPermitidos { get; init; } =
        ["fcm.googleapis.com", "push.services.mozilla.com", "notify.windows.com", "push.apple.com"];
}

/// <summary>Un aviso con cuántos tutores lo recibieron y cuántos lo leyeron (RF-073).</summary>
public sealed record AvisoConLecturas(Aviso Aviso, int Destinatarios, int Leidos);

/// <summary>Lo que el tutor registró por su cuenta: su correo del portal y si tiene push activo.</summary>
public sealed record ContactoPropio(string? Correo, bool Push);

/// <summary>
/// Avisos, entregas y mensajes de WhatsApp del docente, con RLS por docente. Los datos del tutor (su correo propio y
/// sus suscripciones) se leen por su vínculo con el docente.
/// </summary>
public interface IRepositorioAvisos
{
    Task<IReadOnlyList<AvisoConLecturas>> AvisosAsync(Guid grupoId, CancellationToken cancellationToken);

    Task<Aviso?> AvisoAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<EntregaAviso>> EntregasAsync(Guid avisoId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, ContactoPropio>> ContactosAsync(IReadOnlyCollection<Guid> tutorIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<SuscripcionPush>> SuscripcionesAsync(IReadOnlyCollection<Guid> tutorIds, CancellationToken cancellationToken);

    /// <summary>Mensajes del grupo con esa plantilla desde una fecha (o de una referencia), por tutor, para RN-086.</summary>
    Task<IReadOnlyDictionary<Guid, int>> MensajesPorTutorAsync(
        Guid grupoId, string plantilla, DateTimeOffset? desde, Guid? referenciaId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MensajeWhatsapp>> MensajesAsync(Guid referenciaId, string plantilla, CancellationToken cancellationToken);

    void Agregar(Aviso aviso);

    void Agregar(IEnumerable<EntregaAviso> entregas);

    void Agregar(MensajeWhatsapp mensaje);

    void Borrar(SuscripcionPush suscripcion);

    /// <summary>
    /// Webhook de Meta, sin docente: cambia el estado de un mensaje por su id de Meta con la función
    /// <c>actualizar_mensaje_whatsapp</c>, solo hacia adelante. Devuelve si cambió.
    /// </summary>
    Task<bool> ActualizarEstadoWhatsappAsync(string idMeta, string estado, CancellationToken cancellationToken);
}

/// <summary>Un aviso visto por el tutor, con el grupo del que vino.</summary>
public sealed record AvisoDelTutor(Aviso Aviso, EntregaAviso Entrega, string? Docente);

/// <summary>Lo del tutor en el portal, con <c>app.tutor_id</c>: sus avisos, su correo y sus navegadores con push.</summary>
public interface IAvisosDelTutor
{
    /// <summary>Los avisos no borrados que le llegaron, del más nuevo al más viejo.</summary>
    Task<IReadOnlyList<AvisoDelTutor>> AvisosAsync(Guid tutorId, CancellationToken cancellationToken);

    Task<string?> CorreoAsync(Guid tutorId, CancellationToken cancellationToken);

    /// <summary>RF-064: el tutor guarda su correo (o lo quita con nulo) por la función <c>fijar_correo_tutor</c>.</summary>
    Task FijarCorreoAsync(string? correo, CancellationToken cancellationToken);

    Task<SuscripcionPush?> SuscripcionAsync(Guid tutorId, string endpoint, CancellationToken cancellationToken);

    void Agregar(SuscripcionPush suscripcion);

    void Borrar(SuscripcionPush suscripcion);
}

/// <summary>Lo que muestra la notificación. Nunca lleva el nombre de un alumno.</summary>
public sealed record MensajePush(string Titulo, string Cuerpo, string Url);

public enum ResultadoPush
{
    Enviado,

    /// <summary>El servicio dice que la suscripción ya no existe (404 o 410): se borra.</summary>
    Vencida,
    Fallido,
}

public interface ICanalPush
{
    /// <summary>La clave pública VAPID que el navegador necesita para suscribirse.</summary>
    string ClavePublica { get; }

    Task<ResultadoPush> EnviarAsync(SuscripcionPush suscripcion, MensajePush mensaje, CancellationToken cancellationToken);
}

/// <summary>WhatsApp del sistema con plantillas aprobadas (RN-074): el texto libre del aviso nunca viaja.</summary>
public interface ICanalWhatsapp
{
    /// <summary>El id del mensaje en Meta, o nulo si no salió.</summary>
    Task<string?> EnviarPlantillaAsync(
        string telefonoE164, string plantilla, IReadOnlyList<string> parametros, CancellationToken cancellationToken);
}
