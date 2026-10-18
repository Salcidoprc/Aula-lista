using AulaLista.Domain.Comun;

namespace AulaLista.Domain.Cierre;

/// <summary>RN-046: longitud de la observación en palabras.</summary>
public static class LongitudObservacion
{
    public const string Breve = "breve";
    public const string Normal = "normal";
    public const string Detallada = "detallada";

    public static readonly IReadOnlyList<string> Todas = [Breve, Normal, Detallada];

    public static (int Minimo, int Maximo) Palabras(string longitud) => longitud switch
    {
        Breve => (30, 50),
        Detallada => (100, 140),
        _ => (60, 90),
    };

    /// <summary>
    /// Lo que se acepta de la IA: el rango con holgura (un tercio menos del mínimo y un cuarto más del máximo), porque
    /// el modelo no cuenta palabras exacto. Fuera de eso la generación falla por longitud (SPEC-04, Errores).
    /// </summary>
    public static bool Aceptable(string longitud, int palabras)
    {
        var (minimo, maximo) = Palabras(longitud);
        return palabras >= minimo * 2 / 3 && palabras <= maximo * 5 / 4;
    }

    public static int ContarPalabras(string texto) =>
        texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}

/// <summary>RN-047: segunda persona ("Lograste…", al alumno) o tercera ("Logró…").</summary>
public static class PersonaObservacion
{
    public const string Segunda = "segunda";
    public const string Tercera = "tercera";

    public static readonly IReadOnlyList<string> Todas = [Segunda, Tercera];
}

/// <summary>RF-043: cómo quiere el docente que suenen sus observaciones. Una fila por docente.</summary>
public sealed class EstiloObservaciones
{
    public const int MaximoEjemplos = 3;
    public const int LongitudEjemplo = 800;

    private EstiloObservaciones(Guid docenteId, DateTimeOffset ahora)
    {
        DocenteId = docenteId;
        CreatedAt = ahora;
        UpdatedAt = ahora;
    }

    private EstiloObservaciones()
    {
    }

    public Guid DocenteId { get; private set; }

    public string Longitud { get; private set; } = LongitudObservacion.Normal;

    public string Persona { get; private set; } = PersonaObservacion.Tercera;

    public List<string> Ejemplos { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>El estilo de quien nunca lo ha configurado: normal, en tercera persona y sin ejemplos.</summary>
    public static EstiloObservaciones PorDefecto(Guid docenteId, DateTimeOffset ahora) => new(docenteId, ahora);

    public Resultado Actualizar(string longitud, string persona, IReadOnlyList<string> ejemplos, DateTimeOffset ahora)
    {
        var limpios = ejemplos.Select(e => e.Trim()).Where(e => e.Length > 0).ToList();
        if (!LongitudObservacion.Todas.Contains(longitud) || !PersonaObservacion.Todas.Contains(persona)
            || limpios.Count > MaximoEjemplos || limpios.Any(e => e.Length > LongitudEjemplo))
        {
            return Resultado.Fallo(ErroresCierre.EstiloInvalido);
        }

        Longitud = longitud;
        Persona = persona;
        Ejemplos = limpios;
        UpdatedAt = ahora;
        return Resultado.Exito();
    }
}

/// <summary>
/// Una llamada a la IA (reglas/05-ia, Modelo y llamadas): modelo, tokens, duración y resultado, nunca el contenido.
/// Sirve para el límite por hora (RNF-004) y para el consumo por grupo (RNF-022).
/// </summary>
public sealed class LlamadaIa : Entidad
{
    public LlamadaIa(
        Guid docenteId, Guid grupoId, string modelo, string versionPrompt, int tokensEntrada, int tokensSalida,
        int tokensCacheLeidos, int duracionMs, string resultado, DateTimeOffset ahora)
        : base(ahora)
    {
        DocenteId = docenteId;
        GrupoId = grupoId;
        Modelo = modelo;
        VersionPrompt = versionPrompt;
        TokensEntrada = tokensEntrada;
        TokensSalida = tokensSalida;
        TokensCacheLeidos = tokensCacheLeidos;
        DuracionMs = duracionMs;
        Resultado = resultado;
    }

    private LlamadaIa()
    {
    }

    public Guid DocenteId { get; private set; }

    public Guid GrupoId { get; private set; }

    public string Modelo { get; private set; } = string.Empty;

    public string VersionPrompt { get; private set; } = string.Empty;

    public int TokensEntrada { get; private set; }

    public int TokensSalida { get; private set; }

    public int TokensCacheLeidos { get; private set; }

    public int DuracionMs { get; private set; }

    /// <summary><c>ok</c> o un <see cref="FalloGeneracion"/>.</summary>
    public string Resultado { get; private set; } = string.Empty;
}

/// <summary>Errores de negocio del cierre y las observaciones (SPEC-04).</summary>
public static class ErroresCierre
{
    public static readonly ErrorNegocio RubrosNoSuman100 = new("rubros_no_suman_100", "Para iniciar el cierre, los pesos de los rubros deben sumar 100.");
    public static readonly ErrorNegocio SinCalificaciones = new("sin_calificaciones", "Para iniciar el cierre, califica al menos una actividad.");
    public static readonly ErrorNegocio RequierePro = new("requiere_pro", "El cierre con observaciones es del plan Pro. Contrata Pro para este grupo.");
    public static readonly ErrorNegocio PeriodoSinCierre = new("periodo_sin_cierre", "Primero inicia el cierre del periodo.");
    public static readonly ErrorNegocio ObservacionAprobada = new("observacion_aprobada", "La observación ya está aprobada. Reábrela para cambiarla.");
    public static readonly ErrorNegocio ObservacionPublicada = new("observacion_publicada", "La boleta ya se publicó; la observación no se puede cambiar.");
    public static readonly ErrorNegocio EnGeneracion = new("en_generacion", "La observación se está generando. Espera a que termine.");
    public static readonly ErrorNegocio LimiteRegeneraciones = new("limite_regeneraciones", "Ya pediste 5 versiones de esta observación. Edítala o escríbela a mano.");
    public static readonly ErrorNegocio LimiteIa = new("limite_ia", "Llegaste al límite de observaciones generadas por hora. Intenta más tarde.");
    public static readonly ErrorNegocio ObservacionVacia = new("observacion_vacia", "Escribe la observación antes de aprobarla.");
    public static readonly ErrorNegocio ObservacionLarga = new("observacion_larga", "La observación tiene hasta 1,500 caracteres.");
    public static readonly ErrorNegocio InstruccionInvalida = new("instruccion_invalida", "La instrucción tiene hasta 200 caracteres.");
    public static readonly ErrorNegocio EstiloInvalido = new("estilo_invalido", "Elige una longitud y una persona, y hasta 3 ejemplos de hasta 800 caracteres.");

    public static readonly IReadOnlyList<ErrorNegocio> Todos =
    [
        RubrosNoSuman100, SinCalificaciones, RequierePro, PeriodoSinCierre, ObservacionAprobada, ObservacionPublicada,
        EnGeneracion, LimiteRegeneraciones, LimiteIa, ObservacionVacia, ObservacionLarga, InstruccionInvalida, EstiloInvalido,
    ];
}
