using AulaLista.Application.Comun;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Cuentas;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace AulaLista.Application.Cuentas;

public sealed record PerfilDocente(
    string Correo,
    bool CorreoVerificado,
    string? Nombre,
    string? Nivel,
    string? EscuelaNombre,
    string? EscuelaCct,
    string? EntidadFederativa,
    string ZonaHoraria,
    bool PerfilCompleto,
    bool TieneContrasena,
    bool ConGoogle,
    IReadOnlyList<DocumentoPendiente> AceptacionesPendientes,
    bool DeclaracionDatosAceptada);

/// <summary>Documento legal cuya versión vigente el docente no ha aceptado (RN-003).</summary>
public sealed record DocumentoPendiente(string Documento, string Version);

public sealed class ObtenerPerfilCasoUso(
    IRepositorioCuentas cuentas,
    IRepositorioDocentes docentes,
    IContextoUsuario contexto,
    IOptions<OpcionesCuentas> opciones)
{
    public async Task<PerfilDocente?> EjecutarAsync(CancellationToken ct)
    {
        var docenteId = contexto.DocenteId ?? throw new InvalidOperationException("Requiere un docente autenticado.");
        var cuenta = await cuentas.PorIdAsync(docenteId, ct);
        var docente = await docentes.PorIdAsync(docenteId, ct);
        if (cuenta is not { EstaActiva: true } || docente is null)
        {
            return null;
        }

        var aceptaciones = await docentes.AceptacionesAsync(docenteId, ct);
        var versiones = opciones.Value.VersionesLegales;
        bool Aceptada(string documento) =>
            aceptaciones.Any(a => a.Documento == documento && a.Version == versiones.Vigente(documento));

        var pendientes = new[] { DocumentoLegal.Terminos, DocumentoLegal.Privacidad }
            .Where(d => !Aceptada(d))
            .Select(d => new DocumentoPendiente(d, versiones.Vigente(d)!))
            .ToList();

        return new PerfilDocente(
            cuenta.Correo,
            cuenta.CorreoVerificado,
            docente.Nombre,
            docente.Nivel,
            docente.EscuelaNombre,
            docente.EscuelaCct,
            docente.EntidadFederativa,
            docente.ZonaHoraria,
            docente.PerfilCompleto,
            cuenta.HashContrasena is not null,
            cuenta.GoogleSub is not null,
            pendientes,
            Aceptada(DocumentoLegal.DeclaracionDatos));
    }
}

public sealed record ActualizarPerfil(string Nombre, string Nivel, string EscuelaNombre, string? EscuelaCct, string EntidadFederativa);

internal sealed class ActualizarPerfilValidador : AbstractValidator<ActualizarPerfil>
{
    public ActualizarPerfilValidador()
    {
        RuleFor(c => c.Nombre).NotEmpty().MaximumLength(120);
        RuleFor(c => c.Nivel).NotEmpty().MaximumLength(20);
        RuleFor(c => c.EscuelaNombre).NotEmpty().MaximumLength(160);
        RuleFor(c => c.EscuelaCct).MaximumLength(10);
        RuleFor(c => c.EntidadFederativa).NotEmpty().MaximumLength(40);
    }
}

/// <summary>RF-004: nombre, nivel, escuela, CCT opcional y estado de la república.</summary>
public sealed class ActualizarPerfilCasoUso(
    IRepositorioDocentes docentes,
    IContextoUsuario contexto,
    IUnidadDeTrabajo unidad,
    TimeProvider reloj)
{
    public async Task<Resultado> EjecutarAsync(ActualizarPerfil comando, CancellationToken ct)
    {
        var docenteId = contexto.DocenteId ?? throw new InvalidOperationException("Requiere un docente autenticado.");
        var docente = await docentes.PorIdAsync(docenteId, ct) ?? throw new InvalidOperationException("Docente inexistente.");
        var resultado = docente.ActualizarPerfil(
            comando.Nombre, comando.Nivel, comando.EscuelaNombre, comando.EscuelaCct, comando.EntidadFederativa, reloj.GetUtcNow());
        if (resultado.EsExito)
        {
            await unidad.GuardarAsync(ct);
        }

        return resultado;
    }
}

public sealed record RegistrarAceptacion(string Documento, string Version);

/// <summary>RN-003 y RN-004: guarda la aceptación de la versión vigente de un documento legal.</summary>
public sealed class RegistrarAceptacionCasoUso(
    IRepositorioDocentes docentes,
    IContextoUsuario contexto,
    IUnidadDeTrabajo unidad,
    IOptions<OpcionesCuentas> opciones,
    TimeProvider reloj)
{
    public async Task<Resultado> EjecutarAsync(RegistrarAceptacion comando, CancellationToken ct)
    {
        var docenteId = contexto.DocenteId ?? throw new InvalidOperationException("Requiere un docente autenticado.");
        var vigente = opciones.Value.VersionesLegales.Vigente(comando.Documento);
        if (vigente is null || vigente != comando.Version)
        {
            return Resultado.Fallo(ErroresCuentas.VersionNoVigente);
        }

        var aceptaciones = await docentes.AceptacionesAsync(docenteId, ct);
        if (!aceptaciones.Any(a => a.Documento == comando.Documento && a.Version == vigente))
        {
            docentes.AgregarAceptacion(new AceptacionLegal(docenteId, comando.Documento, vigente, reloj.GetUtcNow()));
            await unidad.GuardarAsync(ct);
        }

        return Resultado.Exito();
    }
}
