namespace Aulisima.Domain.Evaluacion;

/// <summary>Cómo se califica un rubro o un criterio (RN-118).</summary>
public static class FormaCalificacion
{
    public const string Numerica = "numerica";
    public const string Cumplimiento = "cumplimiento";

    public static readonly IReadOnlyList<string> Todas = [Numerica, Cumplimiento];
}

/// <summary>Marca de una celda o de un criterio (RN-032, RN-118).</summary>
public static class MarcaCalificacion
{
    public const string Ninguna = "ninguna";
    public const string Cumplio = "cumplio";
    public const string NoCumplio = "no_cumplio";
    public const string NoEntrego = "no_entrego";
    public const string Exento = "exento";

    public static readonly IReadOnlyList<string> Todas = [Ninguna, Cumplio, NoCumplio, NoEntrego, Exento];
}

/// <summary>Comportamiento de cálculo de un tipo de rubro (ADR-0012, RN-039).</summary>
public static class Comportamiento
{
    public const string Actividades = "actividades";
    public const string Asistencia = "asistencia";

    /// <summary>Los que existen en el código desplegado: el panel solo deja elegir estos (RN-091, RN-093).</summary>
    public static readonly IReadOnlyList<string> Todos = [Actividades, Asistencia];
}

/// <summary>
/// Configuración del grupo para calcular; <see cref="ValorRetardo"/> es lo que vale un retardo (RN-102) y
/// <see cref="ValorParticipacion"/>, lo que suma cada participación extra (RN-121).
/// </summary>
public sealed record ConfiguracionCalculo(
    decimal CalifMinima,
    int DecimalesFinal,
    string Redondeo,
    decimal ValorRetardo = Grupos.ValorRetardo.PorDefecto,
    decimal ValorParticipacion = Grupos.ValorParticipacion.PorDefecto);

public sealed record CriterioCalculo(Guid Id, decimal Peso, string Forma, decimal CalifMinima);

public sealed record CeldaCriterio(Guid CriterioId, decimal? Valor, string Marca);

/// <summary>Lo capturado para un alumno en una actividad. Con criterios, el valor sale de ellos.</summary>
public sealed record CeldaCalculo(decimal? Valor, string Marca, IReadOnlyList<CeldaCriterio> Criterios)
{
    public static readonly CeldaCalculo Vacia = new(null, MarcaCalificacion.Ninguna, []);
}

/// <summary>
/// Un rubro con lo capturado para un alumno: sus celdas o, si es de asistencia, el estado del alumno en cada toma
/// de la unidad (<see cref="Tomas"/>).
/// </summary>
public sealed record RubroCalculo(
    Guid Id,
    decimal Peso,
    string Comportamiento,
    string Forma,
    decimal CalifMinima,
    IReadOnlyList<CriterioCalculo> Criterios,
    IReadOnlyList<CeldaCalculo> Celdas,
    IReadOnlyList<string>? Tomas = null);

public sealed record ResultadoRubro(Guid RubroId, decimal? Promedio);

/// <summary>
/// Resultado de un alumno en el periodo. <see cref="Exacto"/> es el cálculo sin redondear, ya con los puntos extra;
/// <see cref="Periodo"/> se muestra con 2 decimales y <see cref="Final"/> sigue la configuración del grupo; ambos
/// respetan la mínima. <see cref="PuntosExtra"/> son las participaciones por su valor (RN-123), aunque el tope de 10
/// no deje sumarlas todas.
/// </summary>
public sealed record ResultadoPeriodo(
    IReadOnlyList<ResultadoRubro> Rubros, decimal? Exacto, decimal? Periodo, decimal? Final, IReadOnlyList<Guid> RubrosSinDatos,
    decimal PuntosExtra = 0m);

/// <summary>
/// Cálculo de calificaciones (RN-032 a RN-035, RN-102 a RN-104, RN-112, RN-118, RN-119, RN-123, RN-124). Decimales exactos sin redondeos
/// intermedios; su gemelo en el frontend (<c>core/calculo</c>) pasa los mismos vectores de prueba (RN-036).
/// </summary>
public static class Calculo
{
    /// <summary>La escala llega a 10 (RN-030); los puntos extra no la rebasan (RN-124).</summary>
    public const decimal Maximo = 10m;

    /// <summary>Valor de una actividad para el promedio, o nulo si no cuenta (vacía o exenta).</summary>
    public static decimal? ValorActividad(RubroCalculo rubro, CeldaCalculo celda)
    {
        switch (celda.Marca)
        {
            case MarcaCalificacion.Exento:
                return null;
            case MarcaCalificacion.NoEntrego:
                return rubro.CalifMinima; // RN-118
        }

        if (rubro.Criterios.Count > 0)
        {
            return ValorConCriterios(rubro.Criterios, celda.Criterios);
        }

        return ValorSimple(rubro.Forma, rubro.CalifMinima, celda.Valor, celda.Marca);
    }

    /// <summary>RN-112: suma ponderada de los criterios calificados; sus pesos se reparten si falta alguno.</summary>
    public static decimal? ValorConCriterios(IReadOnlyList<CriterioCalculo> criterios, IReadOnlyList<CeldaCriterio> celdas)
    {
        decimal suma = 0m, pesos = 0m;
        foreach (var criterio in criterios)
        {
            var celda = celdas.FirstOrDefault(c => c.CriterioId == criterio.Id);
            if (celda is null || ValorSimple(criterio.Forma, criterio.CalifMinima, celda.Valor, celda.Marca) is not { } valor)
            {
                continue;
            }

            suma += valor * criterio.Peso;
            pesos += criterio.Peso;
        }

        return pesos == 0m ? null : suma / pesos;
    }

    /// <summary>RN-032: media aritmética de las actividades que cuentan.</summary>
    public static decimal? PromedioRubro(RubroCalculo rubro)
    {
        decimal suma = 0m;
        var cuenta = 0;
        foreach (var celda in rubro.Celdas)
        {
            if (ValorActividad(rubro, celda) is { } valor)
            {
                suma += valor;
                cuenta++;
            }
        }

        return cuenta == 0 ? null : suma / cuenta;
    }

    /// <summary>
    /// RN-102 a RN-104: presente vale 1, falta 0, retardo lo que diga el grupo y justificada no cuenta. El porcentaje
    /// por 10 da el promedio (95% = 9.5); sin tomas que cuenten, el rubro no tiene datos.
    /// </summary>
    public static decimal? PromedioAsistencia(IEnumerable<string> tomas, decimal valorRetardo)
    {
        decimal suma = 0m;
        var cuenta = 0;
        foreach (var estado in tomas)
        {
            decimal? valor = estado switch
            {
                EstadoAsistencia.Presente => 1m,
                EstadoAsistencia.Falta => 0m,
                EstadoAsistencia.Retardo => valorRetardo,
                _ => null,
            };
            if (valor is { } v)
            {
                suma += v;
                cuenta++;
            }
        }

        return cuenta == 0 ? null : suma * 10m / cuenta;
    }

    /// <summary>
    /// RN-033 y RN-034: rubros con datos ponderados por su peso; los sin datos reparten el suyo. RN-124: encima de esa
    /// calificación ordinaria (ya sin bajar de la mínima) se suman los puntos de las <paramref name="participaciones"/>,
    /// sin pasar de 10; sin calificación ordinaria no hay calificación.
    /// </summary>
    public static ResultadoPeriodo Periodo(IReadOnlyList<RubroCalculo> rubros, ConfiguracionCalculo configuracion, int participaciones = 0)
    {
        var puntos = ReglasParticipacion.Puntos(participaciones, configuracion.ValorParticipacion);
        var resultados = new List<ResultadoRubro>();
        var sinDatos = new List<Guid>();
        decimal suma = 0m, pesos = 0m;
        foreach (var rubro in rubros)
        {
            var promedio = rubro.Comportamiento switch
            {
                Comportamiento.Actividades => PromedioRubro(rubro),
                Comportamiento.Asistencia => PromedioAsistencia(rubro.Tomas ?? [], configuracion.ValorRetardo),
                _ => null,
            };
            resultados.Add(new ResultadoRubro(rubro.Id, promedio));
            if (promedio is { } p && rubro.Peso > 0m)
            {
                suma += p * rubro.Peso;
                pesos += rubro.Peso;
            }
            else
            {
                sinDatos.Add(rubro.Id);
            }
        }

        if (pesos == 0m)
        {
            return new ResultadoPeriodo(resultados, null, null, null, sinDatos, puntos);
        }

        var exacto = suma / pesos;
        if (puntos > 0m)
        {
            exacto = Math.Min(Maximo, Math.Max(configuracion.CalifMinima, exacto) + puntos); // RN-124
        }

        var periodo = Math.Max(configuracion.CalifMinima, Redondear(exacto, 2, Grupos.Redondeo.Cercano)); // RN-034
        var final = Math.Max(configuracion.CalifMinima, Redondear(exacto, configuracion.DecimalesFinal, configuracion.Redondeo)); // RN-035
        return new ResultadoPeriodo(resultados, exacto, periodo, final, sinDatos, puntos);
    }

    /// <summary>RN-035: hacia arriba, hacia abajo o al más cercano con medio hacia arriba.</summary>
    public static decimal Redondear(decimal valor, int decimales, string sentido)
    {
        var factor = decimales switch { 0 => 1m, 1 => 10m, _ => 100m };
        return sentido switch
        {
            Grupos.Redondeo.Arriba => Math.Ceiling(valor * factor) / factor,
            Grupos.Redondeo.Abajo => Math.Floor(valor * factor) / factor,
            _ => Math.Round(valor, decimales, MidpointRounding.AwayFromZero),
        };
    }

    private static decimal? ValorSimple(string forma, decimal minima, decimal? valor, string marca) => marca switch
    {
        MarcaCalificacion.Cumplio => 10m, // RN-118: ✅ vale 10
        MarcaCalificacion.NoCumplio => minima, // ❌ vale la mínima
        MarcaCalificacion.NoEntrego => minima,
        MarcaCalificacion.Exento => null,
        _ => forma == FormaCalificacion.Numerica ? valor : null,
    };
}
