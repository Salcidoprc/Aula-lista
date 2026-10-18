using AulaLista.Domain.Comun;

namespace AulaLista.Domain.Avisos;

/// <summary>
/// Un aviso del docente a los tutores de un grupo (SPEC-07). Se envía al crearse y no se edita después (RN-076);
/// borrarlo lo quita del portal y queda en auditoría.
/// </summary>
public sealed class Aviso : Entidad
{
    public const int LongitudTitulo = 80;
    public const int LongitudCuerpo = 1000;

    public Aviso(Guid grupoId, Guid docenteId, string titulo, string cuerpo, bool urgente, bool paraTodos, DateTimeOffset ahora)
        : base(ahora)
    {
        GrupoId = grupoId;
        DocenteId = docenteId;
        Titulo = titulo;
        Cuerpo = cuerpo;
        Urgente = urgente;
        ParaTodos = paraTodos;
    }

    private Aviso()
    {
    }

    public Guid GrupoId { get; private set; }

    public Guid DocenteId { get; private set; }

    public string Titulo { get; private set; } = string.Empty;

    public string Cuerpo { get; private set; } = string.Empty;

    /// <summary>RN-072: solo un aviso urgente puede ir también por WhatsApp del sistema.</summary>
    public bool Urgente { get; private set; }

    /// <summary>RN-071: a todos los tutores del grupo o a los elegidos.</summary>
    public bool ParaTodos { get; private set; }

    public DateTimeOffset? BorradoEn { get; private set; }

    public void Borrar(DateTimeOffset ahora)
    {
        BorradoEn ??= ahora;
        MarcarActualizada(ahora);
    }

    /// <summary>RN-070: título de 1 a 80 caracteres y cuerpo de 1 a 1,000, sin espacios de sobra.</summary>
    public static Resultado<(string Titulo, string Cuerpo)> Validar(string? titulo, string? cuerpo)
    {
        var t = (titulo ?? string.Empty).Trim();
        var c = (cuerpo ?? string.Empty).Trim();
        if (t.Length == 0 || t.Length > LongitudTitulo)
        {
            return Resultado.Fallo<(string, string)>(ErroresAvisos.TituloInvalido);
        }

        return c.Length is 0 or > LongitudCuerpo
            ? Resultado.Fallo<(string, string)>(ErroresAvisos.CuerpoInvalido)
            : Resultado.Exito((t, c));
    }
}

/// <summary>
/// Cómo le llega un aviso o una boleta a un tutor (RN-072, RN-073). Se decide al enviar: push y correo si los tiene,
/// WhatsApp del sistema si aplica, y si no le llega por ningún canal queda para que el docente lo comparta.
/// </summary>
public sealed record Canales(bool Push, bool Correo, bool Whatsapp)
{
    public bool Ninguno => !Push && !Correo && !Whatsapp;
}

/// <summary>La entrega de un aviso a un tutor: por dónde salió, si el docente lo compartió y cuándo lo leyó (RN-075).</summary>
public sealed class EntregaAviso : Entidad
{
    public EntregaAviso(Aviso aviso, Guid tutorId, Canales canales, bool sinSaldoWhatsapp, DateTimeOffset ahora)
        : base(ahora)
    {
        AvisoId = aviso.Id;
        DocenteId = aviso.DocenteId;
        TutorId = tutorId;
        Push = canales.Push;
        Correo = canales.Correo;
        Whatsapp = canales.Whatsapp;
        SinSaldoWhatsapp = sinSaldoWhatsapp;
    }

    private EntregaAviso()
    {
    }

    public Guid AvisoId { get; private set; }

    public Guid DocenteId { get; private set; }

    public Guid TutorId { get; private set; }

    public bool Push { get; private set; }

    public bool Correo { get; private set; }

    public bool Whatsapp { get; private set; }

    /// <summary>Urgente, pero el tutor ya llegó al límite del mes (RN-086): el detalle lo explica.</summary>
    public bool SinSaldoWhatsapp { get; private set; }

    /// <summary>RN-073: sin push, correo ni WhatsApp, el docente lo comparte desde su WhatsApp.</summary>
    public bool PorCompartir => !Push && !Correo && !Whatsapp;

    public DateTimeOffset? CompartidoEn { get; private set; }

    public DateTimeOffset? LeidoEn { get; private set; }

    /// <summary>RN-075: cuenta la primera vez que el tutor lo abre en el portal.</summary>
    public void MarcarLeido(DateTimeOffset ahora)
    {
        if (LeidoEn is null)
        {
            LeidoEn = ahora;
            MarcarActualizada(ahora);
        }
    }

    public void MarcarCompartido(DateTimeOffset ahora)
    {
        CompartidoEn ??= ahora;
        MarcarActualizada(ahora);
    }
}

/// <summary>Un navegador del tutor que aceptó notificaciones (Web Push con VAPID, RF-064).</summary>
public sealed class SuscripcionPush : Entidad
{
    public const int LongitudEndpoint = 1000;

    public SuscripcionPush(Guid tutorId, string endpoint, string p256dh, string auth, DateTimeOffset ahora)
        : base(ahora)
    {
        TutorId = tutorId;
        Endpoint = endpoint;
        P256dh = p256dh;
        Auth = auth;
    }

    private SuscripcionPush()
    {
    }

    public Guid TutorId { get; private set; }

    public string Endpoint { get; private set; } = string.Empty;

    public string P256dh { get; private set; } = string.Empty;

    public string Auth { get; private set; } = string.Empty;
}

/// <summary>Plantillas de WhatsApp del sistema (SPEC-07); el texto libre del aviso nunca va en el mensaje (RN-074).</summary>
public static class PlantillasWhatsapp
{
    public const string BoletaDisponible = "boleta_disponible";
    public const string AvisoUrgente = "aviso_urgente";
}

/// <summary>Estados de <c>mensaje_whatsapp</c>. Meta los reporta por webhook y solo avanzan (fallido es final).</summary>
public static class EstadoMensaje
{
    public const string Pendiente = "pendiente";
    public const string Enviado = "enviado";
    public const string Entregado = "entregado";
    public const string Leido = "leido";
    public const string Fallido = "fallido";

    public static readonly IReadOnlyList<string> Todos = [Pendiente, Enviado, Entregado, Leido, Fallido];
}

/// <summary>
/// Un mensaje del WhatsApp del sistema. Cuenta para el límite mensual por grupo (RN-086) y guarda su costo estimado
/// para vigilar RNF-022. Meta actualiza su estado por webhook.
/// </summary>
public sealed class MensajeWhatsapp : Entidad
{
    public MensajeWhatsapp(
        Guid grupoId, Guid docenteId, Guid tutorId, string plantilla, Guid referenciaId, decimal costoEstimado, DateTimeOffset ahora)
        : base(ahora)
    {
        GrupoId = grupoId;
        DocenteId = docenteId;
        TutorId = tutorId;
        Plantilla = plantilla;
        ReferenciaId = referenciaId;
        CostoEstimado = costoEstimado;
        Estado = EstadoMensaje.Pendiente;
    }

    private MensajeWhatsapp()
    {
    }

    public Guid GrupoId { get; private set; }

    public Guid DocenteId { get; private set; }

    public Guid TutorId { get; private set; }

    public string Plantilla { get; private set; } = string.Empty;

    /// <summary>El aviso o el periodo de la boleta por el que se envió.</summary>
    public Guid ReferenciaId { get; private set; }

    public decimal CostoEstimado { get; private set; }

    public string Estado { get; private set; } = EstadoMensaje.Pendiente;

    public string? IdMeta { get; private set; }

    public void MarcarEnviado(string idMeta, DateTimeOffset ahora)
    {
        IdMeta = idMeta;
        Estado = EstadoMensaje.Enviado;
        MarcarActualizada(ahora);
    }

    public void MarcarFallido(DateTimeOffset ahora)
    {
        Estado = EstadoMensaje.Fallido;
        MarcarActualizada(ahora);
    }
}

public static class ErroresAvisos
{
    public static readonly ErrorNegocio TituloInvalido = new("aviso_titulo_invalido", "Escribe un título de hasta 80 caracteres.");
    public static readonly ErrorNegocio CuerpoInvalido = new("aviso_cuerpo_invalido", "Escribe el aviso; puede tener hasta 1,000 caracteres.");
    public static readonly ErrorNegocio SinDestinatarios = new("aviso_sin_destinatarios", "Elige al menos un tutor. Si el grupo no tiene tutores, agrégalos en la ficha de cada alumno.");
    public static readonly ErrorNegocio SuscripcionInvalida = new("suscripcion_push_invalida", "El navegador no entregó una suscripción válida. Intenta activar las notificaciones otra vez.");
    public static readonly ErrorNegocio CorreoInvalido = new("correo_tutor_invalido", "Revisa el correo; se ve incompleto.");

    public static readonly IReadOnlyList<ErrorNegocio> Todos =
        [TituloInvalido, CuerpoInvalido, SinDestinatarios, SuscripcionInvalida, CorreoInvalido];
}
