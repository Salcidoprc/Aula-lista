using AulaLista.Domain.Comun;
using AulaLista.Domain.Grupos;

namespace AulaLista.Domain.Cierre;

/// <summary>Estados de una observación (RF-044, RN-042).</summary>
public static class EstadoObservacion
{
    public const string SinGenerar = "sin_generar";
    public const string Generando = "generando";
    public const string Borrador = "borrador";
    public const string Aprobada = "aprobada";
    public const string Publicada = "publicada";

    public static readonly IReadOnlyList<string> Todos = [SinGenerar, Generando, Borrador, Aprobada, Publicada];
}

/// <summary>Por qué falló la última generación; la pantalla dice "No se pudo generar" (SPEC-04, Errores).</summary>
public static class FalloGeneracion
{
    public const string Rechazo = "rechazo";
    public const string Longitud = "longitud";
    public const string Error = "error";
}

/// <summary>
/// La observación de un alumno en un periodo. La IA propone y el docente aprueba (RN-042): solo
/// <see cref="EstadoObservacion.Aprobada"/> puede llegar a un tutor. <see cref="BorradorIa"/> es la primera versión
/// generada y <see cref="TextoFinal"/> la aprobada, para medir cuánto se editó (RN-045).
/// </summary>
public sealed class Observacion : Entidad
{
    public const int LongitudTexto = 1500;
    public const int LongitudInstruccion = 200;

    /// <summary>RN-043: versiones nuevas que se pueden pedir por alumno y periodo.</summary>
    public const int RegeneracionesMaximas = 5;

    /// <summary>Tras este tiempo una generación sin respuesta se da por perdida y se puede volver a pedir.</summary>
    public static readonly TimeSpan GeneracionPerdida = TimeSpan.FromMinutes(10);

    private Observacion(Alumno alumno, Guid periodoId, DateTimeOffset ahora)
        : base(ahora)
    {
        AlumnoId = alumno.Id;
        DocenteId = alumno.DocenteId;
        PeriodoId = periodoId;
    }

    private Observacion()
    {
    }

    public Guid AlumnoId { get; private set; }

    public Guid DocenteId { get; private set; }

    public Guid PeriodoId { get; private set; }

    public string Estado { get; private set; } = EstadoObservacion.SinGenerar;

    /// <summary>El texto vigente: el borrador, lo que escribió el docente o lo aprobado.</summary>
    public string? Texto { get; private set; }

    public string? BorradorIa { get; private set; }

    public string? TextoFinal { get; private set; }

    /// <summary>La instrucción de la regeneración en curso o la última ("más breve").</summary>
    public string? Instruccion { get; private set; }

    public int Regeneraciones { get; private set; }

    public string? Modelo { get; private set; }

    public string? VersionPrompt { get; private set; }

    public int TokensEntrada { get; private set; }

    public int TokensSalida { get; private set; }

    /// <summary>Si la última generación falló, por qué (<see cref="FalloGeneracion"/>).</summary>
    public string? Fallo { get; private set; }

    /// <summary>RN-044: se reabrió sola porque cambiaron las calificaciones o notas del alumno.</summary>
    public bool DatosCambiaron { get; private set; }

    public DateTimeOffset? AprobadaEn { get; private set; }

    public bool EstaAprobada => Estado is EstadoObservacion.Aprobada or EstadoObservacion.Publicada;

    /// <summary>En generación y sin respuesta desde hace más de <see cref="GeneracionPerdida"/>.</summary>
    public bool GeneracionAtorada(DateTimeOffset ahora) =>
        Estado == EstadoObservacion.Generando && ahora - UpdatedAt > GeneracionPerdida;

    /// <summary>La observación vacía de un alumno: se crea al iniciar el cierre o al generar la de un alumno nuevo.</summary>
    public static Observacion Nueva(Alumno alumno, Guid periodoId, DateTimeOffset ahora) => new(alumno, periodoId, ahora);

    /// <summary>Generar para todo el grupo toma las que no tienen texto (RF-041).</summary>
    public bool PuedeGenerarse(DateTimeOffset ahora) =>
        Estado == EstadoObservacion.SinGenerar || GeneracionAtorada(ahora);

    public void Encolar(DateTimeOffset ahora)
    {
        Estado = EstadoObservacion.Generando;
        Fallo = null;
        MarcarActualizada(ahora);
    }

    /// <summary>RN-043: reemplaza el borrador, nunca lo aprobado, y hasta <see cref="RegeneracionesMaximas"/> veces.</summary>
    public Resultado PedirRegeneracion(string? instruccion, DateTimeOffset ahora)
    {
        if (EstaAprobada)
        {
            return Resultado.Fallo(ErroresCierre.ObservacionAprobada);
        }

        if (Estado == EstadoObservacion.Generando && !GeneracionAtorada(ahora))
        {
            return Resultado.Fallo(ErroresCierre.EnGeneracion);
        }

        if (Regeneraciones >= RegeneracionesMaximas)
        {
            return Resultado.Fallo(ErroresCierre.LimiteRegeneraciones);
        }

        var limpia = string.IsNullOrWhiteSpace(instruccion) ? null : instruccion.Trim();
        if (limpia?.Length > LongitudInstruccion)
        {
            return Resultado.Fallo(ErroresCierre.InstruccionInvalida);
        }

        Regeneraciones++;
        Instruccion = limpia;
        Encolar(ahora);
        return Resultado.Exito();
    }

    /// <summary>Llega el texto de la IA. Si el docente aprobó mientras tanto, no se toca (RN-043).</summary>
    public void RegistrarBorrador(string texto, string modelo, string versionPrompt, int tokensEntrada, int tokensSalida, DateTimeOffset ahora)
    {
        SumarConsumo(modelo, versionPrompt, tokensEntrada, tokensSalida);
        if (Estado != EstadoObservacion.Generando)
        {
            return;
        }

        Texto = texto;
        BorradorIa ??= texto;
        Estado = EstadoObservacion.Borrador;
        Fallo = null;
        MarcarActualizada(ahora);
    }

    /// <summary>La generación falló: si ya había texto se conserva y se avisa; si no, queda sin generar.</summary>
    public void RegistrarFallo(string fallo, string? modelo, string? versionPrompt, int tokensEntrada, int tokensSalida, DateTimeOffset ahora)
    {
        SumarConsumo(modelo, versionPrompt, tokensEntrada, tokensSalida);
        if (Estado != EstadoObservacion.Generando)
        {
            return;
        }

        Estado = string.IsNullOrWhiteSpace(Texto) ? EstadoObservacion.SinGenerar : EstadoObservacion.Borrador;
        Fallo = fallo;
        MarcarActualizada(ahora);
    }

    /// <summary>RF-042: el docente corrige o escribe a mano. Vacío lo deja sin generar.</summary>
    public Resultado Editar(string texto, DateTimeOffset ahora)
    {
        if (EstaAprobada)
        {
            return Resultado.Fallo(ErroresCierre.ObservacionAprobada);
        }

        if (Estado == EstadoObservacion.Generando && !GeneracionAtorada(ahora))
        {
            return Resultado.Fallo(ErroresCierre.EnGeneracion);
        }

        if (TextoValido(texto) is { } error)
        {
            return Resultado.Fallo(error);
        }

        var limpio = texto.Trim();
        Texto = limpio.Length == 0 ? null : limpio;
        Estado = Texto is null ? EstadoObservacion.SinGenerar : EstadoObservacion.Borrador;
        MarcarActualizada(ahora);
        return Resultado.Exito();
    }

    /// <summary>RN-042: el docente aprueba el texto vigente o el que manda en ese momento.</summary>
    public Resultado Aprobar(string? texto, DateTimeOffset ahora)
    {
        if (Estado == EstadoObservacion.Publicada)
        {
            return Resultado.Fallo(ErroresCierre.ObservacionPublicada);
        }

        if (Estado == EstadoObservacion.Generando && !GeneracionAtorada(ahora))
        {
            return Resultado.Fallo(ErroresCierre.EnGeneracion);
        }

        if (texto is not null && TextoValido(texto) is { } error)
        {
            return Resultado.Fallo(error);
        }

        var final = (texto ?? Texto)?.Trim();
        if (string.IsNullOrEmpty(final))
        {
            return Resultado.Fallo(ErroresCierre.ObservacionVacia);
        }

        Texto = final;
        TextoFinal = final;
        Estado = EstadoObservacion.Aprobada;
        DatosCambiaron = false;
        AprobadaEn = ahora;
        MarcarActualizada(ahora);
        return Resultado.Exito();
    }

    /// <summary>RF-046: vuelve a borrador mientras la boleta no esté publicada.</summary>
    public Resultado Reabrir(DateTimeOffset ahora)
    {
        if (Estado == EstadoObservacion.Publicada)
        {
            return Resultado.Fallo(ErroresCierre.ObservacionPublicada);
        }

        if (Estado == EstadoObservacion.Aprobada)
        {
            Estado = EstadoObservacion.Borrador;
            AprobadaEn = null;
            MarcarActualizada(ahora);
        }

        return Resultado.Exito();
    }

    private static ErrorNegocio? TextoValido(string texto) =>
        texto.Trim().Length > LongitudTexto ? ErroresCierre.ObservacionLarga : null;

    private void SumarConsumo(string? modelo, string? versionPrompt, int tokensEntrada, int tokensSalida)
    {
        Modelo = modelo ?? Modelo;
        VersionPrompt = versionPrompt ?? VersionPrompt;
        TokensEntrada += tokensEntrada;
        TokensSalida += tokensSalida;
    }
}
