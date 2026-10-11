using AulaLista.Application.Comun;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Cuentas;
using AulaLista.Domain.Operacion;
using Microsoft.Extensions.Options;

namespace AulaLista.Application.Cuentas;

/// <summary>RF-008: encola la exportación; al terminar se avisa por correo.</summary>
public sealed class SolicitarExportacionCasoUso(
    IRepositorioDocentes docentes,
    IContextoUsuario contexto,
    IColaTrabajos cola,
    IAuditoria auditoria,
    IUnidadDeTrabajo unidad,
    TimeProvider reloj)
{
    public async Task<Guid> EjecutarAsync(CancellationToken ct)
    {
        var docenteId = contexto.DocenteId ?? throw new InvalidOperationException("Requiere un docente autenticado.");
        var exportacion = new Exportacion(docenteId, reloj.GetUtcNow());
        docentes.AgregarExportacion(exportacion);
        auditoria.Registrar(AccionAuditoria.ExportacionSolicitada, "exportacion", exportacion.Id, docenteId);
        await unidad.GuardarAsync(ct);
        cola.EncolarExportacion(docenteId, exportacion.Id);
        return exportacion.Id;
    }
}

/// <summary>Trabajo en segundo plano: genera los archivos y manda el correo. Idempotente.</summary>
public sealed class GenerarExportacionTrabajo(
    IRepositorioCuentas cuentas,
    IRepositorioDocentes docentes,
    IContextoUsuario contexto,
    IGeneradorExportacion generador,
    IEnvioCorreo correo,
    IUnidadDeTrabajo unidad,
    IOptions<OpcionesCuentas> opciones,
    TimeProvider reloj)
{
    public async Task EjecutarAsync(Guid docenteId, Guid exportacionId, CancellationToken ct)
    {
        contexto.AsumirDocente(docenteId);
        var exportacion = await docentes.ExportacionPorIdAsync(exportacionId, ct);
        var cuenta = await cuentas.PorIdAsync(docenteId, ct);
        var docente = await docentes.PorIdAsync(docenteId, ct);
        if (exportacion is not { Estado: EstadoExportacion.Pendiente } || cuenta is not { EstaActiva: true } || docente is null)
        {
            return;
        }

        var ahora = reloj.GetUtcNow();
        var aceptaciones = await docentes.AceptacionesAsync(docenteId, ct);
        var (excel, pdf) = await generador.GenerarAsync(
            new DatosExportacion(exportacion.Id, cuenta.Correo, docente, aceptaciones, ahora), ct);
        exportacion.MarcarLista(excel, pdf, ahora);
        await unidad.GuardarAsync(ct);
        await correo.EnviarAsync(CorreosCuentas.ExportacionLista(cuenta.Correo, opciones.Value.UrlWeb, exportacion.Id), ct);
    }
}

public sealed record ArchivoExportacion(string Ruta, string Nombre, string TipoContenido);

/// <summary>Devuelve un archivo de una exportación propia y vigente.</summary>
public sealed class ObtenerArchivoExportacionCasoUso(IRepositorioDocentes docentes, TimeProvider reloj)
{
    public const string Excel = "excel";
    public const string Pdf = "pdf";

    public async Task<Resultado<ArchivoExportacion>> EjecutarAsync(Guid exportacionId, string formato, CancellationToken ct)
    {
        // RLS solo deja ver las exportaciones del docente de la solicitud.
        var exportacion = await docentes.ExportacionPorIdAsync(exportacionId, ct);
        if (exportacion is null || !exportacion.EstaDisponible(reloj.GetUtcNow()))
        {
            return Resultado.Fallo<ArchivoExportacion>(ErroresCuentas.ExportacionNoDisponible);
        }

        var fecha = exportacion.CreatedAt.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        return formato switch
        {
            Excel => Resultado.Exito(new ArchivoExportacion(
                exportacion.RutaExcel!, $"aula-lista-mis-datos-{fecha}.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")),
            Pdf => Resultado.Exito(new ArchivoExportacion(
                exportacion.RutaPdf!, $"aula-lista-mis-datos-{fecha}.pdf", "application/pdf")),
            _ => Resultado.Fallo<ArchivoExportacion>(ErroresCuentas.ExportacionNoDisponible),
        };
    }
}

public sealed record SolicitarEliminacion(string? Contrasena);

/// <summary>RN-006: desactiva la cuenta de inmediato, cierra sus sesiones y programa el borrado a 30 días.</summary>
public sealed class SolicitarEliminacionCasoUso(
    IRepositorioCuentas cuentas,
    IHasherContrasenas hasher,
    IContextoUsuario contexto,
    IAuditoria auditoria,
    IUnidadDeTrabajo unidad,
    IOptions<OpcionesCuentas> opciones,
    TimeProvider reloj)
{
    public async Task<Resultado> EjecutarAsync(SolicitarEliminacion comando, CancellationToken ct)
    {
        var docenteId = contexto.DocenteId ?? throw new InvalidOperationException("Requiere un docente autenticado.");
        var cuenta = await cuentas.PorIdAsync(docenteId, ct) ?? throw new InvalidOperationException("Cuenta inexistente.");
        var ahora = reloj.GetUtcNow();

        if (!await IdentidadConfirmadaAsync(cuenta, comando, ahora, ct))
        {
            return Resultado.Fallo(ErroresCuentas.ReautenticacionRequerida);
        }

        cuenta.SolicitarEliminacion(ahora);
        await cuentas.RevocarSesionesAsync(cuenta.Id, ahora, ct);
        // Los enlaces de tutor se revocan aquí cuando exista SPEC-06 (H5); RN-007 no toca vínculos ajenos.
        auditoria.Registrar(AccionAuditoria.EliminacionSolicitada, "cuenta", cuenta.Id, cuenta.Id);
        await unidad.GuardarAsync(ct);
        return Resultado.Exito();
    }

    private async Task<bool> IdentidadConfirmadaAsync(Cuenta cuenta, SolicitarEliminacion comando, DateTimeOffset ahora, CancellationToken ct)
    {
        if (cuenta.HashContrasena is { } hash)
        {
            return comando.Contrasena is not null && hasher.Verificar(hash, comando.Contrasena);
        }

        // Cuenta solo con Google: vale si inició sesión con Google hace poco.
        if (contexto.FamiliaSesionId is not { } familia)
        {
            return false;
        }

        var sesion = await cuentas.SesionVigenteDeFamiliaAsync(familia, ahora, ct);
        return sesion is { Metodo: MetodoAutenticacion.Google }
            && ahora - sesion.AutenticadaEn <= TimeSpan.FromMinutes(opciones.Value.MinutosReautenticacionGoogle);
    }
}

/// <summary>Trabajo diario de retención (RNF-009): borra las cuentas con eliminación pedida hace 30 días.</summary>
public sealed class RetencionCuentasTrabajo(IRetencionCuentas retencion, TimeProvider reloj)
{
    public static readonly TimeSpan PlazoBorrado = TimeSpan.FromDays(30);

    public Task<int> EjecutarAsync(CancellationToken ct) =>
        retencion.EliminarVencidasAsync(reloj.GetUtcNow() - PlazoBorrado, ct);
}
