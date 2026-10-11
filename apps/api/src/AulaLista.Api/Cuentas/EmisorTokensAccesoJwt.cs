using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AulaLista.Api.Comun;
using AulaLista.Application.Cuentas;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AulaLista.Api.Cuentas;

/// <summary>Token de acceso JWT de 15 minutos (SPEC-01, Sesión). Vive solo en memoria del navegador.</summary>
internal sealed class EmisorTokensAccesoJwt(IOptions<OpcionesJwt> jwt, IOptions<OpcionesCuentas> cuentas, TimeProvider reloj)
    : IEmisorTokensAcceso
{
    private static readonly JwtSecurityTokenHandler Manejador = new();

    public TokenAcceso Emitir(Guid docenteId, Guid familiaSesionId)
    {
        var ahora = reloj.GetUtcNow();
        var expira = ahora.AddMinutes(cuentas.Value.MinutosTokenAcceso);
        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.ClaveFirma)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: jwt.Value.Emisor,
            audience: jwt.Value.Audiencia,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, docenteId.ToString()),
                new Claim(ContextoUsuarioHttp.ClaimDocente, docenteId.ToString()),
                new Claim(ContextoUsuarioHttp.ClaimFamiliaSesion, familiaSesionId.ToString()),
            ],
            notBefore: ahora.UtcDateTime,
            expires: expira.UtcDateTime,
            signingCredentials: credenciales);
        return new TokenAcceso(Manejador.WriteToken(token), expira);
    }
}
