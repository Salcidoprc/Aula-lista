using AulaLista.Domain.Comun;

namespace AulaLista.Domain.Grupos;

/// <summary>Catálogo global de ciclos escolares (ej. 2026-2027). No pertenece a ningún docente.</summary>
public sealed class CicloEscolar : Entidad
{
    public CicloEscolar(string nombre, DateOnly inicio, DateOnly fin, DateTimeOffset ahora)
        : base(ahora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        if (fin <= inicio)
        {
            throw new ArgumentException("El fin del ciclo debe ser posterior a su inicio.", nameof(fin));
        }

        Nombre = nombre;
        Inicio = inicio;
        Fin = fin;
    }

    private CicloEscolar()
    {
    }

    public string Nombre { get; private set; } = string.Empty;

    public DateOnly Inicio { get; private set; }

    public DateOnly Fin { get; private set; }
}
