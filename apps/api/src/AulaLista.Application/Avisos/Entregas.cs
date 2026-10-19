using System.Net;
using AulaLista.Application.Comun;
using AulaLista.Application.Cuentas;
using AulaLista.Application.Grupos;
using AulaLista.Application.Operacion;
using AulaLista.Application.Tutores;
using AulaLista.Domain.Avisos;
using AulaLista.Domain.Grupos;
using AulaLista.Domain.Operacion;
using Microsoft.Extensions.Options;

namespace AulaLista.Application.Avisos;

/// <summary>
/// Un tutor de los alumnos del docente con lo que el docente capturó de él (su vínculo, ADR-0014) y lo que el tutor
/// registró en el portal. El correo capturado por el docente gana; si no hay, se usa el que el tutor registró.
/// </summary>
public sealed record TutorDelGrupo(
    Guid TutorId, string Nombre, string? Parentesco, string TelefonoE164, string? Correo, bool Push, IReadOnlyList<Alumno> Alumnos)
{
    /// <summary>"Hola Rosa": solo el primer nombre en plantillas y correos.</summary>
    public string PrimerNombre => Nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? Nombre;
}

/// <summary>Decide por dónde le llega algo a cada tutor (RN-072, RN-073) y cuánto WhatsApp le queda (RN-086).</summary>
public sealed class PlanEntregas(IRepositorioGrupos grupos, IRepositorioAvisos avisos, IOptions<OpcionesWhatsapp> opciones, TimeProvider reloj)
{
    public OpcionesWhatsapp Opciones => opciones.Value;

    /// <summary>Los tutores de esos alumnos, uno por tutor, en el orden de la lista.</summary>
    public async Task<IReadOnlyList<TutorDelGrupo>> TutoresAsync(IReadOnlyCollection<Alumno> alumnos, CancellationToken ct)
    {
        var porId = alumnos.ToDictionary(a => a.Id);
        if (porId.Count == 0)
        {
            return [];
        }

        var vinculos = (await grupos.VinculosAsync(porId.Keys.ToList(), ct)).Where(v => porId.ContainsKey(v.AlumnoId)).ToList();
        var propios = await avisos.ContactosAsync(vinculos.Select(v => v.TutorId).Distinct().ToList(), ct);
        return vinculos
            .GroupBy(v => v.TutorId)
            .Select(g =>
            {
                var suyos = g.OrderBy(v => porId[v.AlumnoId].NumeroLista).ToList();
                var propio = propios.GetValueOrDefault(g.Key);
                return new TutorDelGrupo(
                    g.Key, suyos[0].Nombre, suyos[0].Parentesco, suyos[0].TelefonoE164,
                    suyos.Select(v => v.Correo).FirstOrDefault(c => c is not null) ?? propio?.Correo,
                    propio?.Push ?? false,
                    suyos.Select(v => porId[v.AlumnoId]).ToList());
            })
            .OrderBy(t => t.Alumnos[0].NumeroLista)
            .ThenBy(t => t.Nombre, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <summary>RN-086: avisos urgentes por WhatsApp que le quedan este mes a cada tutor en el grupo.</summary>
    public async Task<Func<Guid, int>> UrgentesRestantesAsync(Guid grupoId, CancellationToken ct)
    {
        var usados = await avisos.MensajesPorTutorAsync(grupoId, PlantillasWhatsapp.AvisoUrgente, InicioDelMes(), null, ct);
        var limite = opciones.Value.UrgentesPorTutorPorMes;
        return tutorId => Math.Max(0, limite - usados.GetValueOrDefault(tutorId));
    }

    /// <summary>RN-086: mensajes de boleta que le quedan a cada tutor en el periodo.</summary>
    public async Task<Func<Guid, int>> BoletasRestantesAsync(Guid grupoId, Guid periodoId, CancellationToken ct)
    {
        var usados = await avisos.MensajesPorTutorAsync(grupoId, PlantillasWhatsapp.BoletaDisponible, null, periodoId, ct);
        var limite = opciones.Value.BoletasPorTutorPorPeriodo;
        return tutorId => Math.Max(0, limite - usados.GetValueOrDefault(tutorId));
    }

    /// <summary>El primer instante del mes en curso en la zona del límite.</summary>
    public DateTimeOffset InicioDelMes()
    {
        var zona = TimeZoneInfo.TryFindSystemTimeZoneById(opciones.Value.ZonaHoraria, out var encontrada) ? encontrada : TimeZoneInfo.Utc;
        var local = TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), zona);
        var inicio = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(inicio, zona.GetUtcOffset(inicio)).ToUniversalTime();
    }

    /// <summary>Un mensaje pendiente que cuenta para el límite desde ya; el trabajo lo envía.</summary>
    public MensajeWhatsapp NuevoMensaje(Grupo grupo, Guid tutorId, string plantilla, Guid referenciaId) =>
        new(grupo.Id, grupo.DocenteId, tutorId, plantilla, referenciaId, opciones.Value.CostoPorMensaje, reloj.GetUtcNow());
}

/// <summary>Lo que se le manda a un tutor por cada canal; el enlace del correo y del WhatsApp se crea al enviar.</summary>
internal sealed record Envio(
    MensajePush Push,
    string DestinoPortal,
    Func<string, CorreoSaliente> Correo,
    string Plantilla,
    Func<string, IReadOnlyList<string>> Parametros);

/// <summary>
/// Envía por push, correo y WhatsApp a un tutor. Cada falla se cuenta y no detiene a los demás tutores; las
/// suscripciones vencidas se borran.
/// </summary>
public sealed class EnviosATutores(
    IRepositorioAvisos avisos, EmisionEnlaces emision, ICanalPush push, ICanalWhatsapp whatsapp, IEnvioCorreo correo,
    Operacion.IRegistroErrores errores, TimeProvider reloj)
{
    private const string Trabajo = "envio_tutores";

    internal async Task<int> EnviarAsync(
        TutorDelGrupo tutor, Grupo grupo, Envio envio, IEnumerable<SuscripcionPush> suscripciones, bool porCorreo, MensajeWhatsapp? mensaje,
        CancellationToken ct)
    {
        var fallas = 0;
        foreach (var suscripcion in suscripciones)
        {
            switch (await push.EnviarAsync(suscripcion, envio.Push, ct))
            {
                case ResultadoPush.Vencida:
                    avisos.Borrar(suscripcion);
                    break;
                case ResultadoPush.Fallido:
                    fallas++;
                    await errores.RegistrarAsync(OrigenError.Push, Trabajo, grupo.DocenteId, grupo.Id, "push_fallido", ct);
                    break;
            }
        }

        var correoDestino = porCorreo ? tutor.Correo : null;
        if (correoDestino is null && mensaje is null)
        {
            return fallas;
        }

        var (_, enlace) = await emision.EmitirAsync(tutor.TutorId, tutor.Alumnos[0], grupo, envio.DestinoPortal, ct);
        if (correoDestino is not null)
        {
            try
            {
                await correo.EnviarAsync(envio.Correo(enlace.Url) with { Para = correoDestino }, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                fallas++;
                await errores.RegistrarAsync(OrigenError.Correo, Trabajo, grupo.DocenteId, grupo.Id, ex, ct);
            }
        }

        if (mensaje is not null)
        {
            var idMeta = await whatsapp.EnviarPlantillaAsync(tutor.TelefonoE164, envio.Plantilla, envio.Parametros(enlace.Url), ct);
            if (idMeta is null)
            {
                mensaje.MarcarFallido(reloj.GetUtcNow());
                fallas++;
                await errores.RegistrarAsync(OrigenError.Whatsapp, Trabajo, grupo.DocenteId, grupo.Id, "whatsapp_fallido", ct);
            }
            else
            {
                mensaje.MarcarEnviado(idMeta, reloj.GetUtcNow());
            }
        }

        return fallas;
    }
}

/// <summary>Correos de avisos y boletas. Sin nombres de alumnos en el asunto; el cuerpo lo lee solo su tutor.</summary>
internal static class CorreosAvisos
{
    public static CorreoSaliente Aviso(string nombreTutor, string grupo, string? docente, Aviso aviso, string url)
    {
        var quien = docente is null ? "La maestra o el maestro" : docente;
        var mensaje = $"Hola {nombreTutor}, {quien} envió un aviso{(aviso.Urgente ? " urgente" : string.Empty)} del grupo {grupo}:";
        const string Nota = "Ábrelo con el botón para que tu maestra o maestro sepa que ya lo leíste.";
        return Armar(
            $"{(aviso.Urgente ? "Aviso urgente" : "Aviso")} del grupo {grupo}", mensaje, aviso.Titulo, "Leer el aviso", url, Nota);
    }

    public static CorreoSaliente Boleta(string nombreTutor, string alumnos, string unidad, string url)
    {
        var mensaje = $"Hola {nombreTutor}, ya está disponible el reporte de {alumnos} del {unidad}.";
        const string Nota = "Es un reporte de tu maestra o maestro; no es un documento oficial de la SEP.";
        return Armar($"Reporte del {unidad} disponible", mensaje, null, "Ver el reporte", url, Nota);
    }

    private static string Html(string texto) => WebUtility.HtmlEncode(texto);

    private static CorreoSaliente Armar(string asunto, string mensaje, string? destacado, string boton, string url, string nota)
    {
        var bloque = destacado is null ? string.Empty : $"""<p style="font-size:18px;font-weight:700;margin:16px 0">{Html(destacado)}</p>""";
        var html = $"""
            <!doctype html>
            <html lang="es-MX"><body style="font-family:system-ui,sans-serif;color:#1f2433;max-width:480px;margin:0 auto;padding:24px">
            <p style="font-size:18px;font-weight:700">Aula Lista</p>
            <p>{Html(mensaje)}</p>
            {bloque}
            <p><a href="{Html(url)}" style="display:inline-block;background:#3b4fb8;color:#fff;padding:12px 20px;border-radius:12px;text-decoration:none;font-weight:700">{Html(boton)}</a></p>
            <p style="color:#5b6275;font-size:14px">{Html(nota)}</p>
            </body></html>
            """;
        var texto = destacado is null ? $"{mensaje}\n\n{boton}: {url}\n\n{nota}" : $"{mensaje}\n\n{destacado}\n\n{boton}: {url}\n\n{nota}";
        return new CorreoSaliente(string.Empty, asunto, html, texto);
    }
}

internal static class TextosAvisos
{
    /// <summary>"3° A" como lo ve el tutor en el portal.</summary>
    public static string Grupo(Grupo grupo) => $"{grupo.Grado}° {grupo.Nombre}";

    /// <summary>"Ana y Mateo": los primeros nombres de los hijos, para la boleta.</summary>
    public static string Hijos(IEnumerable<Alumno> alumnos)
    {
        var nombres = alumnos.Select(a => a.Nombres.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? a.Nombres).ToList();
        return nombres.Count <= 1 ? string.Concat(nombres) : $"{string.Join(", ", nombres[..^1])} y {nombres[^1]}";
    }

    public static string UrlPortal(IOptions<OpcionesCuentas> cuentas, string ruta) => $"{cuentas.Value.UrlWeb.TrimEnd('/')}{ruta}";
}
