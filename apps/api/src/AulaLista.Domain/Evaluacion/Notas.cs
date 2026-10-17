using AulaLista.Domain.Comun;
using AulaLista.Domain.Grupos;

namespace AulaLista.Domain.Evaluacion;

/// <summary>Etiqueta opcional de una nota (SPEC-03, Notas por alumno).</summary>
public static class EtiquetaNota
{
    public const string Ninguna = "ninguna";
    public const string Fortaleza = "fortaleza";
    public const string Mejora = "mejora";
    public const string Conducta = "conducta";

    public static readonly IReadOnlySet<string> Todas = new HashSet<string> { Ninguna, Fortaleza, Mejora, Conducta };
}

/// <summary>
/// Una nota del docente sobre un alumno en un periodo. Privada del docente: nunca llega a tutores y solo alimenta
/// el borrador de observación (SPEC-04). Cada nota es un registro nuevo, así que no choca al sincronizar; su id lo
/// genera el cliente para que reenviarla no la duplique.
/// </summary>
public sealed class Nota : Entidad
{
    public const int LongitudTexto = 1000;

    private Nota(Guid id, Alumno alumno, Guid periodoId, string texto, string etiqueta, DateOnly fecha, DateTimeOffset ahora)
        : base(id, ahora)
    {
        AlumnoId = alumno.Id;
        DocenteId = alumno.DocenteId;
        PeriodoId = periodoId;
        Texto = texto;
        Etiqueta = etiqueta;
        Fecha = fecha;
    }

    private Nota()
    {
    }

    public Guid AlumnoId { get; private set; }

    public Guid DocenteId { get; private set; }

    public Guid PeriodoId { get; private set; }

    public string Texto { get; private set; } = string.Empty;

    public string Etiqueta { get; private set; } = EtiquetaNota.Ninguna;

    public DateOnly Fecha { get; private set; }

    public static Resultado<Nota> Crear(
        Guid id, Alumno alumno, Periodo periodo, string texto, string etiqueta, DateOnly fecha, DateTimeOffset ahora)
    {
        if (!alumno.EstaActivo)
        {
            return Resultado.Fallo<Nota>(ErroresGrupos.AlumnoDadoDeBaja);
        }

        if (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length > LongitudTexto || !EtiquetaNota.Todas.Contains(etiqueta))
        {
            return Resultado.Fallo<Nota>(ErroresEvaluacion.NotaInvalida);
        }

        return Resultado.Exito(new Nota(id, alumno, periodo.Id, texto.Trim(), etiqueta, fecha, ahora));
    }
}
