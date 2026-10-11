using System.Collections.Concurrent;
using System.Reflection;
using AulaLista.Application.Comun;
using AulaLista.Application.Cuentas;

namespace AulaLista.Infrastructure.Cuentas;

/// <summary>Contraseñas filtradas de 10 caracteres o más (SecLists, más algunas comunes en México).</summary>
internal sealed class ListaContrasenasComunes : IListaContrasenasComunes
{
    private static readonly Lazy<HashSet<string>> Lista = new(Cargar);

    public bool Contiene(string contrasena) => Lista.Value.Contains(contrasena.Trim().ToLowerInvariant());

    private static HashSet<string> Cargar()
    {
        using var flujo = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("AulaLista.Infrastructure.Cuentas.contrasenas-comunes.txt")
            ?? throw new InvalidOperationException("Falta la lista de contraseñas comunes.");
        using var lector = new StreamReader(flujo);
        var lista = new HashSet<string>(StringComparer.Ordinal);
        while (lector.ReadLine() is { } linea)
        {
            if (linea.Length > 0)
            {
                lista.Add(linea);
            }
        }

        return lista;
    }
}

/// <summary>Ventana deslizante en memoria. Basta mientras la API corre en un solo servidor (ADR-0003).</summary>
internal sealed class LimitadorIntentosEnMemoria(TimeProvider reloj) : ILimitadorIntentos
{
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _intentos = new(StringComparer.Ordinal);

    public bool Intentar(string clave, int limite, TimeSpan ventana)
    {
        var ahora = reloj.GetUtcNow();
        var cola = _intentos.GetOrAdd(clave, _ => new Queue<DateTimeOffset>());
        lock (cola)
        {
            while (cola.Count > 0 && ahora - cola.Peek() >= ventana)
            {
                cola.Dequeue();
            }

            cola.Enqueue(ahora);
            return cola.Count <= limite;
        }
    }
}
