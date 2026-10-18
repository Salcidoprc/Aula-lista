using AulaLista.Application.Grupos;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Evaluacion;
using AulaLista.Domain.Grupos;

namespace AulaLista.Application.Evaluacion;

/// <summary>Qué se puede hacer en un periodo según su estado y el de su grupo (RN-018, RN-037, RN-038).</summary>
internal static class ReglasPeriodo
{
    /// <summary>Rubros, criterios y pesos: solo con el periodo abierto (RN-037).</summary>
    public static ErrorNegocio? PuedeEditarRubros(PeriodoCompleto periodo) => periodo.Periodo.Estado switch
    {
        _ when periodo.Grupo.EstaArchivado => ErroresGrupos.GrupoArchivado,
        EstadoPeriodo.Cerrado => ErroresEvaluacion.PeriodoCerrado,
        EstadoPeriodo.EnCierre => ErroresEvaluacion.PeriodoEnCierre,
        _ => null,
    };

    /// <summary>Actividades y calificaciones: también durante el cierre (RN-037); cerrado es de solo lectura (RN-038).</summary>
    public static ErrorNegocio? PuedeCapturar(PeriodoCompleto periodo) => periodo.Periodo.Estado switch
    {
        _ when periodo.Grupo.EstaArchivado => ErroresGrupos.GrupoArchivado,
        EstadoPeriodo.Cerrado => ErroresEvaluacion.PeriodoCerrado,
        _ => null,
    };

    public static ConfiguracionCalculo Configuracion(Grupo grupo) => new(grupo.CalifMinima, grupo.DecimalesFinal, grupo.Redondeo, grupo.ValorRetardo);
}
