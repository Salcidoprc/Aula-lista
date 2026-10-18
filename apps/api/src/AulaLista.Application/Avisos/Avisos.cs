using AulaLista.Application.Comun;
using AulaLista.Application.Cuentas;
using AulaLista.Application.Grupos;
using AulaLista.Application.Tutores;
using AulaLista.Domain.Avisos;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Grupos;
using Microsoft.Extensions.Options;

namespace AulaLista.Application.Avisos;

public sealed record AvisoResumenDto(Guid Id, string Titulo, bool Urgente, DateTimeOffset CreadoEn, int Destinatarios, int Leidos);

/// <summary>Avisos del grupo, del más nuevo al más viejo (SPEC-07, pantalla Avisos).</summary>
public sealed record AvisosDto(Guid GrupoId, bool SoloLectura, int Tutores, IReadOnlyList<AvisoResumenDto> Avisos);

public sealed record AlumnoDeTutorDto(Guid Id, string Nombres, string Apellidos);

/// <summary>Un tutor al que se le puede mandar un aviso, con sus canales y su saldo de WhatsApp urgente (RN-086).</summary>
public sealed record DestinatarioDto(
    Guid TutorId, string Nombre, string? Parentesco, IReadOnlyList<AlumnoDeTutorDto> Alumnos, bool Push, bool Correo, int UrgentesRestantes);

public sealed record DestinatariosDto(Guid GrupoId, bool SoloLectura, int UrgentesPorTutor, IReadOnlyList<DestinatarioDto> Tutores);

/// <summary>RN-070, RN-071: sin <see cref="TutorIds"/> va a todos los tutores del grupo.</summary>
public sealed record CrearAviso(string? Titulo, string? Cuerpo, bool Urgente, IReadOnlyList<Guid>? TutorIds);

/// <summary>Cómo le llegó el aviso a un tutor. Sin vínculo vigente con el docente, el nombre ya no se muestra.</summary>
public sealed record EntregaDto(
    Guid TutorId,
    string? Nombre,
    string? Telefono,
    IReadOnlyList<AlumnoDeTutorDto> Alumnos,
    bool Push,
    bool Correo,
    bool Whatsapp,
    string? EstadoWhatsapp,
    bool SinSaldoWhatsapp,
    bool PorCompartir,
    DateTimeOffset? CompartidoEn,
    DateTimeOffset? LeidoEn);

public sealed record AvisoDto(
    Guid Id,
    Guid GrupoId,
    string Titulo,
    string Cuerpo,
    bool Urgente,
    bool ParaTodos,
    DateTimeOffset CreadoEn,
    int Destinatarios,
    int Leidos,
    IReadOnlyList<EntregaDto> Entregas);

public static class AccionesAuditoriaAvisos
{
    public const string AvisoEnviado = "aviso_enviado";
    public const string AvisoBorrado = "aviso_borrado";
}

/// <summary>La pestaña Avisos del grupo.</summary>
public sealed class AvisosDelGrupoCasoUso(IRepositorioGrupos grupos, IRepositorioAvisos avisos)
{
    public async Task<AvisosDto?> EjecutarAsync(Guid grupoId, CancellationToken ct)
    {
        if (await grupos.GrupoAsync(grupoId, ct) is not { } grupo)
        {
            return null;
        }

        var activos = (await grupos.AlumnosAsync(grupoId, ct)).Where(a => a.EstaActivo).Select(a => a.Id).ToList();
        var tutores = activos.Count == 0 ? 0 : (await grupos.VinculosAsync(activos, ct)).Select(v => v.TutorId).Distinct().Count();
        var lista = (await avisos.AvisosAsync(grupoId, ct))
            .Select(a => new AvisoResumenDto(a.Aviso.Id, a.Aviso.Titulo, a.Aviso.Urgente, a.Aviso.CreatedAt, a.Destinatarios, a.Leidos))
            .ToList();
        return new AvisosDto(grupoId, grupo.EstaArchivado, tutores, lista);
    }
}

/// <summary>Para "Nuevo aviso": los tutores de los alumnos activos, sus canales y cuánto WhatsApp urgente les queda.</summary>
public sealed class DestinatariosAvisoCasoUso(IRepositorioGrupos grupos, PlanEntregas plan)
{
    public async Task<DestinatariosDto?> EjecutarAsync(Guid grupoId, CancellationToken ct)
    {
        if (await grupos.GrupoAsync(grupoId, ct) is not { } grupo)
        {
            return null;
        }

        var tutores = await plan.TutoresAsync((await grupos.AlumnosAsync(grupoId, ct)).Where(a => a.EstaActivo).ToList(), ct);
        var restantes = await plan.UrgentesRestantesAsync(grupoId, ct);
        return new DestinatariosDto(
            grupoId,
            grupo.EstaArchivado,
            plan.Opciones.UrgentesPorTutorPorMes,
            tutores.Select(t => new DestinatarioDto(
                t.TutorId, t.Nombre, t.Parentesco, Alumnos(t.Alumnos), t.Push, t.Correo is not null, restantes(t.TutorId))).ToList());
    }

    internal static IReadOnlyList<AlumnoDeTutorDto> Alumnos(IEnumerable<Alumno> alumnos) =>
        alumnos.Select(a => new AlumnoDeTutorDto(a.Id, a.Nombres, a.Apellidos)).ToList();
}

/// <summary>
/// RF-070, RF-071: crea el aviso y decide por dónde le llega a cada tutor (RN-072). El envío sale en un trabajo al
/// confirmarse; los WhatsApp quedan apartados desde ya para que dos avisos seguidos no pasen el límite (RN-086).
/// </summary>
public sealed class CrearAvisoCasoUso(
    IRepositorioGrupos grupos, IRepositorioAvisos avisos, PlanEntregas plan, DetalleAvisoCasoUso detalle, IColaTrabajos cola,
    IAuditoria auditoria, IContextoUsuario usuario, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<AvisoDto>?> EjecutarAsync(Guid grupoId, CrearAviso comando, CancellationToken ct)
    {
        if (await grupos.GrupoAsync(grupoId, ct) is not { } grupo)
        {
            return null;
        }

        if (grupo.EstaArchivado)
        {
            return Resultado.Fallo<AvisoDto>(ErroresGrupos.GrupoArchivado);
        }

        var validado = Aviso.Validar(comando.Titulo, comando.Cuerpo);
        if (validado.EsFallo)
        {
            return Resultado.Fallo<AvisoDto>(validado.Error);
        }

        var (titulo, cuerpo) = validado.Valor;
        var todos = await plan.TutoresAsync((await grupos.AlumnosAsync(grupoId, ct)).Where(a => a.EstaActivo).ToList(), ct);
        var elegidos = comando.TutorIds?.ToHashSet();
        var destinatarios = elegidos is null ? todos : todos.Where(t => elegidos.Contains(t.TutorId)).ToList();
        if (destinatarios.Count == 0 || (elegidos is not null && elegidos.Count != destinatarios.Count))
        {
            return Resultado.Fallo<AvisoDto>(ErroresAvisos.SinDestinatarios);
        }

        var ahora = reloj.GetUtcNow();
        var aviso = new Aviso(grupoId, grupo.DocenteId, titulo, cuerpo, comando.Urgente, elegidos is null, ahora);
        avisos.Agregar(aviso);
        var restantes = await plan.UrgentesRestantesAsync(grupoId, ct);
        var entregas = destinatarios.Select(t =>
        {
            var whatsapp = comando.Urgente && restantes(t.TutorId) > 0;
            if (whatsapp)
            {
                avisos.Agregar(plan.NuevoMensaje(grupo, t.TutorId, PlantillasWhatsapp.AvisoUrgente, aviso.Id));
            }

            return new EntregaAviso(aviso, t.TutorId, new Canales(t.Push, t.Correo is not null, whatsapp), comando.Urgente && !whatsapp, ahora);
        }).ToList();
        avisos.Agregar(entregas);
        auditoria.Registrar(AccionesAuditoriaAvisos.AvisoEnviado, "aviso", aviso.Id, usuario.DocenteId, entregas.Count);
        await unidad.GuardarAsync(ct);
        cola.EncolarAviso(grupo.DocenteId, aviso.Id);
        return Resultado.Exito((await detalle.EjecutarAsync(aviso.Id, ct))!);
    }
}

/// <summary>RF-073: el aviso con su estado por tutor. Un aviso ajeno o borrado es 404.</summary>
public sealed class DetalleAvisoCasoUso(IRepositorioGrupos grupos, IRepositorioAvisos avisos, PlanEntregas plan)
{
    public async Task<AvisoDto?> EjecutarAsync(Guid avisoId, CancellationToken ct)
    {
        if (await avisos.AvisoAsync(avisoId, ct) is not { BorradoEn: null } aviso)
        {
            return null;
        }

        var entregas = await avisos.EntregasAsync(avisoId, ct);
        var tutores = (await plan.TutoresAsync(await grupos.AlumnosAsync(aviso.GrupoId, ct), ct)).ToDictionary(t => t.TutorId);
        var mensajes = (await avisos.MensajesAsync(avisoId, PlantillasWhatsapp.AvisoUrgente, ct)).ToDictionary(m => m.TutorId);
        var lista = entregas
            .Select(e =>
            {
                var tutor = tutores.GetValueOrDefault(e.TutorId);
                return new EntregaDto(
                    e.TutorId, tutor?.Nombre, tutor?.TelefonoE164, DestinatariosAvisoCasoUso.Alumnos(tutor?.Alumnos ?? []), e.Push, e.Correo,
                    e.Whatsapp, mensajes.GetValueOrDefault(e.TutorId)?.Estado, e.SinSaldoWhatsapp, e.PorCompartir, e.CompartidoEn, e.LeidoEn);
            })
            .OrderBy(e => e.Nombre is null ? 1 : 0)
            .ThenBy(e => tutores.GetValueOrDefault(e.TutorId)?.Alumnos[0].NumeroLista ?? int.MaxValue)
            .ThenBy(e => e.Nombre, StringComparer.CurrentCulture)
            .ToList();
        return new AvisoDto(
            aviso.Id, aviso.GrupoId, aviso.Titulo, aviso.Cuerpo, aviso.Urgente, aviso.ParaTodos, aviso.CreatedAt, entregas.Count,
            entregas.Count(e => e.LeidoEn is not null), lista);
    }
}

/// <summary>RN-076: el aviso no se edita; borrarlo lo quita del portal y queda en auditoría.</summary>
public sealed class BorrarAvisoCasoUso(
    IRepositorioAvisos avisos, IAuditoria auditoria, IContextoUsuario usuario, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<bool> EjecutarAsync(Guid avisoId, CancellationToken ct)
    {
        if (await avisos.AvisoAsync(avisoId, ct) is not { BorradoEn: null } aviso)
        {
            return false;
        }

        aviso.Borrar(reloj.GetUtcNow());
        auditoria.Registrar(AccionesAuditoriaAvisos.AvisoBorrado, "aviso", aviso.Id, usuario.DocenteId);
        await unidad.GuardarAsync(ct);
        return true;
    }
}

/// <summary>
/// RN-073, RF-072: el docente comparte el aviso desde su WhatsApp. Se crea un enlace personal que abre el portal en
/// los avisos de su hijo, y la entrega queda como compartida.
/// </summary>
public sealed class CompartirAvisoCasoUso(
    IRepositorioGrupos grupos, IRepositorioAvisos avisos, PlanEntregas plan, EmisionEnlaces emision, IUnidadDeTrabajo unidad,
    TimeProvider reloj)
{
    public async Task<EnlaceDto?> EjecutarAsync(Guid avisoId, Guid tutorId, CancellationToken ct)
    {
        if (await avisos.AvisoAsync(avisoId, ct) is not { BorradoEn: null } aviso
            || (await avisos.EntregasAsync(avisoId, ct)).FirstOrDefault(e => e.TutorId == tutorId) is not { } entrega
            || await grupos.GrupoAsync(aviso.GrupoId, ct) is not { } grupo
            || (await plan.TutoresAsync(await grupos.AlumnosAsync(grupo.Id, ct), ct)).FirstOrDefault(t => t.TutorId == tutorId) is not { } tutor)
        {
            return null;
        }

        var alumno = tutor.Alumnos.FirstOrDefault(a => a.EstaActivo) ?? tutor.Alumnos[0];
        var (_, enlace) = await emision.EmitirAsync(tutorId, alumno, grupo, $"alumno={alumno.Id}&ver=avisos", ct);
        entrega.MarcarCompartido(reloj.GetUtcNow());
        await unidad.GuardarAsync(ct);
        return enlace;
    }
}

/// <summary>
/// RF-071: envía un aviso ya planeado. Corre como el docente; regresa cuántos envíos fallaron para la bitácora.
/// El push abre el portal directo (ese navegador ya tiene sesión); correo y WhatsApp llevan un enlace nuevo.
/// </summary>
public sealed class EnviarAvisoTrabajo(
    IRepositorioGrupos grupos, IRepositorioAvisos avisos, IRepositorioDocentes docentes, PlanEntregas plan, EnviosATutores envios,
    IUnidadDeTrabajo unidad, IOptions<OpcionesCuentas> cuentas)
{
    public async Task<int> EjecutarAsync(Guid avisoId, CancellationToken ct)
    {
        if (await avisos.AvisoAsync(avisoId, ct) is not { BorradoEn: null } aviso || await grupos.GrupoAsync(aviso.GrupoId, ct) is not { } grupo)
        {
            return 0;
        }

        var entregas = await avisos.EntregasAsync(avisoId, ct);
        var tutores = (await plan.TutoresAsync(await grupos.AlumnosAsync(grupo.Id, ct), ct)).ToDictionary(t => t.TutorId);
        var suscripciones = (await avisos.SuscripcionesAsync(entregas.Where(e => e.Push).Select(e => e.TutorId).ToList(), ct)).ToLookup(s => s.TutorId);
        var mensajes = (await avisos.MensajesAsync(avisoId, PlantillasWhatsapp.AvisoUrgente, ct))
            .Where(m => m.Estado == EstadoMensaje.Pendiente)
            .ToDictionary(m => m.TutorId);
        var docente = (await docentes.PorIdAsync(grupo.DocenteId, ct))?.Nombre;
        var nombreGrupo = TextosAvisos.Grupo(grupo);

        var fallas = 0;
        foreach (var entrega in entregas)
        {
            if (!tutores.TryGetValue(entrega.TutorId, out var tutor))
            {
                continue;
            }

            var alumno = tutor.Alumnos.FirstOrDefault(a => a.EstaActivo) ?? tutor.Alumnos[0];
            var envio = new Envio(
                new MensajePush(
                    aviso.Urgente ? $"Aviso urgente · {nombreGrupo}" : $"Aviso · {nombreGrupo}",
                    aviso.Titulo,
                    TextosAvisos.UrlPortal(cuentas, $"/tutor/alumnos/{alumno.Id}?ver=avisos")),
                $"alumno={alumno.Id}&ver=avisos",
                url => CorreosAvisos.Aviso(tutor.PrimerNombre, nombreGrupo, docente, aviso, url),
                PlantillasWhatsapp.AvisoUrgente,
                url => [tutor.PrimerNombre, nombreGrupo, url]);
            fallas += await envios.EnviarAsync(
                tutor, grupo, envio, entrega.Push ? suscripciones[tutor.TutorId] : [], entrega.Correo, mensajes.GetValueOrDefault(tutor.TutorId), ct);
        }

        await unidad.GuardarAsync(ct);
        return fallas;
    }
}
