using AulaLista.Application.Planes;
using AulaLista.Domain.Planes;
using Microsoft.Extensions.Options;

namespace AulaLista.Application.Operacion;

/// <summary>
/// Uso de la app en ventanas que terminan ahora. Un docente activo abrió la app (inició o renovó su sesión); la frecuencia
/// es cuántos de los activos del mes volvieron en la última semana.
/// </summary>
public sealed record UsoResumenDto(
    int Docentes,
    int ActivosDia,
    int ActivosSemana,
    int ActivosMes,
    decimal? PorcentajeFrecuencia,
    int RegistrosSemana,
    int RegistrosMes,
    int Grupos,
    int Alumnos);

/// <summary>
/// Un paso de la activación: <c>registro</c>, <c>grupo</c>, <c>alumnos</c>, <c>calificacion</c>, <c>cierre</c> o
/// <c>boletas</c>, con el porcentaje sobre los registrados y sobre el paso anterior.
/// </summary>
public sealed record PasoEmbudoDto(string Paso, int Docentes, decimal? Porcentaje, decimal? PorcentajeDelAnterior);

/// <summary>Los porcentajes son <c>null</c> mientras la semana no ha terminado para todos los de la cohorte.</summary>
public sealed record CohorteDto(DateOnly Semana, int Docentes, decimal? Semana1, decimal? Semana2, decimal? Semana4);

/// <summary>
/// MRR: lo que cobran al mes los grupos Pro pagados (SPEC-08). El costo es el variable del mes en curso (IA y WhatsApp) y el
/// margen, el MRR menos ese costo.
/// </summary>
public sealed record DineroResumenDto(
    string Moneda,
    decimal Mrr,
    int DocentesQuePagan,
    int GruposPro,
    decimal? IngresoPromedio,
    int PruebasActivas,
    int BajasMes,
    decimal CostoMes,
    decimal Margen);

/// <summary>Lo que la app entregó en los últimos 30 días.</summary>
public sealed record ValorResumenDto(int Boletas, int Observaciones, int Avisos, int Entregas, decimal? PorcentajeAvisosLeidos);

public sealed record SemanaResumenDto(
    DateOnly Semana, int Activos, int Registros, int Pagan, int AltasPago, int Bajas, int Boletas, int Observaciones, int Avisos);

/// <summary>RF-094: el resumen del negocio de un vistazo, con la tendencia de las últimas semanas.</summary>
public sealed record ResumenDto(
    UsoResumenDto Uso,
    IReadOnlyList<PasoEmbudoDto> Embudo,
    IReadOnlyList<CohorteDto> Retencion,
    DineroResumenDto Dinero,
    ValorResumenDto Valor,
    IReadOnlyList<SemanaResumenDto> Semanas);

/// <summary>
/// RF-094: uso, activación, retención, dinero y valor entregado. Solo conteos y montos; ningún docente, alumno ni tutor
/// (RN-090). El dinero sale del mismo cálculo que la sección Costos para que las dos cifras coincidan.
/// </summary>
public sealed class ResumenCasoUso(
    IRepositorioAdmin repositorio,
    CostosCasoUso costos,
    IOptions<OpcionesPlanes> planes,
    IOptions<OpcionesOperacion> opciones,
    TimeProvider reloj)
{
    public static readonly IReadOnlyList<string> Pasos = ["registro", "grupo", "alumnos", "calificacion", "cierre", "boletas"];

    public async Task<ResumenDto> EjecutarAsync(CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var hoy = Calendario.Hoy(ahora);
        var semanas = opciones.Value.SemanasMetricas;
        var desde = Calendario.LunesDe(hoy).AddDays(-7 * (semanas - 1));

        var r = await repositorio.ResumenAsync(ahora, ct);
        var serie = await repositorio.ResumenSemanasAsync(desde, semanas, ct);
        var embudo = await repositorio.EmbudoAsync(ct);
        var cohortes = await repositorio.RetencionAsync(desde, semanas, ct);
        var dinero = Dinero(await costos.EjecutarAsync(ct), r.BajasMes);

        return new ResumenDto(
            new UsoResumenDto(
                r.Docentes, r.ActivosDia, r.ActivosSemana, r.ActivosMes, Porcentaje(r.ActivosSemana, r.ActivosMes),
                r.RegistrosSemana, r.RegistrosMes, r.Grupos, r.Alumnos),
            Embudo(embudo),
            cohortes.Select(c => new CohorteDto(
                c.Cohorte,
                c.Docentes,
                Retenidos(c, 1, c.Semana1, hoy),
                Retenidos(c, 2, c.Semana2, hoy),
                Retenidos(c, 4, c.Semana4, hoy))).ToList(),
            dinero,
            new ValorResumenDto(r.BoletasMes, r.ObservacionesMes, r.AvisosMes, r.EntregasMes, Porcentaje(r.EntregasLeidasMes, r.EntregasMes)),
            serie.Select(s => new SemanaResumenDto(
                s.Semana, s.Activos, s.Registros, s.Pagan, s.AltasPago, s.Bajas, s.Boletas, s.Observaciones, s.Avisos)).ToList());
    }

    public static IReadOnlyList<PasoEmbudoDto> Embudo(FilaEmbudo e)
    {
        int[] conteos = [e.Registrados, e.ConGrupo, e.ConAlumnos, e.ConCalificacion, e.ConCierre, e.ConBoletas];
        return conteos.Select((n, i) => new PasoEmbudoDto(
            Pasos[i], n, Porcentaje(n, e.Registrados), i == 0 ? null : Porcentaje(n, conteos[i - 1]))).ToList();
    }

    /// <summary>
    /// La semana <paramref name="semana"/> del último docente de la cohorte (que se registró el domingo) termina
    /// <c>7 × (semana + 2)</c> días después del lunes de la cohorte; antes de eso el porcentaje engañaría.
    /// </summary>
    public static decimal? Retenidos(FilaCohorte c, int semana, int volvieron, DateOnly hoy) =>
        c.Cohorte.AddDays(7 * (semana + 2)) > hoy ? null : Porcentaje(volvieron, c.Docentes);

    private DineroResumenDto Dinero(CostosDto c, int bajas)
    {
        var p = planes.Value;
        var pro = c.Grupos.Where(g => g.Plan == PlanVigente.Pro).ToList();
        var mrr = pro.GroupBy(g => g.DocenteId).Sum(g => Tarifa.Mensual(g.Count(), p.PrecioPrimerGrupo, p.PrecioGrupoAdicional));
        var pagan = pro.Select(g => g.DocenteId).Distinct().Count();
        return new DineroResumenDto(
            c.Moneda,
            mrr,
            pagan,
            pro.Count,
            pagan == 0 ? null : decimal.Round(mrr / pagan, 2, MidpointRounding.AwayFromZero),
            c.Grupos.Count(g => g.Plan == PlanVigente.Prueba),
            bajas,
            c.CostoTotal,
            mrr - c.CostoTotal);
    }

    private static decimal? Porcentaje(int parte, int total) => MetricasCasoUso.Porcentaje(parte, total);
}
