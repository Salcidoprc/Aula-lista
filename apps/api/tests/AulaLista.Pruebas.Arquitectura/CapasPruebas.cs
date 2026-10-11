using System.Reflection;
using NetArchTest.Rules;

namespace AulaLista.Pruebas.Arquitectura;

/// <summary>Dependencias permitidas entre capas (reglas/01-backend-dotnet.md).</summary>
public sealed class CapasPruebas
{
    private static readonly Assembly Domain = typeof(AulaLista.Domain.Comun.Entidad).Assembly;
    private static readonly Assembly Application = typeof(AulaLista.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(AulaLista.Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Domain_NoDependeDeNingunaCapa() =>
        AssertSinDependencias(Domain, "AulaLista.Application", "AulaLista.Infrastructure", "AulaLista.Api",
            "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore");

    [Fact]
    public void Application_SoloDependeDeDomain() =>
        AssertSinDependencias(Application, "AulaLista.Infrastructure", "AulaLista.Api",
            "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore");

    [Fact]
    public void Infrastructure_NoDependeDeApi() =>
        AssertSinDependencias(Infrastructure, "AulaLista.Api", "Microsoft.AspNetCore");

    [Fact]
    public void Domain_NoUsaDoubleNiFloatEnPropiedades()
    {
        var invalidas = Domain.GetTypes()
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            .Where(p => Nullable.GetUnderlyingType(p.PropertyType) is { } t2
                ? t2 == typeof(double) || t2 == typeof(float)
                : p.PropertyType == typeof(double) || p.PropertyType == typeof(float))
            .Select(p => $"{p.DeclaringType?.Name}.{p.Name}")
            .ToList();

        Assert.Empty(invalidas);
    }

    private static void AssertSinDependencias(Assembly ensamblado, params string[] prohibidas)
    {
        var resultado = Types.InAssembly(ensamblado)
            .ShouldNot()
            .HaveDependencyOnAny(prohibidas)
            .GetResult();

        Assert.True(
            resultado.IsSuccessful,
            "Dependencias prohibidas en: " + string.Join(", ", resultado.FailingTypeNames ?? []));
    }
}
