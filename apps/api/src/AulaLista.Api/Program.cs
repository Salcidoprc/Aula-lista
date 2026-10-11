using System.Text;
using AulaLista.Api.Comun;
using AulaLista.Api.Cuentas;
using AulaLista.Api.Operacion;
using AulaLista.Application;
using AulaLista.Application.Comun;
using AulaLista.Infrastructure;
using AulaLista.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
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

var opcionesJwt = builder.Services.AddOptions<OpcionesJwt>()
    .Bind(builder.Configuration.GetSection(OpcionesJwt.Seccion))
    .ValidateDataAnnotations();

// Al compilar, la herramienta de OpenAPI arranca la app sin secretos; ahí no se validan.
var esGeneracionOpenApi = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
if (!esGeneracionOpenApi)
{
    opcionesJwt.ValidateOnStart();
}

builder.Services
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
    });

// Toda ruta requiere autenticación salvo las marcadas con AllowAnonymous.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IContextoUsuario, ContextoUsuarioHttp>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetailsConCodigo();
builder.Services.AddOpenApi("v1");

builder.Services.AddAplicacion();
builder.Services.AddInfraestructura(builder.Configuration);

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TransaccionPorSolicitud>();

app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
app.MapSalud();

var api = app.MapGroup("/api/v1");
api.MapHola();

await app.RunAsync();

/// <summary>Punto de entrada expuesto para las pruebas de integración.</summary>
public partial class Program;
