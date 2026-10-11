using AulaLista.Application.Comun;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Cuentas;
using AulaLista.Domain.Operacion;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace AulaLista.Application.Cuentas;

public sealed record RegistrarCuenta(string Correo, string Contrasena, bool AceptaTerminos, bool AceptaPrivacidad);

internal sealed class RegistrarCuentaValidador : AbstractValidator<RegistrarCuenta>
{
    public RegistrarCuentaValidador()
    {
        RuleFor(c => c.Correo).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(c => c.Contrasena).NotEmpty().MinimumLength(ReglasContrasena.LongitudMinima).MaximumLength(128);
    }
}

public static class ReglasContrasena
{
    // RN-002
    public const int LongitudMinima = 10;
}

/// <summary>RF-001, RF-005: crea la cuenta, registra las aceptaciones legales y envía el correo de verificación.</summary>
public sealed class RegistrarCuentaCasoUso(
    IRepositorioCuentas cuentas,
    IRepositorioDocentes docentes,
    IHasherContrasenas hasher,
    IListaContrasenasComunes comunes,
    IContextoUsuario contexto,
    EmisorSesiones emisor,
    IEnvioCorreo correo,
    IUnidadDeTrabajo unidad,
    IOptions<OpcionesCuentas> opciones,
    TimeProvider reloj)
{
    public async Task<Resultado<SesionEmitida>> EjecutarAsync(RegistrarCuenta comando, CancellationToken ct)
    {
        // RN-003
        if (!comando.AceptaTerminos || !comando.AceptaPrivacidad)
        {
            return Resultado.Fallo<SesionEmitida>(ErroresCuentas.AceptacionRequerida);
        }

        // RN-002
        if (comunes.Contiene(comando.Contrasena))
        {
            return Resultado.Fallo<SesionEmitida>(ErroresCuentas.ContrasenaComun);
        }

        if (await cuentas.PorCorreoAsync(Cuenta.NormalizarCorreo(comando.Correo), ct) is not null)
        {
            return Resultado.Fallo<SesionEmitida>(ErroresCuentas.CorreoRegistrado);
        }

        var ahora = reloj.GetUtcNow();
        var cuenta = Cuenta.ConContrasena(comando.Correo, hasher.Hash(comando.Contrasena), ahora);
        cuentas.Agregar(cuenta);
        contexto.AsumirDocente(cuenta.Id);
        docentes.Agregar(new Docente(cuenta.Id, ahora));
        var versiones = opciones.Value.VersionesLegales;
        docentes.AgregarAceptacion(new AceptacionLegal(cuenta.Id, DocumentoLegal.Terminos, versiones.Terminos, ahora));
        docentes.AgregarAceptacion(new AceptacionLegal(cuenta.Id, DocumentoLegal.Privacidad, versiones.Privacidad, ahora));

        var (token, hash) = TokensSeguros.Generar();
        cuentas.AgregarToken(new TokenCuenta(
            cuenta.Id, PropositoToken.VerificarCorreo, hash, ahora.AddHours(opciones.Value.HorasTokenVerificacion), ahora));
        var sesion = emisor.Nueva(cuenta, MetodoAutenticacion.Contrasena);
        await unidad.GuardarAsync(ct);

        await correo.EnviarAsync(CorreosCuentas.Verificacion(cuenta.Correo, opciones.Value.UrlWeb, token), ct);
        return Resultado.Exito(sesion);
    }
}

public sealed record IniciarSesion(string Correo, string Contrasena);

internal sealed class IniciarSesionValidador : AbstractValidator<IniciarSesion>
{
    public IniciarSesionValidador()
    {
        RuleFor(c => c.Correo).NotEmpty().MaximumLength(254);
        RuleFor(c => c.Contrasena).NotEmpty().MaximumLength(128);
    }
}

/// <summary>Inicio de sesión con correo y contraseña, con límite de intentos por cuenta (RNF-004).</summary>
public sealed class IniciarSesionCasoUso(
    IRepositorioCuentas cuentas,
    IHasherContrasenas hasher,
    ILimitadorIntentos limitador,
    EmisorSesiones emisor,
    IUnidadDeTrabajo unidad,
    IOptions<OpcionesCuentas> opciones)
{
    public async Task<Resultado<SesionEmitida>> EjecutarAsync(IniciarSesion comando, CancellationToken ct)
    {
        var correo = Cuenta.NormalizarCorreo(comando.Correo);
        if (!limitador.Intentar($"sesion:{correo}", opciones.Value.IntentosInicioSesionPorMinuto, TimeSpan.FromMinutes(1)))
        {
            return Resultado.Fallo<SesionEmitida>(ErroresCuentas.DemasiadosIntentos);
        }

        var cuenta = await cuentas.PorCorreoAsync(correo, ct);
        if (cuenta is not { EstaActiva: true, HashContrasena: { } hash } || !hasher.Verificar(hash, comando.Contrasena))
        {
            return Resultado.Fallo<SesionEmitida>(ErroresCuentas.CredencialesInvalidas);
        }

        var sesion = emisor.Nueva(cuenta, MetodoAutenticacion.Contrasena);
        await unidad.GuardarAsync(ct);
        return Resultado.Exito(sesion);
    }
}

/// <summary>Rota el token de actualización. Reusar uno ya usado revoca todas las sesiones (SPEC-01, Sesión).</summary>
public sealed class RenovarSesionCasoUso(
    IRepositorioCuentas cuentas,
    IAuditoria auditoria,
    EmisorSesiones emisor,
    IUnidadDeTrabajo unidad,
    TimeProvider reloj)
{
    public async Task<Resultado<SesionEmitida>> EjecutarAsync(string? tokenActualizacion, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(tokenActualizacion))
        {
            return Resultado.Fallo<SesionEmitida>(ErroresCuentas.SesionInvalida);
        }

        var ahora = reloj.GetUtcNow();
        var sesion = await cuentas.SesionPorHashAsync(TokensSeguros.Hash(tokenActualizacion), ct);
        if (sesion is null)
        {
            return Resultado.Fallo<SesionEmitida>(ErroresCuentas.SesionInvalida);
        }

        if (sesion.UsadaEn is not null && sesion.RevocadaEn is null)
        {
            await cuentas.RevocarSesionesAsync(sesion.CuentaId, ahora, ct);
            auditoria.Registrar(AccionAuditoria.ReutilizacionTokenActualizacion, "cuenta", sesion.CuentaId, sesion.CuentaId);
            await unidad.GuardarAsync(ct);
            return Resultado.Fallo<SesionEmitida>(ErroresCuentas.SesionInvalida);
        }

        var cuenta = await cuentas.PorIdAsync(sesion.CuentaId, ct);
        if (!sesion.EstaVigente(ahora) || cuenta is not { EstaActiva: true })
        {
            return Resultado.Fallo<SesionEmitida>(ErroresCuentas.SesionInvalida);
        }

        var nueva = emisor.Rotar(sesion);
        await unidad.GuardarAsync(ct);
        return Resultado.Exito(nueva);
    }
}

/// <summary>Cierra la sesión de este dispositivo.</summary>
public sealed class CerrarSesionCasoUso(IRepositorioCuentas cuentas, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task EjecutarAsync(string? tokenActualizacion, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(tokenActualizacion))
        {
            return;
        }

        var sesion = await cuentas.SesionPorHashAsync(TokensSeguros.Hash(tokenActualizacion), ct);
        if (sesion is null)
        {
            return;
        }

        sesion.Revocar(reloj.GetUtcNow());
        await unidad.GuardarAsync(ct);
    }
}

/// <summary>RF-007: cierra la sesión en todos los dispositivos.</summary>
public sealed class CerrarTodasLasSesionesCasoUso(
    IRepositorioCuentas cuentas,
    IContextoUsuario contexto,
    IAuditoria auditoria,
    IUnidadDeTrabajo unidad,
    TimeProvider reloj)
{
    public async Task EjecutarAsync(CancellationToken ct)
    {
        var docenteId = contexto.DocenteId ?? throw new InvalidOperationException("Requiere un docente autenticado.");
        await cuentas.RevocarSesionesAsync(docenteId, reloj.GetUtcNow(), ct);
        auditoria.Registrar(AccionAuditoria.CierreDeTodasLasSesiones, "cuenta", docenteId, docenteId);
        await unidad.GuardarAsync(ct);
    }
}

public sealed record DatosGoogle(string Sub, string Correo, bool CorreoVerificado);

/// <summary>RF-002 y RN-005: entra con Google; si ya hay una cuenta con ese correo, se vincula.</summary>
public sealed class IniciarConGoogleCasoUso(
    IRepositorioCuentas cuentas,
    IRepositorioDocentes docentes,
    IContextoUsuario contexto,
    EmisorSesiones emisor,
    IUnidadDeTrabajo unidad,
    TimeProvider reloj)
{
    public async Task<Resultado<SesionEmitida>> EjecutarAsync(DatosGoogle datos, CancellationToken ct)
    {
        if (!datos.CorreoVerificado)
        {
            return Resultado.Fallo<SesionEmitida>(ErroresCuentas.GoogleSinCorreoVerificado);
        }

        var ahora = reloj.GetUtcNow();
        var cuenta = await cuentas.PorGoogleSubAsync(datos.Sub, ct)
            ?? await cuentas.PorCorreoAsync(Cuenta.NormalizarCorreo(datos.Correo), ct);

        if (cuenta is null)
        {
            // Las aceptaciones legales se piden en la bienvenida (RN-003).
            cuenta = Cuenta.ConGoogle(datos.Correo, datos.Sub, ahora);
            cuentas.Agregar(cuenta);
            contexto.AsumirDocente(cuenta.Id);
            docentes.Agregar(new Docente(cuenta.Id, ahora));
        }
        else if (!cuenta.EstaActiva)
        {
            return Resultado.Fallo<SesionEmitida>(ErroresCuentas.CredencialesInvalidas);
        }
        else if (cuenta.GoogleSub is null)
        {
            cuenta.VincularGoogle(datos.Sub, ahora);
        }

        var sesion = emisor.Nueva(cuenta, MetodoAutenticacion.Google);
        await unidad.GuardarAsync(ct);
        return Resultado.Exito(sesion);
    }
}
