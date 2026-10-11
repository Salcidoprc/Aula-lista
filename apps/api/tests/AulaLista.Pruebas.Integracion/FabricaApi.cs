using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using AulaLista.Application.Comun;
using AulaLista.Infrastructure;
using AulaLista.Infrastructure.Comun;
using AulaLista.Infrastructure.Trabajos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;

namespace AulaLista.Pruebas.Integracion;

public sealed partial class FabricaApi(BaseDeDatosFixture baseDeDatos, int intentosInicioSesionPorMinuto = 1000)
    : WebApplicationFactory<Program>
{
    public const string ContrasenaValida = "lapiz-azul-del-salon-4B";
    private const string Emisor = "aula-lista";
    private const string ClaveFirma = "clave-de-pruebas-de-al-menos-32-caracteres";

    private readonly ConditionalWeakTable<HttpClient, CookieContainer> _cookies = [];
    private readonly string _almacen = Path.Combine(Path.GetTempPath(), "aula-lista-pruebas", Guid.NewGuid().ToString("N"));

    /// <summary>Reloj controlable; arranca en la hora real para que los JWT sean válidos.</summary>
    public FakeTimeProvider Reloj { get; } = new(DateTimeOffset.UtcNow);

    public BuzonCorreo Buzon => Services.GetRequiredService<BuzonCorreo>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Pruebas");
        builder.UseSetting($"ConnectionStrings:{DependencyInjection.CadenaConexion}", baseDeDatos.ConexionRuntime);
        builder.UseSetting("Jwt:Emisor", Emisor);
        builder.UseSetting("Jwt:Audiencia", Emisor);
        builder.UseSetting("Jwt:ClaveFirma", ClaveFirma);
        builder.UseSetting("Cuentas:UrlWeb", "https://aula-lista.test");
        builder.UseSetting("Cuentas:IntentosInicioSesionPorMinuto", intentosInicioSesionPorMinuto.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Correo:Proveedor", OpcionesCorreo.ProveedorBuzon);
        builder.UseSetting("Almacen:Ruta", _almacen);
        builder.UseSetting("Trabajos:Servidor", "false");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Reloj);
            services.RemoveAll<IColaTrabajos>();
            services.RemoveAll<IDespachoTrabajos>();
            services.AddScoped<ColaInmediata>();
            services.AddScoped<IColaTrabajos>(sp => sp.GetRequiredService<ColaInmediata>());
            services.AddScoped<IDespachoTrabajos>(sp => sp.GetRequiredService<ColaInmediata>());
        });
    }

    /// <summary>Cliente HTTPS con cookies, como el navegador (la cookie de sesión es <c>Secure</c>).</summary>
    public HttpClient Cliente()
    {
        var cookies = new CookieContainer();
        var cliente = CreateDefaultClient(new Uri("https://localhost"), new CookieContainerHandler(cookies));
        _cookies.Add(cliente, cookies);
        return cliente;
    }

    /// <summary>Valor actual de la cookie de sesión de un cliente, como la copiaría un atacante.</summary>
    public string CookieSesion(HttpClient cliente) =>
        _cookies.TryGetValue(cliente, out var cookies)
            ? cookies.GetCookies(new Uri("https://localhost/api/v1/cuentas/sesion"))["aula_lista_sesion"]?.Value
                ?? throw new InvalidOperationException("El cliente no tiene cookie de sesión.")
            : throw new InvalidOperationException("El cliente no se creó con la fábrica.");

    /// <summary>Registra un docente y devuelve su cliente ya autenticado.</summary>
    public async Task<(HttpClient Cliente, Guid DocenteId, string Correo)> RegistrarDocenteAsync(string? correo = null)
    {
        correo ??= $"docente-{Guid.NewGuid():N}@prueba.mx";
        var cliente = Cliente();
        var respuesta = await cliente.PostAsJsonAsync(
            "/api/v1/cuentas/registro",
            new { correo, contrasena = ContrasenaValida, aceptaTerminos = true, aceptaPrivacidad = true });
        respuesta.EnsureSuccessStatusCode();
        var sesion = await respuesta.Content.ReadFromJsonAsync<SesionDto>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sesion!.TokenAcceso);
        return (cliente, sesion.DocenteId, correo);
    }

    /// <summary>Saca el token del último correo enviado a una dirección con el asunto indicado.</summary>
    public string TokenDelCorreo(string para, string asunto)
    {
        var correo = Buzon.Correos.Last(c => c.Para == para && c.Asunto.Contains(asunto, StringComparison.Ordinal));
        return TokenEnEnlace().Match(correo.Texto).Groups[1].Value;
    }

    /// <summary>Emite un token de acceso de prueba para un docente.</summary>
    public static string TokenDocente(Guid docenteId)
    {
        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ClaveFirma)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: Emisor,
            audience: Emisor,
            claims: [new Claim("sub", docenteId.ToString()), new Claim("docente_id", docenteId.ToString())],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credenciales);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_almacen))
        {
            Directory.Delete(_almacen, recursive: true);
        }
    }

    [GeneratedRegex(@"token=([A-Za-z0-9_\-%]+)")]
    private static partial Regex TokenEnEnlace();

    /// <summary>Corre los trabajos al confirmarse la solicitud, con el mismo ejecutor que usa Hangfire.</summary>
    private sealed class ColaInmediata(EjecutorTrabajos ejecutor) : IColaTrabajos, IDespachoTrabajos
    {
        private readonly List<(Guid Docente, Guid Exportacion)> _pendientes = [];

        public void EncolarExportacion(Guid docenteId, Guid exportacionId) => _pendientes.Add((docenteId, exportacionId));

        public void Despachar()
        {
            foreach (var (docente, exportacion) in _pendientes)
            {
                ejecutor.GenerarExportacionAsync(docente, exportacion, CancellationToken.None).GetAwaiter().GetResult();
            }

            _pendientes.Clear();
        }
    }
}

public sealed record SesionDto(Guid DocenteId, string TokenAcceso, DateTimeOffset ExpiraEn);
