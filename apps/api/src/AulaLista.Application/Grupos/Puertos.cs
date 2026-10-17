using AulaLista.Domain.Grupos;

namespace AulaLista.Application.Grupos;

/// <summary>Ciclos, grupos, periodos, alumnos e importaciones: tablas con RLS por docente.</summary>
public interface IRepositorioGrupos
{
    Task<IReadOnlyList<CicloEscolar>> CiclosAsync(CancellationToken cancellationToken);

    Task<CicloEscolar?> CicloAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExisteCicloAsync(string nombre, Guid? excepto, CancellationToken cancellationToken);

    void Agregar(CicloEscolar ciclo);

    /// <summary>Unidades del ciclo que ya están cerradas en algún grupo (RN-026).</summary>
    Task<IReadOnlySet<Guid>> UnidadesCerradasAsync(Guid cicloId, CancellationToken cancellationToken);

    Task<IReadOnlyList<GrupoConConteo>> GruposAsync(bool archivados, CancellationToken cancellationToken);

    Task<Grupo?> GrupoAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lee el grupo con <c>FOR UPDATE</c> para que dos altas simultáneas no pasen el límite (RN-011).</summary>
    Task<Grupo?> GrupoParaInscribirAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Grupo>> GruposDelCicloAsync(Guid cicloId, CancellationToken cancellationToken);

    /// <summary>Nombres (en minúsculas) con su materia de los grupos del ciclo, para evitar duplicados.</summary>
    Task<IReadOnlyList<(int Grado, string Nombre, string? Materia)>> NombresDeGruposAsync(Guid cicloId, Guid? excepto, CancellationToken cancellationToken);

    void Agregar(Grupo grupo);

    void Borrar(Grupo grupo);

    void Agregar(IEnumerable<Periodo> periodos);

    Task<IReadOnlyList<PeriodoDeGrupo>> PeriodosAsync(Guid grupoId, CancellationToken cancellationToken);

    /// <summary>El periodo con su unidad y su grupo, o nulo si no es del docente.</summary>
    Task<PeriodoCompleto?> PeriodoAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Alumno>> AlumnosAsync(Guid grupoId, CancellationToken cancellationToken);

    Task<Alumno?> AlumnoAsync(Guid id, CancellationToken cancellationToken);

    void Agregar(IEnumerable<Alumno> alumnos);

    Task<IReadOnlyList<VinculoTutor>> VinculosAsync(IReadOnlyCollection<Guid> alumnoIds, CancellationToken cancellationToken);

    /// <summary>Si el docente ya tiene algún vínculo con ese tutor (ADR-0014: con hermanos no se avisa).</summary>
    Task<bool> TieneVinculoConAsync(Guid tutorId, CancellationToken cancellationToken);

    void Agregar(VinculoTutor vinculo);

    void Borrar(VinculoTutor vinculo);

    void Agregar(Importacion importacion);

    Task<Importacion?> ImportacionAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Lo que Grupos necesita saber de la evaluación sin leer sus tablas (lo implementa Evaluación).</summary>
public interface IDatosDeEvaluacion
{
    /// <summary>Unidades del ciclo con rubros en algún grupo (RN-027: no se pueden borrar).</summary>
    Task<IReadOnlySet<Guid>> UnidadesConDatosAsync(Guid cicloId, CancellationToken cancellationToken);

    /// <summary>Si el grupo tiene alguna calificación capturada (SPEC-02: entonces no se borra).</summary>
    Task<bool> GrupoTieneCalificacionesAsync(Guid grupoId, CancellationToken cancellationToken);
}

public sealed record GrupoConConteo(Grupo Grupo, string CicloNombre, int AlumnosActivos);

public sealed record PeriodoDeGrupo(Periodo Periodo, Unidad Unidad);

public sealed record PeriodoCompleto(Periodo Periodo, Unidad Unidad, Grupo Grupo);

/// <summary>Tutores compartidos entre docentes (ADR-0014): solo se vinculan por la función de la base.</summary>
public interface ITutores
{
    /// <summary>Id del tutor con ese teléfono, creándolo si no existe, y si ya existía.</summary>
    Task<(Guid TutorId, bool YaExistia)> VincularAsync(string telefonoE164, string nombre, string? correo, CancellationToken cancellationToken);
}

/// <summary>Lee .xlsx y .csv como tabla de texto y genera la plantilla de importación (RF-013).</summary>
public interface IHojasDeCalculo
{
    /// <summary>La primera hoja con encabezados en la primera fila no vacía, o nulo si no se puede leer.</summary>
    TablaCruda? Leer(Stream archivo, string origen);

    byte[] Plantilla();
}

public sealed record TablaCruda(IReadOnlyList<string> Encabezados, IReadOnlyList<IReadOnlyList<string>> Filas);
