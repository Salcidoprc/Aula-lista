using AulaLista.Application.Avisos;
using AulaLista.Application.Boletas;
using AulaLista.Application.Comun;
using AulaLista.Domain.Boletas;

namespace AulaLista.Application.Tutores;

/// <summary>Lo que el tutor ve de una boleta publicada (RN-064): la foto de lo emitido, sin datos de actividades.</summary>
public sealed record BoletaPublicadaDto(
    Guid Id,
    string Unidad,
    string Ciclo,
    decimal? Calificacion,
    int DecimalesFinal,
    IReadOnlyList<RubroBoleta> Rubros,
    string Observacion,
    int Version,
    string Codigo,
    DateTimeOffset PublicadaEn,
    string? Escuela)
{
    public static BoletaPublicadaDto De(Boleta b) => new(
        b.Id, b.Datos.Unidad, b.Datos.Ciclo, b.Datos.Calificacion, b.Datos.DecimalesFinal, b.Datos.Rubros, b.Datos.Observacion,
        b.Version, b.Codigo, b.PublicadaEn!.Value, b.Datos.Escuela);
}

/// <summary>La boleta más reciente, para la tarjeta de "Mis hijos".</summary>
public sealed record ResumenBoletaDto(Guid Id, string Unidad, decimal? Calificacion, int DecimalesFinal, DateTimeOffset PublicadaEn);

public sealed record HijoDto(
    Guid Id,
    string Nombres,
    string Apellidos,
    string Nivel,
    int Grado,
    string Grupo,
    string? Materia,
    string? Docente,
    bool DadoDeBaja,
    ResumenBoletaDto? UltimaBoleta,
    ResumenAvisoDto? UltimoAviso,
    int AvisosSinLeer);

/// <summary>RN-061: todos los hijos vinculados al teléfono del tutor, de cualquier docente.</summary>
public sealed record MisHijosDto(string? Nombre, IReadOnlyList<HijoDto> Hijos);

/// <summary>Detalle de un hijo con sus boletas publicadas, de la más nueva a la más vieja.</summary>
public sealed record DetalleHijoDto(HijoDto Hijo, IReadOnlyList<BoletaPublicadaDto> Boletas);

/// <summary>"Mis hijos" (SPEC-06, pantalla 1).</summary>
public sealed class MisHijosCasoUso(IConsultasTutor consultas, IAvisosDelTutor avisos, IContextoUsuario usuario)
{
    public async Task<MisHijosDto?> EjecutarAsync(CancellationToken ct)
    {
        if (usuario.TutorId is not { } tutorId)
        {
            return null;
        }

        var hijos = await consultas.HijosAsync(tutorId, ct);
        var boletas = (await consultas.BoletasAsync(tutorId, hijos.Select(h => h.AlumnoId).ToList(), ct)).ToLookup(b => b.Boleta.AlumnoId);
        var deGrupo = (await avisos.AvisosAsync(tutorId, ct)).ToLookup(a => a.Aviso.GrupoId);
        var lista = hijos
            .OrderBy(h => h.BajaEn is null ? 0 : 1)
            .ThenBy(h => h.Nombres, StringComparer.CurrentCulture)
            .ThenBy(h => h.Grado)
            .Select(h => PortalTutor.Hijo(h, PortalTutor.Ordenadas(boletas[h.AlumnoId]).FirstOrDefault(), deGrupo[h.GrupoId].ToList()))
            .ToList();
        return new MisHijosDto(await consultas.NombreAsync(tutorId, ct), lista);
    }
}

/// <summary>Detalle del alumno (SPEC-06, pantalla 2). Un alumno no vinculado al tutor es 404.</summary>
public sealed class DetalleHijoCasoUso(IConsultasTutor consultas, IAvisosDelTutor avisos, IContextoUsuario usuario)
{
    public async Task<DetalleHijoDto?> EjecutarAsync(Guid alumnoId, CancellationToken ct)
    {
        if (usuario.TutorId is not { } tutorId
            || (await consultas.HijosAsync(tutorId, ct)).FirstOrDefault(h => h.AlumnoId == alumnoId) is not { } hijo)
        {
            return null;
        }

        var boletas = PortalTutor.Ordenadas(await consultas.BoletasAsync(tutorId, [alumnoId], ct)).ToList();
        var suyos = (await avisos.AvisosAsync(tutorId, ct)).Where(a => a.Aviso.GrupoId == hijo.GrupoId).ToList();
        return new DetalleHijoDto(PortalTutor.Hijo(hijo, boletas.FirstOrDefault(), suyos), boletas.Select(BoletaPublicadaDto.De).ToList());
    }
}

/// <summary>El PDF de una boleta para el tutor: solo publicada, vigente y de un hijo suyo; si no, 404.</summary>
public sealed class PdfBoletaTutorCasoUso(IConsultasTutor consultas, IContextoUsuario usuario)
{
    public async Task<ArchivoBoleta?> EjecutarAsync(Guid boletaId, CancellationToken ct) =>
        usuario.TutorId is { } tutorId && await consultas.BoletaAsync(tutorId, boletaId, ct) is { PdfRuta: { } ruta } boleta
            ? new ArchivoBoleta(null, ruta, $"boleta-{Archivo(boleta.Datos.Unidad)}-v{boleta.Version}.pdf")
            : null;

    private static string Archivo(string texto) =>
        new([.. texto.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')]);
}

internal static class PortalTutor
{
    public static IEnumerable<Boleta> Ordenadas(IEnumerable<BoletaDeHijo> boletas) =>
        boletas.OrderByDescending(b => b.InicioUnidad).ThenByDescending(b => b.Boleta.PublicadaEn).Select(b => b.Boleta);

    /// <param name="avisos">Los avisos del grupo del hijo, del más nuevo al más viejo.</param>
    public static HijoDto Hijo(HijoTutor h, Boleta? ultima, IReadOnlyList<AvisoDelTutor> avisos) => new(
        h.AlumnoId, h.Nombres, h.Apellidos, h.Nivel, h.Grado, h.Grupo, h.Materia, h.Docente, h.BajaEn is not null,
        ultima is null ? null : new ResumenBoletaDto(ultima.Id, ultima.Datos.Unidad, ultima.Datos.Calificacion, ultima.Datos.DecimalesFinal, ultima.PublicadaEn!.Value),
        avisos.Count == 0 ? null : new ResumenAvisoDto(avisos[0].Aviso.Id, avisos[0].Aviso.Titulo, avisos[0].Aviso.CreatedAt, avisos[0].Entrega.LeidoEn is not null),
        avisos.Count(a => a.Entrega.LeidoEn is null));
}
