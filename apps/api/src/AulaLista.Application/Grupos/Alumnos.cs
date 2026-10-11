using System.Net.Mail;
using AulaLista.Application.Comun;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Grupos;
using FluentValidation;

namespace AulaLista.Application.Grupos;

public sealed record TutorDto(Guid Id, string Nombre, string Telefono, string? Correo, string? Parentesco)
{
    public static TutorDto De(VinculoTutor v) => new(v.TutorId, v.Nombre, v.TelefonoE164, v.Correo, v.Parentesco);
}

public sealed record AlumnoDto(
    Guid Id,
    Guid GrupoId,
    int NumeroLista,
    string Nombres,
    string Apellidos,
    string Trato,
    DateTimeOffset? BajaEn,
    IReadOnlyList<TutorDto> Tutores)
{
    public static AlumnoDto De(Alumno a, IEnumerable<VinculoTutor> vinculos) => new(
        a.Id, a.GrupoId, a.NumeroLista, a.Nombres, a.Apellidos, a.Trato, a.BajaEn,
        vinculos.Where(v => v.AlumnoId == a.Id).OrderBy(v => v.CreatedAt).Select(TutorDto.De).ToList());
}

/// <summary>Lista del grupo por número: activos primero y después las bajas.</summary>
public sealed class ListarAlumnosCasoUso(IRepositorioGrupos repositorio)
{
    public async Task<IReadOnlyList<AlumnoDto>?> EjecutarAsync(Guid grupoId, CancellationToken ct)
    {
        if (await repositorio.GrupoAsync(grupoId, ct) is null)
        {
            return null;
        }

        var alumnos = await repositorio.AlumnosAsync(grupoId, ct);
        var vinculos = await repositorio.VinculosAsync(alumnos.Select(a => a.Id).ToList(), ct);
        return alumnos
            .OrderBy(a => a.EstaActivo ? 0 : 1)
            .ThenBy(a => a.NumeroLista)
            .Select(a => AlumnoDto.De(a, vinculos))
            .ToList();
    }
}

public sealed class ObtenerAlumnoCasoUso(IRepositorioGrupos repositorio)
{
    public async Task<AlumnoDto?> EjecutarAsync(Guid id, CancellationToken ct)
    {
        if (await repositorio.AlumnoAsync(id, ct) is not { } alumno)
        {
            return null;
        }

        return AlumnoDto.De(alumno, await repositorio.VinculosAsync([alumno.Id], ct));
    }
}

public sealed record AgregarAlumno(string Nombres, string? Apellidos);

internal sealed class AgregarAlumnoValidador : AbstractValidator<AgregarAlumno>
{
    public AgregarAlumnoValidador()
    {
        RuleFor(c => c.Nombres).NotEmpty().MaximumLength(Alumno.LongitudNombres);
        RuleFor(c => c.Apellidos).MaximumLength(Alumno.LongitudApellidos);
    }
}

/// <summary>RF-012: alta manual, al final de la lista (RN-013) y con el límite de 50 activos (RN-011).</summary>
public sealed class AgregarAlumnoCasoUso(IRepositorioGrupos repositorio, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<AlumnoDto>?> EjecutarAsync(Guid grupoId, AgregarAlumno comando, CancellationToken ct)
    {
        if (await repositorio.GrupoParaInscribirAsync(grupoId, ct) is not { } grupo)
        {
            return null;
        }

        if (grupo.EstaArchivado)
        {
            return Resultado.Fallo<AlumnoDto>(ErroresGrupos.GrupoArchivado);
        }

        var existentes = await repositorio.AlumnosAsync(grupoId, ct);
        var inscritos = Alumno.Inscribir(grupo, existentes, [(comando.Nombres, comando.Apellidos ?? string.Empty)], reloj.GetUtcNow());
        if (inscritos.Count == 0)
        {
            return Resultado.Fallo<AlumnoDto>(ErroresGrupos.LimiteAlumnosGrupo);
        }

        repositorio.Agregar(inscritos);
        await unidad.GuardarAsync(ct);
        return Resultado.Exito(AlumnoDto.De(inscritos[0], []));
    }
}

public sealed record EditarAlumno(string Nombres, string? Apellidos, string Trato, int NumeroLista);

internal sealed class EditarAlumnoValidador : AbstractValidator<EditarAlumno>
{
    public EditarAlumnoValidador()
    {
        RuleFor(c => c.Nombres).NotEmpty().MaximumLength(Alumno.LongitudNombres);
        RuleFor(c => c.Apellidos).MaximumLength(Alumno.LongitudApellidos);
        RuleFor(c => c.Trato).NotEmpty().MaximumLength(10);
    }
}

/// <summary>RF-012, RN-013, RN-020: nombre, trato y número de lista.</summary>
public sealed class EditarAlumnoCasoUso(IRepositorioGrupos repositorio, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<AlumnoDto>?> EjecutarAsync(Guid id, EditarAlumno comando, CancellationToken ct)
    {
        if (await repositorio.AlumnoAsync(id, ct) is not { } alumno)
        {
            return null;
        }

        if (await GrupoEditableAsync(repositorio, alumno.GrupoId, ct) is { } error)
        {
            return Resultado.Fallo<AlumnoDto>(error);
        }

        var ahora = reloj.GetUtcNow();
        var resultado = alumno.Editar(comando.Nombres, comando.Apellidos ?? string.Empty, comando.Trato, ahora);
        if (resultado.EsExito && comando.NumeroLista != alumno.NumeroLista)
        {
            resultado = alumno.CambiarNumero(comando.NumeroLista, await repositorio.AlumnosAsync(alumno.GrupoId, ct), ahora);
        }

        if (resultado.EsFallo)
        {
            return Resultado.Fallo<AlumnoDto>(resultado.Error);
        }

        await unidad.GuardarAsync(ct);
        return Resultado.Exito(AlumnoDto.De(alumno, await repositorio.VinculosAsync([alumno.Id], ct)));
    }

    internal static async Task<ErrorNegocio?> GrupoEditableAsync(IRepositorioGrupos repositorio, Guid grupoId, CancellationToken ct) =>
        await repositorio.GrupoAsync(grupoId, ct) is { EstaArchivado: true } ? ErroresGrupos.GrupoArchivado : null;
}

/// <summary>RF-012, RN-017: baja lógica que conserva el historial.</summary>
public sealed class DarDeBajaAlumnoCasoUso(IRepositorioGrupos repositorio, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado?> EjecutarAsync(Guid id, CancellationToken ct)
    {
        if (await repositorio.AlumnoAsync(id, ct) is not { } alumno)
        {
            return null;
        }

        if (await EditarAlumnoCasoUso.GrupoEditableAsync(repositorio, alumno.GrupoId, ct) is { } error)
        {
            return Resultado.Fallo(error);
        }

        alumno.DarDeBaja(reloj.GetUtcNow());
        await unidad.GuardarAsync(ct);
        return Resultado.Exito();
    }
}

public sealed record VincularTutor(string Nombre, string Telefono, string? Correo, string? Parentesco);

internal sealed class VincularTutorValidador : AbstractValidator<VincularTutor>
{
    public VincularTutorValidador()
    {
        RuleFor(c => c.Nombre).NotEmpty().MaximumLength(VinculoTutor.LongitudNombre);
        RuleFor(c => c.Telefono).NotEmpty().MaximumLength(30);
        RuleFor(c => c.Correo).MaximumLength(VinculoTutor.LongitudCorreo).Must(Correos.EsValidoOVacio);
        RuleFor(c => c.Parentesco).MaximumLength(VinculoTutor.LongitudParentesco);
    }
}

public sealed record EditarTutor(string Nombre, string? Correo, string? Parentesco);

internal sealed class EditarTutorValidador : AbstractValidator<EditarTutor>
{
    public EditarTutorValidador()
    {
        RuleFor(c => c.Nombre).NotEmpty().MaximumLength(VinculoTutor.LongitudNombre);
        RuleFor(c => c.Correo).MaximumLength(VinculoTutor.LongitudCorreo).Must(Correos.EsValidoOVacio);
        RuleFor(c => c.Parentesco).MaximumLength(VinculoTutor.LongitudParentesco);
    }
}

public sealed record TutorVinculado(TutorDto Tutor, bool YaRegistrado);

public sealed class ListarTutoresCasoUso(IRepositorioGrupos repositorio)
{
    public async Task<IReadOnlyList<TutorDto>?> EjecutarAsync(Guid alumnoId, CancellationToken ct) =>
        await repositorio.AlumnoAsync(alumnoId, ct) is null
            ? null
            : (await repositorio.VinculosAsync([alumnoId], ct)).OrderBy(v => v.CreatedAt).Select(TutorDto.De).ToList();
}

/// <summary>
/// RF-016, RF-017, RN-015, RN-016: vincula un tutor por su WhatsApp. Si el número ya existe se vincula el mismo
/// tutor; el docente conserva lo que capturó y no ve datos de otros docentes (ADR-0014).
/// </summary>
public sealed class VincularTutorCasoUso(IRepositorioGrupos repositorio, ITutores tutores, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<TutorVinculado>?> EjecutarAsync(Guid alumnoId, VincularTutor comando, CancellationToken ct)
    {
        if (await repositorio.AlumnoAsync(alumnoId, ct) is not { } alumno)
        {
            return null;
        }

        var resultado = await VincularAsync(repositorio, tutores, alumno, comando, reloj.GetUtcNow(), ct);
        if (resultado.EsExito)
        {
            await unidad.GuardarAsync(ct);
        }

        return resultado;
    }

    internal static async Task<Resultado<TutorVinculado>> VincularAsync(
        IRepositorioGrupos repositorio, ITutores tutores, Alumno alumno, VincularTutor comando, DateTimeOffset ahora, CancellationToken ct)
    {
        if (await EditarAlumnoCasoUso.GrupoEditableAsync(repositorio, alumno.GrupoId, ct) is { } error)
        {
            return Resultado.Fallo<TutorVinculado>(error);
        }

        if (!alumno.EstaActivo)
        {
            return Resultado.Fallo<TutorVinculado>(ErroresGrupos.AlumnoDadoDeBaja);
        }

        if (Telefono.Normalizar(comando.Telefono) is not { } telefono)
        {
            return Resultado.Fallo<TutorVinculado>(ErroresGrupos.TelefonoInvalido);
        }

        var vinculos = await repositorio.VinculosAsync([alumno.Id], ct);
        if (vinculos.Any(v => v.TelefonoE164 == telefono))
        {
            return Resultado.Fallo<TutorVinculado>(ErroresGrupos.TutorYaVinculado);
        }

        if (vinculos.Count >= VinculoTutor.MaximoPorAlumno)
        {
            return Resultado.Fallo<TutorVinculado>(ErroresGrupos.LimiteTutoresAlumno);
        }

        var datos = new DatosTutor(comando.Nombre, comando.Correo, comando.Parentesco);
        var (tutorId, yaExistia) = await tutores.VincularAsync(telefono, datos.Nombre.Trim(), Correos.Normalizar(datos.Correo), ct);
        var yaRegistrado = yaExistia && !await repositorio.TieneVinculoConAsync(tutorId, ct);
        var vinculo = new VinculoTutor(alumno, tutorId, telefono, datos, ahora);
        repositorio.Agregar(vinculo);
        return Resultado.Exito(new TutorVinculado(TutorDto.De(vinculo), yaRegistrado));
    }
}

public sealed class EditarTutorCasoUso(IRepositorioGrupos repositorio, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<TutorDto>?> EjecutarAsync(Guid alumnoId, Guid tutorId, EditarTutor comando, CancellationToken ct)
    {
        if (await repositorio.AlumnoAsync(alumnoId, ct) is not { } alumno
            || (await repositorio.VinculosAsync([alumnoId], ct)).FirstOrDefault(v => v.TutorId == tutorId) is not { } vinculo)
        {
            return null;
        }

        if (await EditarAlumnoCasoUso.GrupoEditableAsync(repositorio, alumno.GrupoId, ct) is { } error)
        {
            return Resultado.Fallo<TutorDto>(error);
        }

        vinculo.Editar(new DatosTutor(comando.Nombre, comando.Correo, comando.Parentesco), reloj.GetUtcNow());
        await unidad.GuardarAsync(ct);
        return Resultado.Exito(TutorDto.De(vinculo));
    }
}

public sealed class DesvincularTutorCasoUso(IRepositorioGrupos repositorio, IUnidadDeTrabajo unidad)
{
    public async Task<Resultado?> EjecutarAsync(Guid alumnoId, Guid tutorId, CancellationToken ct)
    {
        if (await repositorio.AlumnoAsync(alumnoId, ct) is not { } alumno
            || (await repositorio.VinculosAsync([alumnoId], ct)).FirstOrDefault(v => v.TutorId == tutorId) is not { } vinculo)
        {
            return null;
        }

        if (await EditarAlumnoCasoUso.GrupoEditableAsync(repositorio, alumno.GrupoId, ct) is { } error)
        {
            return Resultado.Fallo(error);
        }

        repositorio.Borrar(vinculo);
        await unidad.GuardarAsync(ct);
        return Resultado.Exito();
    }
}

internal static class Correos
{
    public static bool EsValidoOVacio(string? correo) =>
        string.IsNullOrWhiteSpace(correo) || (MailAddress.TryCreate(correo.Trim(), out var direccion) && direccion.Address == correo.Trim());

    public static string? Normalizar(string? correo) => string.IsNullOrWhiteSpace(correo) ? null : correo.Trim().ToLowerInvariant();
}
