using AulaLista.Domain.Comun;

namespace AulaLista.Domain.Tutores;

/// <summary>
/// Un enlace personal del tutor (RN-060): solo se guarda el hash de su token de 256 bits. Vence al terminar el ciclo
/// escolar más los días de gracia (RN-062). Reenviar crea otro enlace sin revocar los anteriores, porque el token
/// original no se puede recuperar; regenerar los revoca todos junto con sus sesiones (RN-063).
/// </summary>
public sealed class EnlaceTutor : Entidad
{
    public EnlaceTutor(Guid tutorId, Guid alumnoId, Guid docenteId, string tokenHash, DateTimeOffset expiraEn, DateTimeOffset ahora)
        : base(ahora)
    {
        TutorId = tutorId;
        AlumnoId = alumnoId;
        CreadoPorDocenteId = docenteId;
        TokenHash = tokenHash;
        ExpiraEn = expiraEn;
    }

    private EnlaceTutor()
    {
    }

    public Guid TutorId { get; private set; }

    /// <summary>Desde la ficha de qué alumno se envió; ahí se lleva al docente si el enlace se vuelve sospechoso.</summary>
    public Guid AlumnoId { get; private set; }

    public Guid CreadoPorDocenteId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiraEn { get; private set; }

    public DateTimeOffset? RevocadoEn { get; private set; }

    public DateTimeOffset? UltimoUsoEn { get; private set; }

    /// <summary>RN-065: se abrió en más dispositivos de los permitidos; se avisa una sola vez al docente.</summary>
    public DateTimeOffset? SospechosoEn { get; private set; }

    public bool EstaVigente(DateTimeOffset ahora) => RevocadoEn is null && ahora < ExpiraEn;

    public void RegistrarUso(DateTimeOffset ahora)
    {
        UltimoUsoEn = ahora;
        MarcarActualizada(ahora);
    }

    /// <summary>Marca el enlace como sospechoso; devuelve si es la primera vez, para avisar solo una.</summary>
    public bool MarcarSospechoso(DateTimeOffset ahora)
    {
        if (SospechosoEn is not null)
        {
            return false;
        }

        SospechosoEn = ahora;
        MarcarActualizada(ahora);
        return true;
    }

    public void Revocar(DateTimeOffset ahora)
    {
        RevocadoEn ??= ahora;
        MarcarActualizada(ahora);
    }
}

/// <summary>
/// La sesión del tutor en un navegador (RN-062): una cookie <c>HttpOnly</c> con un token aleatorio del que solo se
/// guarda el hash. Cada sesión cuenta como un dispositivo para RN-065.
/// </summary>
public sealed class SesionTutor : Entidad
{
    public SesionTutor(EnlaceTutor enlace, string tokenHash, DateTimeOffset expiraEn, DateTimeOffset ahora)
        : base(ahora)
    {
        TutorId = enlace.TutorId;
        EnlaceId = enlace.Id;
        TokenHash = tokenHash;
        ExpiraEn = expiraEn;
        UltimoUsoEn = ahora;
    }

    private SesionTutor()
    {
    }

    public Guid TutorId { get; private set; }

    public Guid EnlaceId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiraEn { get; private set; }

    public DateTimeOffset? RevocadaEn { get; private set; }

    public DateTimeOffset UltimoUsoEn { get; private set; }

    public bool EstaVigente(DateTimeOffset ahora) => RevocadaEn is null && ahora < ExpiraEn;

    /// <summary>Renovable (RN-062): cada uso corre el vencimiento; se escribe a lo más una vez al día.</summary>
    public bool Renovar(DateTimeOffset ahora, TimeSpan duracion)
    {
        if (ahora - UltimoUsoEn < TimeSpan.FromDays(1))
        {
            return false;
        }

        UltimoUsoEn = ahora;
        ExpiraEn = ahora + duracion;
        MarcarActualizada(ahora);
        return true;
    }

    public void Revocar(DateTimeOffset ahora)
    {
        RevocadaEn ??= ahora;
        MarcarActualizada(ahora);
    }
}

public static class ErroresTutores
{
    /// <summary>Token alterado, revocado o vencido: siempre el mismo error, sin decir si el enlace existió.</summary>
    public static readonly ErrorNegocio EnlaceInvalido = new("enlace_invalido", "Este enlace ya no funciona. Pide a la maestra o al maestro un enlace nuevo.");

    public static readonly IReadOnlyList<ErrorNegocio> Todos = [EnlaceInvalido];
}
