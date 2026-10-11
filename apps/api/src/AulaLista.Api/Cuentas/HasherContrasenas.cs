using AulaLista.Application.Cuentas;
using Microsoft.AspNetCore.Identity;

namespace AulaLista.Api.Cuentas;

/// <summary>Hasher de ASP.NET Core Identity (PBKDF2), como pide reglas/04.</summary>
internal sealed class HasherContrasenas : IHasherContrasenas
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object Usuario = new();

    public string Hash(string contrasena) => Hasher.HashPassword(Usuario, contrasena);

    public bool Verificar(string hash, string contrasena) =>
        Hasher.VerifyHashedPassword(Usuario, hash, contrasena) != PasswordVerificationResult.Failed;
}
