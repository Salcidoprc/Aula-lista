using System.Security.Cryptography;
using System.Text;

namespace AulaLista.Application.Comun;

/// <summary>Tokens aleatorios de 256 bits; en la base solo se guarda su hash SHA-256 (reglas/04).</summary>
public static class TokensSeguros
{
    public static (string Token, string Hash) Generar()
    {
        var token = Base64UrlSinRelleno(RandomNumberGenerator.GetBytes(32));
        return (token, Hash(token));
    }

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlSinRelleno(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
