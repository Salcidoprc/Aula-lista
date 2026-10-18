using System.Net.Http.Json;
using System.Text.Json;

namespace AulaLista.Pruebas.Integracion;

/// <summary>Pone grupos en Pro como lo haría el docente: Checkout con tarjeta y la página de pago simulado.</summary>
internal static class PlanesDePrueba
{
    public static async Task ActivarProAsync(this HttpClient cliente, params Guid[] grupos)
    {
        var suscripcion = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/docente/suscripcion");
        if (suscripcion.GetProperty("modalidad").GetString() == "tarjeta" && suscripcion.GetProperty("estado").GetString() == "activa")
        {
            var actuales = suscripcion.GetProperty("grupos").EnumerateArray()
                .Where(g => g.GetProperty("enSuscripcion").GetBoolean())
                .Select(g => g.GetProperty("grupoId").GetGuid());
            (await cliente.PutAsJsonAsync("/api/v1/docente/suscripcion/grupos", new { grupoIds = actuales.Union(grupos).ToArray() }))
                .EnsureSuccessStatusCode();
            return;
        }

        var sesion = await PagoSimuladoAsync(cliente, new { grupoIds = grupos, modalidad = "tarjeta" });
        (await cliente.PostAsync($"/api/v1/pagos-simulados/{sesion}/pagar", null)).EnsureSuccessStatusCode();
    }

    /// <summary>Abre el Checkout y regresa el id de la sesión simulada.</summary>
    public static async Task<string> PagoSimuladoAsync(HttpClient cliente, object checkout)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/docente/suscripcion/checkout", checkout);
        Assert.True(respuesta.IsSuccessStatusCode, await respuesta.Content.ReadAsStringAsync());
        var url = (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!;
        return url[(url.LastIndexOf('/') + 1)..];
    }
}
