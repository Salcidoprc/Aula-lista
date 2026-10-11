using AulaLista.Domain.Grupos;

namespace AulaLista.Pruebas.Unit.Comun;

public sealed class CicloEscolarPruebas
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Crear_GeneraIdVersion7YMarcasEnUtc()
    {
        var ciclo = new CicloEscolar("2026-2027", new DateOnly(2026, 8, 31), new DateOnly(2027, 7, 15), Ahora);

        Assert.Equal(7, ciclo.Id.Version);
        Assert.Equal(Ahora, ciclo.CreatedAt);
    }

    [Fact]
    public void Crear_ConFinAntesDelInicio_Falla() =>
        Assert.Throws<ArgumentException>(() =>
            new CicloEscolar("2026-2027", new DateOnly(2027, 7, 15), new DateOnly(2026, 8, 31), Ahora));
}
