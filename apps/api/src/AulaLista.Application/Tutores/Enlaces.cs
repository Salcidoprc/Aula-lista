using System.Net;
using AulaLista.Application.Comun;
using AulaLista.Application.Cuentas;
using AulaLista.Application.Grupos;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Grupos;
using AulaLista.Domain.Tutores;
using Microsoft.Extensions.Options;

namespace AulaLista.Application.Tutores;

/// <summary>El enlace recién creado. El token solo existe aquí: en la base queda su hash (RN-060).</summary>
public sealed record EnlaceDto(string Url, DateTimeOffset ExpiraEn);

/// <summary>Reenviar crea otro enlace; regenerar revoca los anteriores y cierra sus sesiones (RN-063).</summary>
public sealed record EnviarEnlace(bool Regenerar);

/// <summary>Lo que el docente ve del enlace de un tutor en la ficha del alumno.</summary>
public sealed record EstadoEnlaceDto(DateTimeOffset? UltimoAccesoEn, bool Sospechoso);

/// <summary>Una sesión del tutor lista para la cookie.</summary>
public sealed record SesionTutorEmitida(string Token, DateTimeOffset ExpiraEn);

/// <summary>Quién es el tutor de la cookie y, si se renovó, hasta cuándo dura ahora.</summary>
public sealed record SesionTutorValida(Guid TutorId, DateTimeOffset? RenovadaHasta);

public static class AccionesAuditoriaTutores
{
    public const string EnlaceEnviado = "enlace_tutor_enviado";
    public const string EnlaceRegenerado = "enlace_tutor_regenerado";
    public const string EnlaceSospechoso = "enlace_tutor_sospechoso";
}

/// <summary>
/// RN-063: desde la ficha del alumno, el docente obtiene un enlace nuevo para un tutor suyo. Con
/// <see cref="EnviarEnlace.Regenerar"/> antes revoca todos los enlaces del tutor y cierra sus sesiones.
/// </summary>
public sealed class EnviarEnlaceCasoUso(
    IRepositorioGrupos grupos, IRepositorioEnlaces enlaces, EmisionEnlaces emision, IContextoUsuario usuario, IAuditoria auditoria,
    IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<EnlaceDto>?> EjecutarAsync(Guid alumnoId, Guid tutorId, EnviarEnlace comando, CancellationToken ct)
    {
        // El vínculo se lee con RLS por docente: un tutor ajeno a sus alumnos es 404.
        if (await grupos.AlumnoAsync(alumnoId, ct) is not { } alumno
            || !(await grupos.VinculosAsync([alumnoId], ct)).Any(v => v.TutorId == tutorId)
            || await grupos.GrupoAsync(alumno.GrupoId, ct) is not { } grupo)
        {
            return null;
        }

        var ahora = reloj.GetUtcNow();
        if (comando.Regenerar)
        {
            foreach (var anterior in await enlaces.EnlacesAsync(tutorId, ct))
            {
                anterior.Revocar(ahora);
            }

            foreach (var sesion in await enlaces.SesionesAsync(tutorId, ct))
            {
                sesion.Revocar(ahora);
            }
        }

        var (enlace, dto) = await emision.EmitirAsync(tutorId, alumno, grupo, null, ct);
        auditoria.Registrar(
            comando.Regenerar ? AccionesAuditoriaTutores.EnlaceRegenerado : AccionesAuditoriaTutores.EnlaceEnviado,
            "enlace_tutor", enlace.Id, usuario.DocenteId);
        await unidad.GuardarAsync(ct);
        return Resultado.Exito(dto);
    }
}

/// <summary>
/// Crea un enlace personal del tutor (RN-060) para la ficha, un aviso o una boleta. Lo agrega a la unidad de trabajo;
/// quien lo pide guarda. El token solo existe en la URL que se devuelve.
/// </summary>
public sealed class EmisionEnlaces(
    IRepositorioGrupos grupos, IRepositorioEnlaces enlaces, IOptions<OpcionesTutores> opciones, IOptions<OpcionesCuentas> cuentas,
    TimeProvider reloj)
{
    /// <param name="destino">Consulta para abrir el portal en un lugar, por ejemplo <c>alumno=…&amp;ver=avisos</c>.</param>
    public async Task<(EnlaceTutor Enlace, EnlaceDto Dto)> EmitirAsync(
        Guid tutorId, Alumno alumno, Grupo grupo, string? destino, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var ciclo = await grupos.CicloAsync(grupo.CicloEscolarId, ct);
        var expira = Vencimiento(ciclo?.Fin, ahora);
        var (token, hash) = TokensSeguros.Generar();
        var enlace = new EnlaceTutor(tutorId, alumno.Id, alumno.DocenteId, hash, expira, ahora);
        enlaces.Agregar(enlace);
        return (enlace, new EnlaceDto(UrlDelPortal(cuentas.Value.UrlWeb, token, destino), expira));
    }

    /// <summary>El token va en el fragmento: el navegador no lo manda al servidor, así no queda en bitácoras.</summary>
    public static string UrlDelPortal(string urlWeb, string token, string? destino = null) =>
        $"{urlWeb.TrimEnd('/')}/tutor{(string.IsNullOrEmpty(destino) ? string.Empty : "?" + destino)}#{token}";

    /// <summary>RN-062: fin del ciclo más la gracia; si el ciclo ya terminó, cuenta la gracia desde hoy.</summary>
    private DateTimeOffset Vencimiento(DateOnly? finCiclo, DateTimeOffset ahora)
    {
        var gracia = opciones.Value.DiasGraciaEnlace;
        var minimo = ahora.AddDays(gracia);
        if (finCiclo is not { } fin)
        {
            return minimo;
        }

        var porCiclo = new DateTimeOffset(fin.AddDays(gracia + 1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        return porCiclo > minimo ? porCiclo : minimo;
    }
}

/// <summary>
/// RN-062: canjea el token del enlace por una sesión del navegador. Un token alterado, revocado o vencido da siempre
/// el mismo error, sin indicar si existió. Si el navegador ya tiene una sesión vigente del mismo tutor, se reutiliza
/// y no cuenta como dispositivo nuevo (RN-065).
/// </summary>
public sealed class CanjearEnlaceCasoUso(
    IRepositorioEnlaces enlaces, IRepositorioCuentas cuentas, IEnvioCorreo correo, IAuditoria auditoria, IContextoUsuario usuario,
    IUnidadDeTrabajo unidad, IOptions<OpcionesTutores> opciones, IOptions<OpcionesCuentas> opcionesCuentas, TimeProvider reloj)
{
    public async Task<Resultado<SesionTutorEmitida>> EjecutarAsync(string token, string? sesionActual, CancellationToken ct)
    {
        var hash = string.IsNullOrWhiteSpace(token) ? null : TokensSeguros.Hash(token.Trim());
        if (hash is null || await enlaces.TutorDelEnlaceAsync(hash, ct) is not { } tutorId)
        {
            return Resultado.Fallo<SesionTutorEmitida>(ErroresTutores.EnlaceInvalido);
        }

        usuario.AsumirTutor(tutorId);
        var ahora = reloj.GetUtcNow();
        var o = opciones.Value;
        var canje = await unidad.EnTransaccionAsync(async () =>
        {
            if (await enlaces.EnlacePorHashAsync(hash, ct) is not { } enlace || !enlace.EstaVigente(ahora))
            {
                return null;
            }

            enlace.RegistrarUso(ahora);
            if (!string.IsNullOrEmpty(sesionActual)
                && await enlaces.SesionPorHashAsync(TokensSeguros.Hash(sesionActual), ct) is { } actual
                && actual.Sesion.TutorId == tutorId
                && actual.Sesion.EstaVigente(ahora)
                && actual.Enlace.EstaVigente(ahora))
            {
                return new Canje(new SesionTutorEmitida(sesionActual, actual.Sesion.ExpiraEn), null);
            }

            var (nuevo, hashSesion) = TokensSeguros.Generar();
            var sesion = new SesionTutor(enlace, hashSesion, ahora.AddDays(o.DiasSesion), ahora);
            enlaces.Agregar(sesion);

            // La sesión nueva todavía no está en la base: se suma a la cuenta.
            var dispositivos = await enlaces.SesionesDesdeAsync(enlace.Id, ahora.AddDays(-o.DiasVentanaDispositivos), ct) + 1;
            var sospechoso = dispositivos > o.DispositivosPorEnlace && enlace.MarcarSospechoso(ahora);
            if (sospechoso)
            {
                auditoria.Registrar(AccionesAuditoriaTutores.EnlaceSospechoso, "enlace_tutor", enlace.Id, enlace.CreadoPorDocenteId, dispositivos);
            }

            return new Canje(new SesionTutorEmitida(nuevo, sesion.ExpiraEn), sospechoso ? enlace : null);
        }, ct);

        if (canje is null)
        {
            return Resultado.Fallo<SesionTutorEmitida>(ErroresTutores.EnlaceInvalido);
        }

        if (canje.Sospechoso is { } aviso && await cuentas.PorIdAsync(aviso.CreadoPorDocenteId, ct) is { } cuenta)
        {
            await correo.EnviarAsync(CorreoEnlaceSospechoso(cuenta.Correo, opcionesCuentas.Value.UrlWeb, aviso.AlumnoId, o.DispositivosPorEnlace), ct);
        }

        return Resultado.Exito(canje.Sesion);
    }

    /// <summary>Sin nombres de alumno ni de tutor: el docente los ve al abrir la ficha.</summary>
    internal static CorreoSaliente CorreoEnlaceSospechoso(string para, string urlWeb, Guid alumnoId, int dispositivos)
    {
        var enlace = $"{urlWeb.TrimEnd('/')}/alumnos/{alumnoId}?seccion=datos";
        var mensaje = $"El enlace que enviaste a un tutor se abrió en más de {dispositivos} celulares o navegadores en 30 días. "
            + "Puede que alguien lo haya reenviado.";
        const string Nota = "Si no lo reconoces, abre la ficha del alumno y regenera el enlace: el anterior deja de funcionar.";
        var e = WebUtility.HtmlEncode(enlace);
        var html = $"""
            <!doctype html>
            <html lang="es-MX"><body style="font-family:system-ui,sans-serif;color:#1f2433;max-width:480px;margin:0 auto;padding:24px">
            <p style="font-size:18px;font-weight:700">Aula Lista</p>
            <p>{WebUtility.HtmlEncode(mensaje)}</p>
            <p><a href="{e}" style="display:inline-block;background:#3b4fb8;color:#fff;padding:12px 20px;border-radius:12px;text-decoration:none;font-weight:700">Revisar el enlace</a></p>
            <p style="color:#5b6275;font-size:14px">{WebUtility.HtmlEncode(Nota)}</p>
            </body></html>
            """;
        return new CorreoSaliente(para, "Un enlace de tutor se abrió en muchos dispositivos", html, $"{mensaje}\n\nRevisar el enlace: {enlace}\n\n{Nota}");
    }

    private sealed record Canje(SesionTutorEmitida Sesion, EnlaceTutor? Sospechoso);
}

/// <summary>Valida la cookie del tutor en cada solicitud y la renueva con el uso (RN-062).</summary>
public sealed class ValidacionSesionTutor(
    IRepositorioEnlaces enlaces, IContextoUsuario usuario, IUnidadDeTrabajo unidad, IOptions<OpcionesTutores> opciones, TimeProvider reloj)
{
    public async Task<SesionTutorValida?> ValidarAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var hash = TokensSeguros.Hash(token);
        if (await enlaces.TutorDeLaSesionAsync(hash, ct) is not { } tutorId)
        {
            return null;
        }

        usuario.AsumirTutor(tutorId);
        var ahora = reloj.GetUtcNow();
        return await unidad.EnTransaccionAsync(async () =>
        {
            if (await enlaces.SesionPorHashAsync(hash, ct) is not { } actual
                || !actual.Sesion.EstaVigente(ahora)
                || !actual.Enlace.EstaVigente(ahora))
            {
                return null;
            }

            var renovada = actual.Sesion.Renovar(ahora, TimeSpan.FromDays(opciones.Value.DiasSesion));
            return new SesionTutorValida(tutorId, renovada ? actual.Sesion.ExpiraEn : null);
        }, ct);
    }
}

/// <summary>Último acceso y sospecha de los tutores de un alumno, para la ficha del docente.</summary>
public sealed class EstadoEnlaces(IRepositorioEnlaces enlaces, TimeProvider reloj)
{
    public async Task<IReadOnlyDictionary<Guid, EstadoEnlaceDto>> LeerAsync(IReadOnlyCollection<Guid> tutorIds, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var estados = new Dictionary<Guid, EstadoEnlaceDto>();
        foreach (var tutorId in tutorIds.Distinct())
        {
            var suyos = await enlaces.EnlacesAsync(tutorId, ct);
            if (suyos.Count == 0)
            {
                continue;
            }

            var sesiones = await enlaces.SesionesAsync(tutorId, ct);
            var acceso = sesiones.Count == 0 ? (DateTimeOffset?)null : sesiones.Max(s => s.UltimoUsoEn);
            estados[tutorId] = new EstadoEnlaceDto(acceso, suyos.Any(e => e.EstaVigente(ahora) && e.SospechosoEn is not null));
        }

        return estados;
    }
}
