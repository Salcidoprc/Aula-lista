using AulaLista.Domain.Comun;

namespace AulaLista.Domain.Evaluacion;

/// <summary>Errores de negocio de rubros, actividades y calificaciones (SPEC-03).</summary>
public static class ErroresEvaluacion
{
    public static readonly ErrorNegocio PesosNoSuman100 = new("pesos_no_suman_100", "Los pesos de los rubros deben sumar 100.");
    public static readonly ErrorNegocio PesosCriteriosNoSuman100 = new("pesos_criterios_no_suman_100", "Los pesos de los criterios de un rubro deben sumar 100.");
    public static readonly ErrorNegocio RubrosInvalidos = new("rubros_invalidos", "Un periodo tiene hasta 20 rubros, cada uno con nombre y un peso de 0 a 100.");
    public static readonly ErrorNegocio CriteriosInvalidos = new("criterios_invalidos", "Un rubro tiene de 2 a 10 criterios, cada uno con nombre y peso.");
    public static readonly ErrorNegocio TipoRubroInvalido = new("tipo_rubro_invalido", "Elige uno de los tipos de rubro disponibles.");
    public static readonly ErrorNegocio FormaInvalida = new("forma_invalida", "La forma de calificar es con número o por cumplimiento.");
    public static readonly ErrorNegocio MinimaInvalida = new("minima_invalida", "La calificación mínima va de 0 a 10, con hasta dos decimales.");
    public static readonly ErrorNegocio FormaConCalificaciones = new("forma_con_calificaciones", "La forma de calificar solo se cambia mientras no haya calificaciones.");
    public static readonly ErrorNegocio BorradoRequiereConfirmacion = new("borrado_requiere_confirmacion", "Lo que vas a borrar tiene calificaciones. Confirma para borrarlas también.");
    public static readonly ErrorNegocio PeriodoEnCierre = new("periodo_en_cierre", "El periodo está en cierre: los rubros y pesos ya no se editan.");
    public static readonly ErrorNegocio PeriodoCerrado = new("periodo_cerrado", "El periodo está cerrado: es de solo lectura.");
    public static readonly ErrorNegocio PeriodoConRubros = new("periodo_con_rubros", "Este periodo ya tiene rubros. Bórralos antes de copiar otros.");
    public static readonly ErrorNegocio ActividadInvalida = new("actividad_invalida", "La actividad necesita un nombre de hasta 60 caracteres.");
    public static readonly ErrorNegocio FechaFueraDeUnidad = new("fecha_fuera_de_unidad", "La fecha debe caer dentro de la unidad.");
    public static readonly ErrorNegocio RubroSinActividades = new("rubro_sin_actividades", "Este rubro se calcula solo; no lleva actividades.");
    public static readonly ErrorNegocio CalificacionFueraDeRango = new("calificacion_fuera_de_rango", "La calificación va de 0 a 10.");
    public static readonly ErrorNegocio CalificacionInvalida = new("calificacion_invalida", "Usa números con hasta dos decimales.");
    public static readonly ErrorNegocio CalificacionMenorAMinima = new("calificacion_menor_a_minima", "La calificación no puede ser menor que la mínima del rubro.");
    public static readonly ErrorNegocio MarcaInvalida = new("marca_invalida", "Esa marca no aplica a esta forma de calificar.");
    public static readonly ErrorNegocio CriterioInexistente = new("criterio_inexistente", "Ese criterio no es de este rubro.");
    public static readonly ErrorNegocio CeldaInexistente = new("celda_inexistente", "Esa actividad o ese alumno ya no están en el grupo.");
    public static readonly ErrorNegocio GrupoConCalificaciones = new("grupo_con_calificaciones", "El grupo ya tiene calificaciones; archívalo en lugar de borrarlo.");

    public static ErrorNegocio CalificacionesMenoresAMinima(int cantidad) =>
        new("calificaciones_menores_a_minima", cantidad == 1
            ? "Hay 1 calificación menor que la nueva mínima. Corrígela antes de guardar."
            : $"Hay {cantidad} calificaciones menores que la nueva mínima. Corrígelas antes de guardar.");

    public static readonly IReadOnlyList<ErrorNegocio> Todos =
    [
        PesosNoSuman100, PesosCriteriosNoSuman100, RubrosInvalidos, CriteriosInvalidos, TipoRubroInvalido, FormaInvalida,
        MinimaInvalida, FormaConCalificaciones, BorradoRequiereConfirmacion, PeriodoEnCierre, PeriodoCerrado, PeriodoConRubros,
        ActividadInvalida, FechaFueraDeUnidad, RubroSinActividades, CalificacionFueraDeRango, CalificacionInvalida,
        CalificacionMenorAMinima, MarcaInvalida, CriterioInexistente, CeldaInexistente, GrupoConCalificaciones, CalificacionesMenoresAMinima(2),
    ];
}
