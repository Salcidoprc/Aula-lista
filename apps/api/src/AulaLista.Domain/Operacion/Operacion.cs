using AulaLista.Domain.Comun;

namespace AulaLista.Domain.Operacion;

/// <summary>
/// RN-095 y RF-093: lo que hizo un administrador en el panel, con los valores antes y después en JSON. Inmutable; nunca
/// lleva datos de alumnos ni de tutores (RN-090).
/// </summary>
public sealed class RegistroAuditoriaAdmin : Entidad
{
    public RegistroAuditoriaAdmin(Guid adminId, string accion, string entidad, Guid? entidadId, string? antes, string? despues, DateTimeOffset ahora)
        : base(ahora)
    {
        AdminId = adminId;
        Accion = accion;
        Entidad = entidad;
        EntidadId = entidadId;
        Antes = antes;
        Despues = despues;
        Fecha = ahora;
    }

    private RegistroAuditoriaAdmin()
    {
    }

    public Guid AdminId { get; private set; }

    public string Accion { get; private set; } = string.Empty;

    public string Entidad { get; private set; } = string.Empty;

    public Guid? EntidadId { get; private set; }

    public string? Antes { get; private set; }

    public string? Despues { get; private set; }

    public DateTimeOffset Fecha { get; private set; }
}

public static class AccionAuditoriaAdmin
{
    public const string TipoRubroCreado = "tipo_rubro_creado";
    public const string TipoRubroEditado = "tipo_rubro_editado";
    public const string TipoRubroDesactivado = "tipo_rubro_desactivado";
    public const string TipoRubroActivado = "tipo_rubro_activado";
}

/// <summary>Eventos de producto de SPEC-09 (RF-090). Solo ids internos y números; nunca datos personales.</summary>
public static class EventoProducto
{
    public const string DocenteRegistrado = "docente_registrado";
    public const string GrupoCreado = "grupo_creado";
    public const string CalificacionCapturada = "calificacion_capturada";
    public const string CierreIniciado = "cierre_iniciado";
    public const string ObservacionAprobada = "observacion_aprobada";
    public const string BoletasPublicadas = "boletas_publicadas";
    public const string EnlaceTutorAbierto = "enlace_tutor_abierto";
    public const string SuscripcionActivada = "suscripcion_activada";
}

/// <summary>De dónde viene el error de un trabajo en segundo plano que ve el panel (SPEC-09, Errores).</summary>
public static class OrigenError
{
    public const string Ia = "ia";
    public const string Whatsapp = "whatsapp";
    public const string Correo = "correo";
    public const string Push = "push";
    public const string Pagos = "pagos";
    public const string Boletas = "boletas";
    public const string Exportacion = "exportacion";
    public const string Retencion = "retencion";

    public static readonly IReadOnlyList<string> Todos = [Ia, Whatsapp, Correo, Push, Pagos, Boletas, Exportacion, Retencion];
}

/// <summary>
/// Distancia de edición normalizada entre el borrador de la IA y el texto aprobado (0 igual, 1 todo distinto), para la
/// métrica "observaciones aceptadas sin cambios mayores" del piloto: menos de 0.3 es un cambio menor.
/// </summary>
public static class DistanciaEdicion
{
    public const decimal CambioMayor = 0.3m;

    public static decimal Entre(string original, string final)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(final);
        var largo = Math.Max(original.Length, final.Length);
        if (largo == 0)
        {
            return 0m;
        }

        // Levenshtein con dos filas: las observaciones miden a lo más unos cuantos miles de caracteres.
        var anterior = new int[final.Length + 1];
        var actual = new int[final.Length + 1];
        for (var j = 0; j <= final.Length; j++)
        {
            anterior[j] = j;
        }

        for (var i = 1; i <= original.Length; i++)
        {
            actual[0] = i;
            for (var j = 1; j <= final.Length; j++)
            {
                var costo = original[i - 1] == final[j - 1] ? 0 : 1;
                actual[j] = Math.Min(Math.Min(actual[j - 1] + 1, anterior[j] + 1), anterior[j - 1] + costo);
            }

            (anterior, actual) = (actual, anterior);
        }

        return decimal.Round((decimal)anterior[final.Length] / largo, 4);
    }
}

public static class ErroresOperacion
{
    public static readonly ErrorNegocio TipoRubroDatosInvalidos = new(
        "tipo_rubro_datos_invalidos",
        "El tipo lleva nombre de hasta 40 caracteres, descripción de hasta 200, un icono, de 1 a 5 nombres sugeridos y un orden de 1 a 999.");

    public static readonly ErrorNegocio TipoRubroRepetido = new("tipo_rubro_repetido", "Ya hay un tipo de rubro con ese nombre.");

    public static readonly ErrorNegocio ComportamientoInexistente = new(
        "comportamiento_inexistente", "Ese comportamiento no existe en esta versión de Aula Lista.");

    public static readonly ErrorNegocio ComportamientoEnUso = new(
        "comportamiento_en_uso", "Este tipo ya lo usan rubros: su comportamiento no se cambia.");

    public static readonly IReadOnlyList<ErrorNegocio> Todos = [TipoRubroDatosInvalidos, TipoRubroRepetido, ComportamientoInexistente, ComportamientoEnUso];
}
