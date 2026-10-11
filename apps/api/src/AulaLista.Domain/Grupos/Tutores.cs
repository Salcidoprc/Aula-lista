using AulaLista.Domain.Comun;

namespace AulaLista.Domain.Grupos;

/// <summary>Teléfonos en E.164 (RN-014). Diez dígitos se toman como de México (+52).</summary>
public static class Telefono
{
    /// <summary>Normaliza a E.164 o devuelve nulo si no es un número válido.</summary>
    public static string? Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var limpio = texto.Trim();
        var conMas = limpio.StartsWith('+');
        if (limpio.Any(c => !char.IsAsciiDigit(c) && c is not (' ' or '-' or '(' or ')' or '.' or '+')) || limpio.LastIndexOf('+') > 0)
        {
            return null;
        }

        var digitos = new string(limpio.Where(char.IsAsciiDigit).ToArray());
        if (!conMas)
        {
            // 10 dígitos de México; o con 52, 521 (celular antiguo), 044 o 045 al frente.
            return digitos.Length switch
            {
                10 => "+52" + digitos,
                12 when digitos.StartsWith("52", StringComparison.Ordinal) => "+" + digitos,
                13 when digitos.StartsWith("521", StringComparison.Ordinal)
                    || digitos.StartsWith("044", StringComparison.Ordinal)
                    || digitos.StartsWith("045", StringComparison.Ordinal) => "+52" + digitos[3..],
                _ => null,
            };
        }

        if (digitos.StartsWith("52", StringComparison.Ordinal))
        {
            return digitos.Length switch
            {
                12 => "+" + digitos,
                13 when digitos[2] == '1' => "+52" + digitos[3..],
                _ => null,
            };
        }

        return digitos.Length is >= 8 and <= 15 && digitos[0] != '0' ? "+" + digitos : null;
    }
}

/// <summary>
/// Tutor compartido entre docentes, identificado por su teléfono (RN-015, RF-017). Un docente nunca lo lee ni
/// lo escribe directo: lo vincula con la función <c>vincular_tutor</c> y ve solo su <see cref="VinculoTutor"/>.
/// </summary>
public sealed class Tutor : Entidad
{
    private Tutor()
    {
    }

    public string TelefonoE164 { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public string? Correo { get; private set; }
}

/// <summary>
/// Vínculo de un alumno con un tutor, con lo que capturó el docente (nombre, correo, parentesco). De 0 a 3 por
/// alumno (RN-016). El docente nunca ve lo que capturó otro docente para el mismo tutor (ADR-0014).
/// </summary>
public sealed class VinculoTutor : Entidad
{
    public const int MaximoPorAlumno = 3;
    public const int LongitudNombre = 120;
    public const int LongitudCorreo = 254;
    public const int LongitudParentesco = 30;

    public VinculoTutor(Alumno alumno, Guid tutorId, string telefonoE164, DatosTutor datos, DateTimeOffset ahora)
        : base(ahora)
    {
        AlumnoId = alumno.Id;
        DocenteId = alumno.DocenteId;
        TutorId = tutorId;
        TelefonoE164 = telefonoE164;
        Asignar(datos);
    }

    private VinculoTutor()
    {
    }

    public Guid AlumnoId { get; private set; }

    public Guid TutorId { get; private set; }

    public Guid DocenteId { get; private set; }

    /// <summary>Copia del teléfono para mostrarlo sin leer <c>tutor</c>.</summary>
    public string TelefonoE164 { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public string? Correo { get; private set; }

    public string? Parentesco { get; private set; }

    public void Editar(DatosTutor datos, DateTimeOffset ahora)
    {
        Asignar(datos);
        MarcarActualizada(ahora);
    }

    private void Asignar(DatosTutor datos)
    {
        Nombre = datos.Nombre.Trim();
        Correo = string.IsNullOrWhiteSpace(datos.Correo) ? null : datos.Correo.Trim().ToLowerInvariant();
        Parentesco = string.IsNullOrWhiteSpace(datos.Parentesco) ? null : datos.Parentesco.Trim();
    }
}

/// <summary>Lo que el docente captura de un tutor.</summary>
public sealed record DatosTutor(string Nombre, string? Correo, string? Parentesco);

public static class OrigenImportacion
{
    public const string Excel = "excel";
    public const string Csv = "csv";
    public const string Texto = "texto";
}

/// <summary>
/// Importación de alumnos (RF-013, RF-014). Solo guarda metadatos, sin nombres: la vista previa vive en la web
/// y las filas llegan al confirmar (ADR-0014).
/// </summary>
public sealed class Importacion : Entidad
{
    public Importacion(Grupo grupo, string origen, DateTimeOffset ahora)
        : base(ahora)
    {
        GrupoId = grupo.Id;
        DocenteId = grupo.DocenteId;
        Origen = origen;
    }

    private Importacion()
    {
    }

    public Guid GrupoId { get; private set; }

    public Guid DocenteId { get; private set; }

    public string Origen { get; private set; } = OrigenImportacion.Texto;

    public DateTimeOffset? ConfirmadaEn { get; private set; }

    public Resultado Confirmar(DateTimeOffset ahora)
    {
        if (ConfirmadaEn is not null)
        {
            return Resultado.Fallo(ErroresGrupos.ImportacionConfirmada);
        }

        ConfirmadaEn = ahora;
        MarcarActualizada(ahora);
        return Resultado.Exito();
    }
}
