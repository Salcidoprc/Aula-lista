using AulaLista.Application.Comun;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Cuentas;
using AulaLista.Domain.Operacion;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace AulaLista.Application.Cuentas;

/// <summary>RF-001: confirma el correo con el token recibido (vale 24 horas).</summary>
public sealed class VerificarCorreoCasoUso(IRepositorioCuentas cuentas, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado> EjecutarAsync(string token, CancellationToken ct)
    {
        var registro = await cuentas.TokenPorHashAsync(TokensSeguros.Hash(token), PropositoToken.VerificarCorreo, ct);
        if (registro is null)
        {
            return Resultado.Fallo(ErroresCuentas.TokenInvalido);
        }

        var ahora = reloj.GetUtcNow();
        var uso = registro.Usar(ahora);
        if (uso.EsFallo)
        {
            return uso;
        }

        var cuenta = await cuentas.PorIdAsync(registro.CuentaId, ct);
        if (cuenta is not { EstaActiva: true })
        {
            return Resultado.Fallo(ErroresCuentas.TokenInvalido);
        }

        cuenta.VerificarCorreo(ahora);
        await unidad.GuardarAsync(ct);
        return Resultado.Exito();
    }
}

/// <summary>Reenvía el correo de verificación, máximo 3 por hora (SPEC-01, Casos borde).</summary>
public sealed class ReenviarVerificacionCasoUso(
    IRepositorioCuentas cuentas,
    IContextoUsuario contexto,
    IEnvioCorreo correo,
    IUnidadDeTrabajo unidad,
    IOptions<OpcionesCuentas> opciones,
    TimeProvider reloj)
{
    public async Task<Resultado> EjecutarAsync(CancellationToken ct)
    {
        var docenteId = contexto.DocenteId ?? throw new InvalidOperationException("Requiere un docente autenticado.");
        var cuenta = await cuentas.PorIdAsync(docenteId, ct) ?? throw new InvalidOperationException("Cuenta inexistente.");
        if (cuenta.CorreoVerificado)
        {
            return Resultado.Exito();
        }

        var ahora = reloj.GetUtcNow();
        // El correo enviado al registrarse no cuenta como reenvío.
        var desde = ahora.AddHours(-1);
        var enviados = await cuentas.ContarTokensDesdeAsync(cuenta.Id, PropositoToken.VerificarCorreo, desde, ct);
        var reenvios = cuenta.CreatedAt >= desde ? enviados - 1 : enviados;
        if (reenvios >= opciones.Value.ReenviosVerificacionPorHora)
        {
            return Resultado.Fallo(ErroresCuentas.DemasiadosIntentos);
        }

        var (token, hash) = TokensSeguros.Generar();
        cuentas.AgregarToken(new TokenCuenta(
            cuenta.Id, PropositoToken.VerificarCorreo, hash, ahora.AddHours(opciones.Value.HorasTokenVerificacion), ahora));
        await unidad.GuardarAsync(ct);
        await correo.EnviarAsync(CorreosCuentas.Verificacion(cuenta.Correo, opciones.Value.UrlWeb, token), ct);
        return Resultado.Exito();
    }
}

/// <summary>RF-003: pide el correo de recuperación. Siempre responde igual para no revelar si la cuenta existe.</summary>
public sealed class SolicitarRecuperacionCasoUso(
    IRepositorioCuentas cuentas,
    IEnvioCorreo correo,
    IUnidadDeTrabajo unidad,
    IOptions<OpcionesCuentas> opciones,
    TimeProvider reloj)
{
    public async Task EjecutarAsync(string correoSolicitado, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(correoSolicitado))
        {
            return;
        }

        var cuenta = await cuentas.PorCorreoAsync(Cuenta.NormalizarCorreo(correoSolicitado), ct);
        if (cuenta is not { EstaActiva: true })
        {
            return;
        }

        var ahora = reloj.GetUtcNow();
        if (await cuentas.ContarTokensDesdeAsync(cuenta.Id, PropositoToken.RestablecerContrasena, ahora.AddHours(-1), ct) >= opciones.Value.RecuperacionesPorHora)
        {
            return;
        }

        var (token, hash) = TokensSeguros.Generar();
        cuentas.AgregarToken(new TokenCuenta(cuenta.Id, PropositoToken.RestablecerContrasena, hash, ahora.AddMinutes(opciones.Value.MinutosTokenRecuperacion), ahora));
        await unidad.GuardarAsync(ct);
        await correo.EnviarAsync(CorreosCuentas.Recuperacion(cuenta.Correo, opciones.Value.UrlWeb, token), ct);
    }
}

public sealed record RestablecerContrasena(string Token, string Contrasena);

internal sealed class RestablecerContrasenaValidador : AbstractValidator<RestablecerContrasena>
{
    public RestablecerContrasenaValidador()
    {
        RuleFor(c => c.Token).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Contrasena).NotEmpty().MinimumLength(ReglasContrasena.LongitudMinima).MaximumLength(128);
    }
}

/// <summary>RF-003: fija la contraseña nueva con el token del correo y cierra todas las sesiones.</summary>
public sealed class RestablecerContrasenaCasoUso(
    IRepositorioCuentas cuentas,
    IHasherContrasenas hasher,
    IListaContrasenasComunes comunes,
    IAuditoria auditoria,
    IUnidadDeTrabajo unidad,
    TimeProvider reloj)
{
    public async Task<Resultado> EjecutarAsync(RestablecerContrasena comando, CancellationToken ct)
    {
        if (comunes.Contiene(comando.Contrasena))
        {
            return Resultado.Fallo(ErroresCuentas.ContrasenaComun);
        }

        var registro = await cuentas.TokenPorHashAsync(TokensSeguros.Hash(comando.Token), PropositoToken.RestablecerContrasena, ct);
        if (registro is null)
        {
            return Resultado.Fallo(ErroresCuentas.TokenInvalido);
        }

        var ahora = reloj.GetUtcNow();
        var uso = registro.Usar(ahora);
        if (uso.EsFallo)
        {
            return uso;
        }

        var cuenta = await cuentas.PorIdAsync(registro.CuentaId, ct);
        if (cuenta is not { EstaActiva: true })
        {
            return Resultado.Fallo(ErroresCuentas.TokenInvalido);
        }

        cuenta.CambiarContrasena(hasher.Hash(comando.Contrasena), ahora);
        // Abrir el enlace prueba que el correo es suyo.
        cuenta.VerificarCorreo(ahora);
        await cuentas.RevocarSesionesAsync(cuenta.Id, ahora, ct);
        auditoria.Registrar(AccionAuditoria.ContrasenaRestablecida, "cuenta", cuenta.Id, cuenta.Id);
        await unidad.GuardarAsync(ct);
        return Resultado.Exito();
    }
}

public sealed record CambiarContrasena(string? Actual, string Nueva);

internal sealed class CambiarContrasenaValidador : AbstractValidator<CambiarContrasena>
{
    public CambiarContrasenaValidador()
    {
        RuleFor(c => c.Nueva).NotEmpty().MinimumLength(ReglasContrasena.LongitudMinima).MaximumLength(128);
        RuleFor(c => c.Actual).MaximumLength(128);
    }
}

/// <summary>Cambia la contraseña, o la define si la cuenta entró solo con Google (SPEC-01, Casos borde).</summary>
public sealed class CambiarContrasenaCasoUso(
    IRepositorioCuentas cuentas,
    IHasherContrasenas hasher,
    IListaContrasenasComunes comunes,
    IContextoUsuario contexto,
    IUnidadDeTrabajo unidad,
    TimeProvider reloj)
{
    public async Task<Resultado> EjecutarAsync(CambiarContrasena comando, CancellationToken ct)
    {
        var docenteId = contexto.DocenteId ?? throw new InvalidOperationException("Requiere un docente autenticado.");
        var cuenta = await cuentas.PorIdAsync(docenteId, ct) ?? throw new InvalidOperationException("Cuenta inexistente.");
        if (cuenta.HashContrasena is { } hash
            && (comando.Actual is null || !hasher.Verificar(hash, comando.Actual)))
        {
            return Resultado.Fallo(ErroresCuentas.CredencialesInvalidas);
        }

        if (comunes.Contiene(comando.Nueva))
        {
            return Resultado.Fallo(ErroresCuentas.ContrasenaComun);
        }

        cuenta.CambiarContrasena(hasher.Hash(comando.Nueva), reloj.GetUtcNow());
        await unidad.GuardarAsync(ct);
        return Resultado.Exito();
    }
}
