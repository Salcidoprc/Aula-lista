using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AulaLista.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;

namespace AulaLista.Pruebas.Integracion;

public sealed class FabricaApi(BaseDeDatosFixture baseDeDatos) : WebApplicationFactory<Program>
{
    private const string Emisor = "aula-lista";
    private const string ClaveFirma = "clave-de-pruebas-de-al-menos-32-caracteres";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Pruebas");
        builder.UseSetting($"ConnectionStrings:{DependencyInjection.CadenaConexion}", baseDeDatos.ConexionRuntime);
        builder.UseSetting("Jwt:Emisor", Emisor);
        builder.UseSetting("Jwt:Audiencia", Emisor);
        builder.UseSetting("Jwt:ClaveFirma", ClaveFirma);
    }

    /// <summary>Emite un token de acceso de prueba para un docente.</summary>
    public static string TokenDocente(Guid docenteId)
    {
        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ClaveFirma)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: Emisor,
            audience: Emisor,
            claims: [new Claim("sub", docenteId.ToString()), new Claim("docente_id", docenteId.ToString())],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credenciales);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
