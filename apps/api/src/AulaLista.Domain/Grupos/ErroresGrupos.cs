using AulaLista.Domain.Comun;

namespace AulaLista.Domain.Grupos;

/// <summary>Errores de negocio de grupos, alumnos, tutores, ciclo y unidades (SPEC-02).</summary>
public static class ErroresGrupos
{
    public static readonly ErrorNegocio CicloFechasInvalidas = new("ciclo_fechas_invalidas", "La fecha de fin del ciclo debe ser posterior a la de inicio.");
    public static readonly ErrorNegocio CicloMuyLargo = new("ciclo_muy_largo", "El ciclo dura como máximo 13 meses.");
    public static readonly ErrorNegocio CicloDuplicado = new("ciclo_duplicado", "Ya tienes un ciclo con ese nombre.");
    public static readonly ErrorNegocio TipoUnidadInvalido = new("tipo_unidad_invalido", "Elige bimestre, trimestre, cuatrimestre, semestre, mensual o personalizada.");
    public static readonly ErrorNegocio CantidadUnidadesInvalida = new("cantidad_unidades_invalida", "El ciclo se divide en 1 a 12 unidades.");
    public static readonly ErrorNegocio UnidadFechasInvalidas = new("unidad_fechas_invalidas", "La fecha de fin de la unidad no puede ser anterior a la de inicio.");
    public static readonly ErrorNegocio UnidadFueraDeCiclo = new("unidad_fuera_de_ciclo", "Las unidades deben quedar dentro de las fechas del ciclo.");
    public static readonly ErrorNegocio UnidadesTraslapadas = new("unidades_traslapadas", "Dos unidades no pueden compartir fechas.");
    public static readonly ErrorNegocio UnidadCerrada = new("unidad_cerrada", "Esa unidad ya está cerrada en algún grupo; sus fechas no se pueden cambiar.");
    public static readonly ErrorNegocio UnidadInexistente = new("unidad_inexistente", "Esa unidad no es de este ciclo.");

    public static readonly ErrorNegocio CicloInexistente = new("ciclo_inexistente", "Elige uno de tus ciclos escolares.");
    public static readonly ErrorNegocio GradoInvalido = new("grado_invalido", "El grado va de 1 a 6 en primaria y de 1 a 3 en secundaria.");
    public static readonly ErrorNegocio GrupoDuplicado = new("grupo_duplicado", "Ya tienes un grupo con ese nombre y materia en este ciclo.");
    public static readonly ErrorNegocio LoteInvalido = new("lote_invalido", "Escribe de 1 a 15 nombres de grupo distintos.");
    public static readonly ErrorNegocio ConfiguracionCalificacionInvalida = new("configuracion_calificacion_invalida", "La mínima va de 0 a 10 y la aprobatoria no puede ser menor que la mínima.");
    public static readonly ErrorNegocio GrupoArchivado = new("grupo_archivado", "El grupo está archivado: se puede consultar, pero no editar.");

    public static readonly ErrorNegocio LimiteAlumnosGrupo = new("limite_alumnos_grupo", "El grupo ya tiene 50 alumnos activos.");
    public static readonly ErrorNegocio NumeroListaInvalido = new("numero_lista_invalido", "El número de lista va de 1 a 99.");
    public static readonly ErrorNegocio TratoInvalido = new("trato_invalido", "El trato es neutro, él o ella.");
    public static readonly ErrorNegocio AlumnoDadoDeBaja = new("alumno_dado_de_baja", "El alumno está dado de baja.");
    public static readonly ErrorNegocio TelefonoInvalido = new("telefono_invalido", "Escribe un WhatsApp de 10 dígitos o con clave de país, por ejemplo +52 667 123 4567.");
    public static readonly ErrorNegocio LimiteTutoresAlumno = new("limite_tutores_alumno", "Un alumno tiene como máximo 3 tutores.");
    public static readonly ErrorNegocio TutorYaVinculado = new("tutor_ya_vinculado", "Ese tutor ya está vinculado a este alumno.");
    public static readonly ErrorNegocio ArchivoInvalido = new("archivo_invalido", "Sube un archivo .xlsx o .csv de hasta 2 MB.");
    public static readonly ErrorNegocio ArchivoIlegible = new("archivo_ilegible", "No pudimos leer el archivo. Revisa que sea un Excel o CSV con encabezados.");
    public static readonly ErrorNegocio ImportacionConfirmada = new("importacion_confirmada", "Esa importación ya se guardó.");

    public static readonly IReadOnlyList<ErrorNegocio> Todos =
    [
        CicloFechasInvalidas, CicloMuyLargo, CicloDuplicado, TipoUnidadInvalido, CantidadUnidadesInvalida,
        UnidadFechasInvalidas, UnidadFueraDeCiclo, UnidadesTraslapadas, UnidadCerrada, UnidadInexistente,
        CicloInexistente, GradoInvalido, GrupoDuplicado, LoteInvalido, ConfiguracionCalificacionInvalida,
        GrupoArchivado, LimiteAlumnosGrupo, NumeroListaInvalido, TratoInvalido,
        AlumnoDadoDeBaja, TelefonoInvalido, LimiteTutoresAlumno, TutorYaVinculado, ArchivoInvalido,
        ArchivoIlegible, ImportacionConfirmada,
    ];
}
