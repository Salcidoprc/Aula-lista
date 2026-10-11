using AulaLista.Domain.Cuentas;

namespace AulaLista.Application.Cuentas;

/// <summary>Cuentas y tokens: tablas de autenticación sin RLS, solo las usa este módulo (ADR-0013).</summary>
public interface IRepositorioCuentas
{
    Task<Cuenta?> PorCorreoAsync(string correo, CancellationToken cancellationToken);

    Task<Cuenta?> PorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Cuenta?> PorGoogleSubAsync(string googleSub, CancellationToken cancellationToken);

    void Agregar(Cuenta cuenta);

    void AgregarToken(TokenCuenta token);

    Task<TokenCuenta?> TokenPorHashAsync(string hash, string proposito, CancellationToken cancellationToken);

    Task<int> ContarTokensDesdeAsync(Guid cuentaId, string proposito, DateTimeOffset desde, CancellationToken cancellationToken);

    void AgregarSesion(Sesion sesion);

    Task<Sesion?> SesionPorHashAsync(string hash, CancellationToken cancellationToken);

    Task<Sesion?> SesionVigenteDeFamiliaAsync(Guid familiaId, DateTimeOffset ahora, CancellationToken cancellationToken);

    Task RevocarSesionesAsync(Guid cuentaId, DateTimeOffset ahora, CancellationToken cancellationToken);
}

/// <summary>Perfil, aceptaciones y exportaciones del docente: tablas con RLS por docente.</summary>
public interface IRepositorioDocentes
{
    Task<Docente?> PorIdAsync(Guid id, CancellationToken cancellationToken);

    void Agregar(Docente docente);

    void AgregarAceptacion(AceptacionLegal aceptacion);

    Task<IReadOnlyList<AceptacionLegal>> AceptacionesAsync(Guid docenteId, CancellationToken cancellationToken);

    void AgregarExportacion(Exportacion exportacion);

    Task<Exportacion?> ExportacionPorIdAsync(Guid id, CancellationToken cancellationToken);
}

public interface IHasherContrasenas
{
    string Hash(string contrasena);

    bool Verificar(string hash, string contrasena);
}

/// <summary>Contraseñas de listas filtradas comunes (RN-002).</summary>
public interface IListaContrasenasComunes
{
    bool Contiene(string contrasena);
}

public interface IEmisorTokensAcceso
{
    TokenAcceso Emitir(Guid docenteId, Guid familiaSesionId);
}

public sealed record TokenAcceso(string Token, DateTimeOffset ExpiraEn);

/// <summary>Arma el Excel y el PDF de la exportación y los guarda; devuelve sus rutas.</summary>
public interface IGeneradorExportacion
{
    Task<(string RutaExcel, string RutaPdf)> GenerarAsync(DatosExportacion datos, CancellationToken cancellationToken);
}

/// <summary>Lo que se exporta. Cada módulo agrega sus datos conforme llega (H2: grupos y alumnos; H3: calificaciones).</summary>
public sealed record DatosExportacion(
    Guid ExportacionId,
    string Correo,
    Docente Perfil,
    IReadOnlyList<AceptacionLegal> Aceptaciones,
    DateTimeOffset GeneradaEn);

/// <summary>Borra las cuentas cuya eliminación se pidió hace 30 días o más (RN-006, RNF-009).</summary>
public interface IRetencionCuentas
{
    Task<int> EliminarVencidasAsync(DateTimeOffset limite, CancellationToken cancellationToken);
}
