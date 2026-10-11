using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AulaLista.Api.Cuentas;

namespace AulaLista.Pruebas.Integracion;

[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class H0HolaAutenticadoPruebas(BaseDeDatosFixture baseDeDatos) : IDisposable
{
    private readonly FabricaApi _fabrica = new(baseDeDatos);

    public void Dispose() => _fabrica.Dispose();

    [Fact]
    public async Task H0_SinToken_Responde401ConCodigo()
    {
        using var cliente = _fabrica.CreateClient();

        var respuesta = await cliente.GetAsync(new Uri("/api/v1/hola", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal("no_autenticado", cuerpo.RootElement.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task H0_ConToken_SaludaAlDocente()
    {
        var docenteId = Guid.CreateVersion7();
        using var cliente = _fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", FabricaApi.TokenDocente(docenteId));

        var respuesta = await cliente.GetFromJsonAsync<RespuestaHola>(new Uri("/api/v1/hola", UriKind.Relative));

        Assert.NotNull(respuesta);
        Assert.Equal(docenteId, respuesta.DocenteId);
    }

    [Fact]
    public async Task H0_Salud_RespondeSinAutenticacion()
    {
        using var cliente = _fabrica.CreateClient();

        var vive = await cliente.GetAsync(new Uri("/salud", UriKind.Relative));
        var lista = await cliente.GetAsync(new Uri("/salud/lista", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, vive.StatusCode);
        Assert.Equal(HttpStatusCode.OK, lista.StatusCode);
    }

    [Fact]
    public async Task H0_OpenApi_SePublica()
    {
        using var cliente = _fabrica.CreateClient();

        var respuesta = await cliente.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }
}
