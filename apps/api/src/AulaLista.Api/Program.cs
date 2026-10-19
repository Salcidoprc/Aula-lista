using System.Text;
using System.Threading.RateLimiting;
using AulaLista.Api.Avisos;
using AulaLista.Api.Boletas;
using AulaLista.Api.Cierre;
using AulaLista.Api.Comun;
using AulaLista.Api.Cuentas;
using AulaLista.Api.Evaluacion;
using AulaLista.Api.Grupos;
using AulaLista.Api.Operacion;
using AulaLista.Api.Planes;
using AulaLista.Api.Tutores;
using AulaLista.Application;
using AulaLista.Application.Comun;
using AulaLista.Application.Cuentas;
using AulaLista.Application.Tutores;
using AulaLista.Domain.Cuentas;
using AulaLista.Infrastructure;
using AulaLista.Infrastructure.Persistencia;
using AulaLista.Infrastructure.Trabajos;
using Hangfire;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// `--migrar` aplica las migraciones con el rol app_migraciones y termina (paso previo al despliegue).
if (args.Contains("--migrar"))
{
    await Migrador.MigrarAsync(builder.Configuration);
    return;
}

builder.Services.AddSerilog((services, configuracion) => configuracion
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// SPEC-09: trazas y métricas con OpenTelemetry, solo si hay a dónde mandarlas (OTEL_EXPORTER_OTLP_ENDPOINT). Las
// rutas llevan IDs, nunca nombres; la cadena de consulta se redacta y SQL viaja sin valores.
if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService("aula-lista-api"))
        .WithTracing(t => t
            .AddAspNetCoreInstrumentation(o => o.Filter = http => !http.Request.Path.StartsWithSegments("/salud", StringComparison.Ordinal))
            .AddHttpClientInstrumentation()
            .AddSource("Npgsql")
            .AddOtlpExporter())
        .WithMetrics(m => m
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter("Npgsql")
            .AddOtlpExporter());
}

var opcionesJwt = builder.Services.AddOptions<OpcionesJwt>()
    .Bind(builder.Configuration.GetSection(OpcionesJwt.Seccion))
    .ValidateDataAnnotations();

// Al compilar, la herramienta de OpenAPI arranca la app sin secretos; ahí no se validan.
var esGeneracionOpenApi = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
var opcionesCuentas = builder.Services.AddOptions<OpcionesCuentas>()
    .Bind(builder.Configuration.GetSection(OpcionesCuentas.Seccion))
    .ValidateDataAnnotations();
var opcionesTutores = builder.Services.AddOptions<OpcionesTutores>()
    .Bind(builder.Configuration.GetSection(OpcionesTutores.Seccion))
    .ValidateDataAnnotations();
if (!esGeneracionOpenApi)
{
    opcionesJwt.ValidateOnStart();
    opcionesCuentas.ValidateOnStart();
    opcionesTutores.ValidateOnStart();
}

var autenticacion = builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opciones =>
    {
        var jwt = builder.Configuration.GetSection(OpcionesJwt.Seccion).Get<OpcionesJwt>() ?? new OpcionesJwt();
        opciones.MapInboundClaims = false;
        opciones.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Emisor,
            ValidAudience = jwt.Audiencia,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.ClaveFirma)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    })
    .AddScheme<AuthenticationSchemeOptions, AutenticacionTutor>(AutenticacionTutor.Esquema, null);

// RF-002: Google solo se activa con credenciales (Google__ClientId y Google__ClientSecret por variable de entorno).
var google = builder.Configuration.GetSection("Google");
if (!string.IsNullOrWhiteSpace(google["ClientId"]) && !string.IsNullOrWhiteSpace(google["ClientSecret"]))
{
    autenticacion
        .AddCookie(GoogleEndpoints.EsquemaExterno, o =>
        {
            o.Cookie.Name = "aula_lista_externa";
            o.Cookie.SameSite = SameSiteMode.Lax;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        })
        .AddGoogle(GoogleEndpoints.EsquemaGoogle, o =>
        {
            o.ClientId = google["ClientId"]!;
            o.ClientSecret = google["ClientSecret"]!;
            o.SignInScheme = GoogleEndpoints.EsquemaExterno;
            o.CallbackPath = GoogleEndpoints.RutaRetornoGoogle;
            o.ClaimActions.MapJsonKey("email_verified", "email_verified");
        });
}

// Toda ruta requiere autenticación salvo las marcadas con AllowAnonymous.
// SPEC-06: el portal solo acepta la cookie del tutor; el PDF de una boleta acepta al docente o al tutor.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build())
    .AddPolicy(AutenticacionTutor.Politica, p => p
        .AddAuthenticationSchemes(AutenticacionTutor.Esquema)
        .RequireClaim(ContextoUsuarioHttp.ClaimTutor))
    .AddPolicy(AutenticacionTutor.PoliticaDocenteOTutor, p => p
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, AutenticacionTutor.Esquema)
        .RequireAuthenticatedUser())
    .AddPolicy(PoliticaAdmin.Nombre, PoliticaAdmin.Crear());
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PoliticaAdmin.Manejador>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, PoliticaAdmin.Resultado>();

// RNF-004: 5 intentos de inicio de sesión por minuto por IP (el límite por cuenta vive en el caso de uso).
builder.Services.AddRateLimiter(o =>
{
    o.AddPolicy(CuentasEndpoints.PoliticaInicioSesion, http =>
    {
        var limite = builder.Configuration.GetValue($"{OpcionesCuentas.Seccion}:{nameof(OpcionesCuentas.IntentosInicioSesionPorMinuto)}", 5);
        return RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limite, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
    });
    o.AddPolicy(TutoresEndpoints.PoliticaCanje, http =>
    {
        var limite = builder.Configuration.GetValue($"{OpcionesTutores.Seccion}:{nameof(OpcionesTutores.CanjesPorMinuto)}", 10);
        return RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limite, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
    });
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await ErroresCuentas.DemasiadosIntentos.AProblema(StatusCodes.Status429TooManyRequests).ExecuteAsync(ctx.HttpContext);
    };
});

// Detrás de nginx: la IP real llega en X-Forwarded-For desde el proxy inmediato.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ContextoUsuarioHttp>();
builder.Services.AddScoped<IContextoUsuario>(sp => sp.GetRequiredService<ContextoUsuarioHttp>());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IHasherContrasenas, HasherContrasenas>();
builder.Services.AddSingleton<IEmisorTokensAcceso, EmisorTokensAccesoJwt>();
builder.Services.AddProblemDetailsConCodigo();
builder.Services.AddOpenApi("v1");

builder.Services.AddAplicacion();
builder.Services.AddInfraestructura(builder.Configuration);

var app = builder.Build();

// `--admin <correo>` da el rol admin a una cuenta verificada (SPEC-09: nunca desde la interfaz) y termina.
if (Array.IndexOf(args, "--admin") is var posicion and >= 0)
{
    var correo = posicion + 1 < args.Length ? args[posicion + 1] : string.Empty;
    await using var alcance = app.Services.CreateAsyncScope();
    var hecho = await alcance.ServiceProvider.GetRequiredService<AulaLista.Application.Operacion.HacerAdminCasoUso>()
        .EjecutarAsync(correo, CancellationToken.None);
    Console.WriteLine(hecho ? "Listo: la cuenta ya es administradora." : "No hay una cuenta activa y verificada con ese correo.");
    Environment.ExitCode = hecho ? 0 : 1;
    return;
}

app.UseForwardedHeaders();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<TransaccionPorSolicitud>();

app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
app.MapSalud();

var api = app.MapGroup("/api/v1");
api.MapCuentas();
api.MapDocente();
api.MapGrupos();
api.MapEvaluacion();
api.MapCierre();
api.MapBoletas();
api.MapTutores();
api.MapAvisos();
api.MapPlanes(app.Configuration);
api.MapAdmin();
if (app.Services.GetService<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>() is { } esquemas
    && await esquemas.GetSchemeAsync(GoogleEndpoints.EsquemaGoogle) is not null)
{
    api.MapGoogle();
}
else
{
    api.MapGoogleNoDisponible();
}

api.MapDesarrollo(app.Environment, app.Services);

if (app.Configuration.GetValue("Trabajos:Servidor", true)
    && !string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString(AulaLista.Infrastructure.DependencyInjection.CadenaConexion)))
{
    // RNF-009: limpieza diaria por retención, 03:00 hora del centro de México.
    app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<EjecutorTrabajos>(
        "retencion-cuentas",
        e => e.RetencionAsync(CancellationToken.None),
        "0 3 * * *",
        new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById(Docente.ZonaHorariaPorDefecto) });

    // RN-085, RN-089: vencimientos, gracia y avisos de pago, 06:00 hora del centro de México.
    app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<EjecutorTrabajos>(
        "revisar-suscripciones",
        e => e.RevisarSuscripcionesAsync(CancellationToken.None),
        "0 6 * * *",
        new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById(Docente.ZonaHorariaPorDefecto) });
}

await app.RunAsync();

/// <summary>Punto de entrada expuesto para las pruebas de integración.</summary>
public partial class Program;
