namespace AulaLista.Application.Comun;

/// <summary>Cuenta intentos por clave en una ventana de tiempo (RNF-004).</summary>
public interface ILimitadorIntentos
{
    /// <summary>Registra un intento y dice si cabe dentro del límite.</summary>
    bool Intentar(string clave, int limite, TimeSpan ventana);
}
