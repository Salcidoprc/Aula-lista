using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AulaLista.Domain.Cierre;
using Npgsql;

namespace AulaLista.Pruebas.Integracion;

/// <summary>Criterios de aceptación de SPEC-02 para la importación desde una foto de la lista (RF-015).</summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class Spec02FotoPruebas(BaseDeDatosFixture baseDeDatos) : IDisposable
{
    /// <summary>La firma de un PNG y unos bytes: basta para el caso de uso, que solo mira los primeros bytes.</summary>
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 0x4A, 0x46, 0x49, 0x46];
    private readonly FabricaApi _fabrica = new(baseDeDatos);

    public void Dispose() => _fabrica.Dispose();

    [Fact]
    public async Task RF015_FotoDeLaLista_LlegaALaVistaPreviaDeTextoYSoloQuedaLaLlamada()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var grupo = await CrearGrupoAsync(cliente);

        var respuesta = await SubirFotoAsync(cliente, grupo, Png, "image/png", "lista.png");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var vista = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("foto", vista.GetProperty("origen").GetString());
        Assert.Equal(["nombre_completo"], vista.GetProperty("mapeo").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal(
            ["Acosta Beltrán Ana Sofía", "Bojórquez Félix Diego", "Castro Lizárraga Valeria"],
            vista.GetProperty("filas").EnumerateArray().Select(f => f[0].GetString()));
        Assert.Equal(Png, Assert.Single(_fabrica.Lector.Recibidas).Datos.ToArray());

        // La foto no se guarda: solo los metadatos de la importación y la llamada, que cuenta en el costo del grupo.
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM importacion WHERE grupo_id = '{grupo}' AND origen = 'foto'"));
        Assert.Equal(1, await ContarAsync(
            $"SELECT count(*) FROM llamada_ia WHERE grupo_id = '{grupo}' AND version_prompt = 'lista-v1' AND resultado = 'ok' AND tokens_entrada = 1600"));
        Assert.Equal(1, await ContarAsync($"SELECT coalesce(sum(llamadas_ia), 0) FROM consumo_mensual WHERE grupo_id = '{grupo}'"));
        Assert.Equal(0, await ContarAsync(
            $"SELECT count(*) FROM information_schema.columns WHERE table_name = 'importacion' AND data_type = 'bytea'"));

        var confirmar = await cliente.PostAsJsonAsync(
            $"/api/v1/importaciones/{vista.GetProperty("id").GetGuid()}/confirmar",
            new { filas = new[] { new { nombres = "Ana Sofía", apellidos = "Acosta Beltrán" }, new { nombres = "Diego", apellidos = "Bojórquez Félix" } } });
        Assert.Equal(2, (await confirmar.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("guardados").GetInt32());

        // reglas/04-seguridad: los nombres que leyó la IA no llegan a los logs.
        Assert.DoesNotContain(_fabrica.Registros.Lineas, l => l.Contains("Bojórquez", StringComparison.Ordinal) || l.Contains("Bojo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RF015_ArchivoQueNoEsJpgNiPngOPasaDe5MB_SeRechazaSinLlamarALaIa()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var grupo = await CrearGrupoAsync(cliente);
        var grande = new byte[(5 * 1024 * 1024) + 1];
        Jpeg.CopyTo(grande, 0);

        var pdf = await SubirFotoAsync(cliente, grupo, "%PDF-1.7"u8.ToArray(), "image/png", "lista.png");
        var disfrazado = await SubirFotoAsync(cliente, grupo, Jpeg, "image/png", "lista.png");
        var pesado = await SubirFotoAsync(cliente, grupo, grande, "image/jpeg", "lista.jpg");

        Assert.Equal("foto_invalida", await CodigoAsync(pdf));
        Assert.Equal("foto_invalida", await CodigoAsync(disfrazado));
        Assert.Equal("foto_invalida", await CodigoAsync(pesado));
        Assert.Empty(_fabrica.Lector.Recibidas);
        Assert.Equal(0, await ContarAsync($"SELECT count(*) FROM llamada_ia WHERE grupo_id = '{grupo}'"));
    }

    [Fact]
    public async Task RF015_FotoQueLaIaNoLee_LlegaSinFilasYLaLlamadaCuenta()
    {
        var (cliente, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var grupo = await CrearGrupoAsync(cliente);
        _fabrica.Lector.Fallo = FalloGeneracion.Error;

        var fallida = await SubirFotoAsync(cliente, grupo, Jpeg, "image/jpeg", "lista.jpg");
        _fabrica.Lector.Fallo = null;
        _fabrica.Lector.Nombres = ["", " - ", "3."];
        var vacia = await SubirFotoAsync(cliente, grupo, Jpeg, "image/jpeg", "lista.jpg");

        // Sin nombres la vista previa llega vacía y la web ofrece pegar la lista; las dos lecturas costaron y cuentan.
        foreach (var respuesta in new[] { fallida, vacia })
        {
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            Assert.Empty((await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("filas").EnumerateArray());
        }

        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM llamada_ia WHERE grupo_id = '{grupo}' AND resultado = 'error'"));
        Assert.Equal(2, await ContarAsync($"SELECT coalesce(sum(llamadas_ia), 0) FROM consumo_mensual WHERE grupo_id = '{grupo}'"));
    }

    [Fact]
    public async Task RNF004_LecturasDeFoto_CuentanParaElLimitePorHora()
    {
        using var fabrica = new FabricaApi(baseDeDatos, solicitudesIaPorHora: 2);
        var (cliente, _, _) = await fabrica.RegistrarDocenteVerificadoAsync();
        var grupo = await CrearGrupoAsync(cliente);

        var primera = await SubirFotoAsync(cliente, grupo, Png, "image/png", "lista.png");
        var segunda = await SubirFotoAsync(cliente, grupo, Png, "image/png", "lista.png");
        var tercera = await SubirFotoAsync(cliente, grupo, Png, "image/png", "lista.png");

        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, tercera.StatusCode);
        Assert.Equal("limite_lecturas", await CodigoAsync(tercera));
        Assert.Equal(2, fabrica.Lector.Recibidas.Count);
    }

    [Fact]
    public async Task RNF001_FotoEnGrupoDeOtroDocente_Responde404SinLlamarALaIa()
    {
        var (dueno, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var grupo = await CrearGrupoAsync(dueno);
        var (intruso, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();

        var respuesta = await SubirFotoAsync(intruso, grupo, Png, "image/png", "lista.png");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Empty(_fabrica.Lector.Recibidas);
    }

    private static async Task<Guid> CrearGrupoAsync(HttpClient cliente)
    {
        var ciclo = await cliente.PostAsJsonAsync("/api/v1/ciclos", new { nombre = "2026-2027", inicio = "2026-08-25", fin = "2027-07-16", tipoUnidad = "trimestre" });
        ciclo.EnsureSuccessStatusCode();
        var cicloId = (await ciclo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var grupo = await cliente.PostAsJsonAsync("/api/v1/grupos", new { nombre = "A", nivel = "primaria", grado = 4, cicloId });
        grupo.EnsureSuccessStatusCode();
        return (await grupo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<HttpResponseMessage> SubirFotoAsync(HttpClient cliente, Guid grupo, byte[] foto, string tipo, string nombre)
    {
        var contenido = new ByteArrayContent(foto);
        contenido.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        using var formulario = new MultipartFormDataContent { { contenido, "foto", nombre } };
        return await cliente.PostAsync($"/api/v1/grupos/{grupo}/importaciones", formulario);
    }

    private async Task<long> ContarAsync(string sql)
    {
        await using var conexion = new NpgsqlConnection(baseDeDatos.ConexionAdmin);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        return Convert.ToInt64(await comando.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<string?> CodigoAsync(HttpResponseMessage respuesta)
    {
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.TryGetProperty("codigo", out var codigo) ? codigo.GetString() : null;
    }
}
