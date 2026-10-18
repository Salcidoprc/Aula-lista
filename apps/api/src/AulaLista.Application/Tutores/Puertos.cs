using System.ComponentModel.DataAnnotations;
using AulaLista.Domain.Boletas;
using AulaLista.Domain.Tutores;

namespace AulaLista.Application.Tutores;

/// <summary>Vigencias y límites del portal de tutores (SPEC-06). Viven en configuración (reglas/01).</summary>
public sealed class OpcionesTutores
{
    public const string Seccion = "Tutores";

    /// <summary>RN-062: la sesión del navegador dura esto y se renueva con el uso.</summary>
    [Range(1, 365)]
    public int DiasSesion { get; init; } = 90;

    /// <summary>RN-062: el enlace vence al terminar el ciclo escolar más estos días.</summary>
    [Range(0, 365)]
    public int DiasGraciaEnlace { get; init; } = 30;

    /// <summary>RN-065: más de este número de dispositivos en la ventana marca el enlace como sospechoso.</summary>
    [Range(1, 100)]
    public int DispositivosPorEnlace { get; init; } = 5;

    [Range(1, 365)]
    public int DiasVentanaDispositivos { get; init; } = 30;

    /// <summary>Canjes de enlace por minuto por IP.</summary>
    [Range(1, 1000)]
    public int CanjesPorMinuto { get; init; } = 10;
}

/// <summary>
/// Enlaces y sesiones del tutor, con RLS: los ve el tutor dueño y el docente que tiene un vínculo con él. Antes de
/// saber quién es el tutor solo se puede preguntar de quién es un hash conocido (<c>tutor_del_enlace</c> y
/// <c>tutor_de_la_sesion</c>); el resto se lee después de asumir a ese tutor.
/// </summary>
public interface IRepositorioEnlaces
{
    Task<Guid?> TutorDelEnlaceAsync(string tokenHash, CancellationToken cancellationToken);

    Task<Guid?> TutorDeLaSesionAsync(string tokenHash, CancellationToken cancellationToken);

    Task<EnlaceTutor?> EnlacePorHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<EnlaceTutor>> EnlacesAsync(Guid tutorId, CancellationToken cancellationToken);

    Task<SesionConEnlace?> SesionPorHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<SesionTutor>> SesionesAsync(Guid tutorId, CancellationToken cancellationToken);

    /// <summary>Sesiones abiertas con el enlace desde esa fecha: cada una es un dispositivo (RN-065).</summary>
    Task<int> SesionesDesdeAsync(Guid enlaceId, DateTimeOffset desde, CancellationToken cancellationToken);

    void Agregar(EnlaceTutor enlace);

    void Agregar(SesionTutor sesion);
}

public sealed record SesionConEnlace(SesionTutor Sesion, EnlaceTutor Enlace);

/// <summary>Un hijo del tutor con su grupo y su docente (RN-061).</summary>
public sealed record HijoTutor(
    Guid AlumnoId, string Nombres, string Apellidos, DateTimeOffset? BajaEn, string Nivel, int Grado, string Grupo, string? Materia,
    string? Docente);

/// <summary>Una boleta publicada y vigente con la fecha de inicio de su unidad, para ordenarlas.</summary>
public sealed record BoletaDeHijo(Boleta Boleta, DateOnly InicioUnidad);

/// <summary>
/// Lecturas del portal con <c>app.tutor_id</c>: RLS solo deja ver lo vinculado al tutor y, de las boletas, solo las
/// publicadas y vigentes (RN-064). Las consultas filtran además por el tutor de forma explícita.
/// </summary>
public interface IConsultasTutor
{
    Task<string?> NombreAsync(Guid tutorId, CancellationToken cancellationToken);

    Task<IReadOnlyList<HijoTutor>> HijosAsync(Guid tutorId, CancellationToken cancellationToken);

    Task<IReadOnlyList<BoletaDeHijo>> BoletasAsync(Guid tutorId, IReadOnlyCollection<Guid> alumnoIds, CancellationToken cancellationToken);

    Task<Boleta?> BoletaAsync(Guid tutorId, Guid boletaId, CancellationToken cancellationToken);
}
