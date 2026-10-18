using System.Security.Cryptography;
using AulaLista.Domain.Comun;

namespace AulaLista.Domain.Boletas;

/// <summary>Un rubro como sale en la boleta: nombre, peso y promedio con 2 decimales.</summary>
public sealed record RubroBoleta(string Nombre, decimal Peso, decimal? Promedio);

/// <summary>
/// RN-051: la foto de lo que dice la boleta. Se guarda completa (jsonb) para que cambios posteriores en el grupo, el
/// perfil o las calificaciones no alteren una boleta emitida. No lleva fechas: dos fotos iguales son la misma boleta.
/// </summary>
public sealed record DatosBoleta(
    string? Escuela,
    string? Cct,
    string Ciclo,
    string Unidad,
    string Alumno,
    int NumeroLista,
    string Nivel,
    int Grado,
    string Grupo,
    string? Materia,
    string Docente,
    IReadOnlyList<RubroBoleta> Rubros,
    decimal? Calificacion,
    int DecimalesFinal,
    string Observacion)
{
    /// <summary>Igualdad por contenido, también de los rubros (los registros comparan listas por referencia).</summary>
    public bool MismoContenido(DatosBoleta otra) =>
        this with { Rubros = [] } == otra with { Rubros = [] } && Rubros.SequenceEqual(otra.Rubros);
}

/// <summary>
/// La boleta de un alumno en un periodo (SPEC-05). Cada corrección de una publicada es una versión nueva y la anterior
/// queda <see cref="ReemplazadaPor"/> la nueva (RN-052). Mientras no se publica, volver a generar la actualiza.
/// </summary>
public sealed class Boleta : Entidad
{
    public const int LongitudCodigo = 8;

    // Base 32 de Crockford sin I, L, O ni U: se dicta y se copia sin confundir letras con números.
    private const string Alfabeto = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private Boleta(Guid alumnoId, Guid docenteId, Guid periodoId, int version, DatosBoleta datos, DateTimeOffset ahora)
        : base(ahora)
    {
        AlumnoId = alumnoId;
        DocenteId = docenteId;
        PeriodoId = periodoId;
        Version = version;
        Datos = datos;
        Codigo = NuevoCodigo();
        EmitidaEn = ahora;
    }

    private Boleta()
    {
    }

    public Guid AlumnoId { get; private set; }

    public Guid DocenteId { get; private set; }

    public Guid PeriodoId { get; private set; }

    public int Version { get; private set; }

    public DatosBoleta Datos { get; private set; } = null!;

    /// <summary>Código corto de verificación que va en el pie del PDF.</summary>
    public string Codigo { get; private set; } = string.Empty;

    /// <summary>Cuándo se tomó la foto de <see cref="Datos"/>; es la fecha de emisión del PDF.</summary>
    public DateTimeOffset EmitidaEn { get; private set; }

    /// <summary>Ruta del PDF en el almacén; nula mientras se prepara.</summary>
    public string? PdfRuta { get; private set; }

    /// <summary>Falló la preparación del PDF tras los reintentos; volver a generar lo intenta de nuevo.</summary>
    public bool FalloPdf { get; private set; }

    public DateTimeOffset? PublicadaEn { get; private set; }

    public Guid? ReemplazadaPor { get; private set; }

    public bool EstaPublicada => PublicadaEn is not null;

    public bool EsVigente => ReemplazadaPor is null;

    public bool EsCorreccion => Version > 1;

    public static Boleta Primera(Guid alumnoId, Guid docenteId, Guid periodoId, DatosBoleta datos, DateTimeOffset ahora) =>
        new(alumnoId, docenteId, periodoId, 1, datos, ahora);

    /// <summary>Volver a generar una boleta sin publicar: nueva foto, nuevo PDF (RN-051 solo protege lo emitido).</summary>
    public Resultado Actualizar(DatosBoleta datos, DateTimeOffset ahora)
    {
        if (EstaPublicada)
        {
            return Resultado.Fallo(ErroresBoletas.BoletaPublicada);
        }

        Datos = datos;
        EmitidaEn = ahora;
        PdfRuta = null;
        FalloPdf = false;
        MarcarActualizada(ahora);
        return Resultado.Exito();
    }

    /// <summary>RN-052: la versión corregida, ya publicada, que reemplaza a esta para el tutor.</summary>
    public Resultado<Boleta> Corregir(DatosBoleta datos, DateTimeOffset ahora)
    {
        if (!EstaPublicada || !EsVigente)
        {
            return Resultado.Fallo<Boleta>(ErroresBoletas.BoletaNoCorregible);
        }

        if (datos.MismoContenido(Datos))
        {
            return Resultado.Fallo<Boleta>(ErroresBoletas.SinCambios);
        }

        var nueva = new Boleta(AlumnoId, DocenteId, PeriodoId, Version + 1, datos, ahora) { PublicadaEn = ahora };
        ReemplazadaPor = nueva.Id;
        MarcarActualizada(ahora);
        return Resultado.Exito(nueva);
    }

    public Resultado Publicar(DateTimeOffset ahora)
    {
        if (EstaPublicada)
        {
            return Resultado.Exito();
        }

        if (PdfRuta is null)
        {
            return Resultado.Fallo(ErroresBoletas.BoletaPreparando);
        }

        PublicadaEn = ahora;
        MarcarActualizada(ahora);
        return Resultado.Exito();
    }

    public void RegistrarPdf(string ruta, DateTimeOffset ahora)
    {
        PdfRuta = ruta;
        FalloPdf = false;
        MarcarActualizada(ahora);
    }

    public void RegistrarFalloPdf(DateTimeOffset ahora)
    {
        FalloPdf = true;
        MarcarActualizada(ahora);
    }

    private static string NuevoCodigo() =>
        string.Create(LongitudCodigo, 0, (letras, _) =>
        {
            for (var i = 0; i < letras.Length; i++)
            {
                letras[i] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
            }
        });
}

/// <summary>Estados de la boleta de un alumno como los ve el docente en la pantalla de boletas.</summary>
public static class EstadoBoleta
{
    /// <summary>Su observación aún no está aprobada (RN-050).</summary>
    public const string SinAprobar = "sin_aprobar";

    /// <summary>Observación aprobada y todavía sin boleta.</summary>
    public const string SinBoleta = "sin_boleta";

    public const string Preparando = "preparando";
    public const string Lista = "lista";
    public const string Publicada = "publicada";
}

public static class ErroresBoletas
{
    public static readonly ErrorNegocio BoletaPublicada = new("boleta_publicada", "La boleta ya se publicó. Para cambiarla, emite una versión corregida.");
    public static readonly ErrorNegocio BoletaNoCorregible = new("boleta_no_corregible", "Solo se corrige la versión vigente de una boleta publicada.");
    public static readonly ErrorNegocio SinCambios = new("boleta_sin_cambios", "La boleta ya tiene los datos actuales; no hay nada que corregir.");
    public static readonly ErrorNegocio BoletaPreparando = new("boleta_preparando", "La boleta todavía se está preparando. Espera unos segundos.");
    public static readonly ErrorNegocio ObservacionSinAprobar = new("observacion_sin_aprobar", "Aprueba la observación del alumno antes de emitir su boleta.");
    public static readonly ErrorNegocio SinBoletasPorPublicar = new("sin_boletas_por_publicar", "No hay boletas listas para publicar. Genera las boletas primero.");
    public static readonly ErrorNegocio BoletasDesactualizadas = new("boletas_desactualizadas", "Algunas boletas ya no coinciden con los datos actuales. Vuelve a generarlas antes de publicar.");
    public static readonly ErrorNegocio SinBoletas = new("sin_boletas", "Todavía no hay boletas. Genera las boletas primero.");

    public static readonly IReadOnlyList<ErrorNegocio> Todos =
    [
        BoletaPublicada, BoletaNoCorregible, SinCambios, BoletaPreparando, ObservacionSinAprobar, SinBoletasPorPublicar,
        BoletasDesactualizadas, SinBoletas,
    ];
}
