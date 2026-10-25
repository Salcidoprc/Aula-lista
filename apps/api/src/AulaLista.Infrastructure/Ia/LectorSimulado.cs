using AulaLista.Application.Grupos;

namespace AulaLista.Infrastructure.Ia;

/// <summary>
/// Lector sin red ni costo para local, pruebas y demostraciones (<c>Ia:Proveedor=simulado</c>): devuelve siempre la
/// misma lista ficticia, como si la foto se hubiera leído bien.
/// </summary>
internal sealed class LectorSimulado : ILectorListas
{
    private static readonly string[] Lista =
    [
        "Acosta Beltrán Ana Sofía",
        "Bojórquez Félix Diego",
        "Castro Lizárraga Valeria",
        "Domínguez Peña Mateo",
        "Escobar Ruiz Ximena",
        "Félix Osuna Santiago",
        "García Inzunza Regina",
        "Ibarra López Emiliano",
        "Leyva Sánchez Camila",
        "Medina Rojo Sebastián",
        "Núñez Vega Renata",
        "Ochoa Zazueta Leonardo",
    ];

    public async Task<ListaLeida> LeerAsync(ImagenLista imagen, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken);
        return new ListaLeida(Lista, null, RedactorSimulado.Modelo, PromptLista.Version, 1600, 180, 400);
    }
}
