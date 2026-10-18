using AulaLista.Application.Comun;
using AulaLista.Application.Grupos;
using AulaLista.Application.Tutores;
using AulaLista.Domain.Avisos;
using AulaLista.Domain.Comun;
using Microsoft.Extensions.Options;

namespace AulaLista.Application.Avisos;

/// <summary>Un aviso como lo ve el tutor (RF-063): completo, porque son cortos.</summary>
public sealed record AvisoTutorDto(Guid Id, string Titulo, string Cuerpo, bool Urgente, DateTimeOffset CreadoEn, string? Docente, DateTimeOffset? LeidoEn);

/// <summary>El último aviso del hijo para la tarjeta de "Mis hijos".</summary>
public sealed record ResumenAvisoDto(Guid Id, string Titulo, DateTimeOffset CreadoEn, bool Leido);

/// <summary>RF-064: el correo del tutor y la clave pública para suscribir este navegador a push.</summary>
public sealed record ContactoTutorDto(string? Correo, string ClavePush);

public sealed record GuardarCorreoTutor(string? Correo);

/// <summary>Lo que entrega <c>PushSubscription.toJSON()</c> del navegador.</summary>
public sealed record SuscribirPush(string? Endpoint, string? P256dh, string? Auth);

public sealed record DesuscribirPush(string? Endpoint);

/// <summary>La pestaña Avisos del detalle del hijo: los del grupo del hijo, del más nuevo al más viejo.</summary>
public sealed class AvisosDelHijoCasoUso(IConsultasTutor consultas, IAvisosDelTutor avisos, IContextoUsuario usuario)
{
    public async Task<IReadOnlyList<AvisoTutorDto>?> EjecutarAsync(Guid alumnoId, CancellationToken ct)
    {
        if (usuario.TutorId is not { } tutorId
            || (await consultas.HijosAsync(tutorId, ct)).FirstOrDefault(h => h.AlumnoId == alumnoId) is not { } hijo)
        {
            return null;
        }

        return (await avisos.AvisosAsync(tutorId, ct))
            .Where(a => a.Aviso.GrupoId == hijo.GrupoId)
            .Select(a => new AvisoTutorDto(a.Aviso.Id, a.Aviso.Titulo, a.Aviso.Cuerpo, a.Aviso.Urgente, a.Aviso.CreatedAt, a.Docente, a.Entrega.LeidoEn))
            .ToList();
    }
}

/// <summary>RN-075, RF-063: abrir la pestaña Avisos en el portal marca como leídos los avisos de ese hijo.</summary>
public sealed class LeerAvisosDelHijoCasoUso(
    IConsultasTutor consultas, IAvisosDelTutor avisos, IContextoUsuario usuario, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<bool> EjecutarAsync(Guid alumnoId, CancellationToken ct)
    {
        if (usuario.TutorId is not { } tutorId
            || (await consultas.HijosAsync(tutorId, ct)).FirstOrDefault(h => h.AlumnoId == alumnoId) is not { } hijo)
        {
            return false;
        }

        var ahora = reloj.GetUtcNow();
        foreach (var aviso in (await avisos.AvisosAsync(tutorId, ct)).Where(a => a.Aviso.GrupoId == hijo.GrupoId))
        {
            aviso.Entrega.MarcarLeido(ahora);
        }

        await unidad.GuardarAsync(ct);
        return true;
    }
}

public sealed class ContactoTutorCasoUso(IAvisosDelTutor avisos, ICanalPush push, IContextoUsuario usuario)
{
    public async Task<ContactoTutorDto?> EjecutarAsync(CancellationToken ct) =>
        usuario.TutorId is { } tutorId ? new ContactoTutorDto(await avisos.CorreoAsync(tutorId, ct), push.ClavePublica) : null;
}

/// <summary>RF-064: el tutor registra o quita su correo para recibir avisos.</summary>
public sealed class GuardarCorreoTutorCasoUso(IAvisosDelTutor avisos, ICanalPush push, IContextoUsuario usuario)
{
    public async Task<Resultado<ContactoTutorDto>?> EjecutarAsync(GuardarCorreoTutor comando, CancellationToken ct)
    {
        if (usuario.TutorId is not { } tutorId)
        {
            return null;
        }

        if (!Correos.EsValidoOVacio(comando.Correo) || (comando.Correo?.Trim().Length ?? 0) > Domain.Grupos.VinculoTutor.LongitudCorreo)
        {
            return Resultado.Fallo<ContactoTutorDto>(ErroresAvisos.CorreoInvalido);
        }

        await avisos.FijarCorreoAsync(Correos.Normalizar(comando.Correo), ct);
        return Resultado.Exito(new ContactoTutorDto(await avisos.CorreoAsync(tutorId, ct), push.ClavePublica));
    }
}

/// <summary>
/// RF-064: guarda la suscripción push de este navegador. Solo se aceptan servicios de push conocidos: el servidor les
/// hace solicitudes, así que una URL cualquiera abriría la puerta a llamadas a la red interna.
/// </summary>
public sealed class SuscribirPushCasoUso(
    IAvisosDelTutor avisos, IContextoUsuario usuario, IUnidadDeTrabajo unidad, IOptions<OpcionesPush> opciones, TimeProvider reloj)
{
    public async Task<Resultado?> EjecutarAsync(SuscribirPush comando, CancellationToken ct)
    {
        if (usuario.TutorId is not { } tutorId)
        {
            return null;
        }

        if (!EsValida(comando, opciones.Value))
        {
            return Resultado.Fallo(ErroresAvisos.SuscripcionInvalida);
        }

        if (await avisos.SuscripcionAsync(tutorId, comando.Endpoint!, ct) is { } anterior)
        {
            avisos.Borrar(anterior);
        }

        avisos.Agregar(new SuscripcionPush(tutorId, comando.Endpoint!, comando.P256dh!, comando.Auth!, reloj.GetUtcNow()));
        await unidad.GuardarAsync(ct);
        return Resultado.Exito();
    }

    internal static bool EsValida(SuscribirPush s, OpcionesPush opciones) =>
        s is { Endpoint.Length: > 0 and <= SuscripcionPush.LongitudEndpoint, P256dh.Length: >= 80 and <= 120, Auth.Length: >= 16 and <= 32 }
        && Uri.TryCreate(s.Endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && opciones.ServiciosPermitidos.Any(h => uri.Host.Equals(h, StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase))
        && EsBase64Url(s.P256dh!) && EsBase64Url(s.Auth!);

    private static bool EsBase64Url(string texto) => texto.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '=');
}

public sealed class DesuscribirPushCasoUso(IAvisosDelTutor avisos, IContextoUsuario usuario, IUnidadDeTrabajo unidad)
{
    public async Task<bool> EjecutarAsync(DesuscribirPush comando, CancellationToken ct)
    {
        if (usuario.TutorId is not { } tutorId)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(comando.Endpoint) && await avisos.SuscripcionAsync(tutorId, comando.Endpoint, ct) is { } suscripcion)
        {
            avisos.Borrar(suscripcion);
            await unidad.GuardarAsync(ct);
        }

        return true;
    }
}
