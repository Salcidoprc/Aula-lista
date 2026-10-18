using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AulaLista.Application.Boletas;
using AulaLista.Application.Comun;
using AulaLista.Application.Cuentas;
using AulaLista.Application.Grupos;
using AulaLista.Application.Tutores;
using AulaLista.Domain.Avisos;
using AulaLista.Domain.Boletas;
using AulaLista.Domain.Grupos;
using Microsoft.Extensions.Options;

namespace AulaLista.Application.Avisos;

/// <summary>Un tutor al que no le llega la boleta por ningún canal: el docente se la comparte (RN-054, RN-073).</summary>
public sealed record TutorPorCompartirDto(Guid TutorId, string Nombre, string Telefono, Guid BoletaId, IReadOnlyList<AlumnoDeTutorDto> Alumnos);

/// <summary>
/// RN-054: al publicar, cada tutor de los alumnos publicados recibe un aviso: WhatsApp del sistema (uno por periodo,
/// RN-086) y push o correo si los tiene. Una corrección (RN-052) no vuelve a avisar.
/// </summary>
public sealed class AvisoBoletas(PlanEntregas plan, IRepositorioAvisos avisos)
{
    /// <summary>Aparta los WhatsApp y devuelve a quién no le llegará por ningún lado.</summary>
    public async Task<IReadOnlyList<TutorPorCompartirDto>> PlanearAsync(
        PeriodoCompleto periodo, IReadOnlyList<(Alumno Alumno, Boleta Boleta)> publicadas, CancellationToken ct)
    {
        var porAlumno = publicadas.Where(p => !p.Boleta.EsCorreccion).ToDictionary(p => p.Alumno.Id, p => p.Boleta);
        var tutores = await plan.TutoresAsync(publicadas.Where(p => porAlumno.ContainsKey(p.Alumno.Id)).Select(p => p.Alumno).ToList(), ct);
        var restantes = await plan.BoletasRestantesAsync(periodo.Grupo.Id, periodo.Periodo.Id, ct);
        var sinCanal = new List<TutorPorCompartirDto>();
        foreach (var tutor in tutores)
        {
            var whatsapp = restantes(tutor.TutorId) > 0;
            if (whatsapp)
            {
                avisos.Agregar(plan.NuevoMensaje(periodo.Grupo, tutor.TutorId, PlantillasWhatsapp.BoletaDisponible, periodo.Periodo.Id));
            }
            else if (!tutor.Push && tutor.Correo is null)
            {
                sinCanal.Add(new TutorPorCompartirDto(
                    tutor.TutorId, tutor.Nombre, tutor.TelefonoE164, porAlumno[tutor.Alumnos[0].Id].Id, DestinatariosAvisoCasoUso.Alumnos(tutor.Alumnos)));
            }
        }

        return sinCanal;
    }

    /// <summary>Para la hoja previa a publicar: por alumno, cuántos de sus tutores tienen push y cuántos no recibirían nada.</summary>
    public async Task<IReadOnlyDictionary<Guid, (int Push, int PorCompartir)>> CanalesAsync(
        PeriodoCompleto periodo, IReadOnlyCollection<Alumno> alumnos, CancellationToken ct)
    {
        var tutores = await plan.TutoresAsync(alumnos, ct);
        var restantes = await plan.BoletasRestantesAsync(periodo.Grupo.Id, periodo.Periodo.Id, ct);
        return alumnos.ToDictionary(
            a => a.Id,
            a =>
            {
                var suyos = tutores.Where(t => t.Alumnos.Any(x => x.Id == a.Id)).ToList();
                return (suyos.Count(t => t.Push), suyos.Count(t => restantes(t.TutorId) == 0 && !t.Push && t.Correo is null));
            });
    }
}

/// <summary>RN-054: envía los avisos de boleta disponible ya planeados, uno por tutor aunque tenga varios hijos publicados.</summary>
public sealed class AvisarBoletasTrabajo(
    IRepositorioGrupos grupos, IRepositorioBoletas boletas, IRepositorioAvisos avisos, PlanEntregas plan, EnviosATutores envios,
    IUnidadDeTrabajo unidad, IOptions<OpcionesCuentas> cuentas)
{
    public async Task<int> EjecutarAsync(Guid periodoId, IReadOnlyCollection<Guid> boletaIds, CancellationToken ct)
    {
        if (await grupos.PeriodoAsync(periodoId, ct) is not { } periodo)
        {
            return 0;
        }

        var publicadas = (await boletas.BoletasPorIdAsync(boletaIds, ct))
            .Where(b => b is { EstaPublicada: true, EsVigente: true, EsCorreccion: false })
            .Select(b => b.AlumnoId)
            .ToHashSet();
        var alumnos = (await grupos.AlumnosAsync(periodo.Grupo.Id, ct)).Where(a => publicadas.Contains(a.Id)).ToList();
        var tutores = await plan.TutoresAsync(alumnos, ct);
        var suscripciones = (await avisos.SuscripcionesAsync(tutores.Where(t => t.Push).Select(t => t.TutorId).ToList(), ct)).ToLookup(s => s.TutorId);
        var mensajes = (await avisos.MensajesAsync(periodoId, PlantillasWhatsapp.BoletaDisponible, ct))
            .Where(m => m.Estado == EstadoMensaje.Pendiente)
            .GroupBy(m => m.TutorId)
            .ToDictionary(g => g.Key, g => g.First());
        var unidadNombre = periodo.Unidad.Nombre;

        var fallas = 0;
        foreach (var tutor in tutores)
        {
            var hijos = TextosAvisos.Hijos(tutor.Alumnos);
            var envio = new Envio(
                new MensajePush(
                    $"Reporte del {unidadNombre}",
                    $"Ya está disponible en Aula Lista ({TextosAvisos.Grupo(periodo.Grupo)}).",
                    TextosAvisos.UrlPortal(cuentas, $"/tutor/alumnos/{tutor.Alumnos[0].Id}")),
                $"alumno={tutor.Alumnos[0].Id}",
                url => CorreosAvisos.Boleta(tutor.PrimerNombre, hijos, unidadNombre, url),
                PlantillasWhatsapp.BoletaDisponible,
                url => [tutor.PrimerNombre, hijos, unidadNombre, url]);
            fallas += await envios.EnviarAsync(
                tutor, periodo.Grupo, envio, suscripciones[tutor.TutorId], true, mensajes.GetValueOrDefault(tutor.TutorId), ct);
        }

        await unidad.GuardarAsync(ct);
        return fallas;
    }
}

/// <summary>RF-072: el docente comparte una boleta publicada desde su WhatsApp con un enlace personal del tutor.</summary>
public sealed class CompartirBoletaCasoUso(
    IRepositorioGrupos grupos, IRepositorioBoletas boletas, EmisionEnlaces emision, IUnidadDeTrabajo unidad)
{
    public async Task<EnlaceDto?> EjecutarAsync(Guid boletaId, Guid tutorId, CancellationToken ct)
    {
        if (await boletas.BoletaAsync(boletaId, ct) is not { EstaPublicada: true } boleta
            || await grupos.AlumnoAsync(boleta.AlumnoId, ct) is not { } alumno
            || !(await grupos.VinculosAsync([alumno.Id], ct)).Any(v => v.TutorId == tutorId)
            || await grupos.GrupoAsync(alumno.GrupoId, ct) is not { } grupo)
        {
            return null;
        }

        var (_, enlace) = await emision.EmitirAsync(tutorId, alumno, grupo, $"alumno={alumno.Id}", ct);
        await unidad.GuardarAsync(ct);
        return enlace;
    }
}

/// <summary>
/// Webhook de estados de Meta (SPEC-07). Sin firma válida no se toca nada (401); los estados solo avanzan, así que
/// un reintento o un evento fuera de orden no hace daño (idempotente).
/// </summary>
public sealed class WebhookWhatsappCasoUso(IRepositorioAvisos avisos, IOptions<OpcionesWhatsapp> opciones)
{
    private static readonly Dictionary<string, string> Estados = new(StringComparer.Ordinal)
    {
        ["sent"] = EstadoMensaje.Enviado,
        ["delivered"] = EstadoMensaje.Entregado,
        ["read"] = EstadoMensaje.Leido,
        ["failed"] = EstadoMensaje.Fallido,
    };

    /// <summary>Nulo si la firma no es válida; si no, cuántos mensajes cambiaron.</summary>
    public async Task<int?> EjecutarAsync(byte[] cuerpo, string? firma, CancellationToken ct)
    {
        if (!FirmaValida(cuerpo, firma, opciones.Value.SecretoApp))
        {
            return null;
        }

        var cambios = 0;
        foreach (var (id, estado) in LeerEstados(cuerpo))
        {
            if (await avisos.ActualizarEstadoWhatsappAsync(id, estado, ct))
            {
                cambios++;
            }
        }

        return cambios;
    }

    /// <summary>El registro del webhook en Meta: regresa el reto solo si el token coincide.</summary>
    public string? Verificar(string? modo, string? token, string? reto)
    {
        var esperado = opciones.Value.TokenVerificacion;
        return modo == "subscribe" && !string.IsNullOrEmpty(esperado) && !string.IsNullOrEmpty(token)
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(esperado))
            ? reto
            : null;
    }

    /// <summary><c>X-Hub-Signature-256: sha256=&lt;hex&gt;</c>, HMAC-SHA256 del cuerpo tal cual con el secreto de la app.</summary>
    public static bool FirmaValida(byte[] cuerpo, string? firma, string? secreto)
    {
        const string Prefijo = "sha256=";
        if (string.IsNullOrEmpty(secreto) || firma is null || !firma.StartsWith(Prefijo, StringComparison.Ordinal))
        {
            return false;
        }

        byte[] recibida;
        try
        {
            recibida = Convert.FromHexString(firma.AsSpan(Prefijo.Length));
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secreto), cuerpo), recibida);
    }

    private static List<(string Id, string Estado)> LeerEstados(byte[] cuerpo)
    {
        var estados = new List<(string, string)>();
        try
        {
            using var json = JsonDocument.Parse(cuerpo);
            foreach (var entrada in Arreglo(json.RootElement, "entry"))
            {
                foreach (var cambio in Arreglo(entrada, "changes"))
                {
                    if (!cambio.TryGetProperty("value", out var valor))
                    {
                        continue;
                    }

                    foreach (var s in Arreglo(valor, "statuses"))
                    {
                        if (s.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                            && s.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String
                            && Estados.TryGetValue(st.GetString()!, out var estado))
                        {
                            estados.Add((id.GetString()!, estado));
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Firmado pero ilegible: no hay nada que cambiar.
        }

        return estados;
    }

    private static List<JsonElement> Arreglo(JsonElement elemento, string propiedad) =>
        elemento.ValueKind == JsonValueKind.Object && elemento.TryGetProperty(propiedad, out var arreglo) && arreglo.ValueKind == JsonValueKind.Array
            ? [.. arreglo.EnumerateArray()]
            : [];
}
