using AulaLista.Domain.Comun;

namespace AulaLista.Domain.Cuentas;

/// <summary>Errores de negocio del módulo de cuentas (SPEC-01).</summary>
public static class ErroresCuentas
{
    public static readonly ErrorNegocio CorreoRegistrado = new("correo_registrado", "Ya existe una cuenta con este correo.");
    public static readonly ErrorNegocio ContrasenaComun = new("contrasena_comun", "Esa contraseña es muy común. Elige otra.");
    public static readonly ErrorNegocio AceptacionRequerida = new("aceptacion_requerida", "Debes aceptar los términos y el aviso de privacidad.");
    public static readonly ErrorNegocio CredencialesInvalidas = new("credenciales_invalidas", "Correo o contraseña incorrectos.");
    public static readonly ErrorNegocio DemasiadosIntentos = new("demasiados_intentos", "Demasiados intentos. Espera un momento e inténtalo de nuevo.");
    public static readonly ErrorNegocio SesionInvalida = new("sesion_invalida", "Tu sesión terminó. Inicia sesión de nuevo.");
    public static readonly ErrorNegocio TokenInvalido = new("token_invalido", "El enlace no es válido o ya se usó.");
    public static readonly ErrorNegocio TokenVencido = new("token_vencido", "El enlace venció. Pide uno nuevo.");
    public static readonly ErrorNegocio NivelInvalido = new("nivel_invalido", "El nivel debe ser primaria o secundaria.");
    public static readonly ErrorNegocio EntidadFederativaInvalida = new("entidad_federativa_invalida", "Elige un estado de la república.");
    public static readonly ErrorNegocio CctInvalida = new("cct_invalida", "La CCT tiene 10 caracteres, por ejemplo 25DPR0123X.");
    public static readonly ErrorNegocio VersionNoVigente = new("version_no_vigente", "Esa versión del documento ya no está vigente.");
    public static readonly ErrorNegocio ReautenticacionRequerida = new("reautenticacion_requerida", "Confirma tu identidad para continuar.");
    public static readonly ErrorNegocio ExportacionNoDisponible = new("exportacion_no_disponible", "La exportación no está lista o ya venció.");
    public static readonly ErrorNegocio GoogleSinCorreoVerificado = new("google_sin_correo_verificado", "Tu cuenta de Google no tiene el correo verificado.");

    public static readonly IReadOnlyList<ErrorNegocio> Todos =
    [
        CorreoRegistrado, ContrasenaComun, AceptacionRequerida, CredencialesInvalidas, DemasiadosIntentos,
        SesionInvalida, TokenInvalido, TokenVencido, NivelInvalido, EntidadFederativaInvalida, CctInvalida,
        VersionNoVigente, ReautenticacionRequerida, ExportacionNoDisponible, GoogleSinCorreoVerificado,
    ];
}
