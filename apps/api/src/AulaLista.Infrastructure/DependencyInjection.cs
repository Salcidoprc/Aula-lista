using Anthropic;
using AulaLista.Application.Boletas;
using AulaLista.Application.Cierre;
using AulaLista.Application.Comun;
using AulaLista.Application.Cuentas;
using AulaLista.Application.Evaluacion;
using AulaLista.Application.Grupos;
using AulaLista.Infrastructure.Boletas;
using AulaLista.Infrastructure.Cierre;
using AulaLista.Infrastructure.Comun;
using AulaLista.Infrastructure.Cuentas;
using AulaLista.Infrastructure.Evaluacion;
using AulaLista.Infrastructure.Grupos;
using AulaLista.Infrastructure.Ia;
using AulaLista.Infrastructure.Persistencia;
using AulaLista.Infrastructure.Trabajos;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AulaLista.Infrastructure;

public static class DependencyInjection
{
    public const string CadenaConexion = "AulaLista";
    public const string EsquemaTrabajos = "hangfire";

    public static IServiceCollection AddInfraestructura(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<VariablesRlsInterceptor>();
        services.AddDbContext<AulaListaDbContext>((sp, options) => options
            .UseNpgsql(configuration.GetConnectionString(CadenaConexion))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<VariablesRlsInterceptor>()));
        services.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();
        services.AddScoped<IAuditoria, Auditoria>();

        services.AddScoped<IRepositorioCuentas, RepositorioCuentas>();
        services.AddScoped<IRepositorioDocentes, RepositorioDocentes>();
        services.AddScoped<IRetencionCuentas, RetencionCuentas>();
        services.AddScoped<IGeneradorExportacion, GeneradorExportacion>();
        services.AddSingleton<IListaContrasenasComunes, ListaContrasenasComunes>();
        services.AddSingleton<ILimitadorIntentos, LimitadorIntentosEnMemoria>();

        services.AddScoped<IRepositorioGrupos, RepositorioGrupos>();
        services.AddScoped<RepositorioEvaluacion>();
        services.AddScoped<IRepositorioEvaluacion>(sp => sp.GetRequiredService<RepositorioEvaluacion>());
        services.AddScoped<IDatosDeEvaluacion>(sp => sp.GetRequiredService<RepositorioEvaluacion>());
        services.AddScoped<ITutores, Tutores>();
        services.AddSingleton<IHojasDeCalculo, HojasDeCalculo>();

        services.AddOptions<OpcionesAlmacen>().Bind(configuration.GetSection(OpcionesAlmacen.Seccion)).ValidateDataAnnotations();
        services.AddSingleton<IAlmacenArchivos, AlmacenArchivosLocal>();

        services.AddScoped<IRepositorioCierre, RepositorioCierre>();
        services.AddScoped<IRepositorioBoletas, RepositorioBoletas>();
        services.AddSingleton<IGeneradorBoletas, GeneradorBoletas>();
        AgregarIa(services, configuration);

        AgregarCorreo(services, configuration);
        AgregarTrabajos(services, configuration);

        services.AddHealthChecks().AddDbContextCheck<AulaListaDbContext>("base_de_datos", tags: ["lista"]);
        return services;
    }

    private static void AgregarCorreo(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpcionesCorreo>()
            .Bind(configuration.GetSection(OpcionesCorreo.Seccion))
            .ValidateDataAnnotations()
            .Validate(
                o => o.Proveedor != OpcionesCorreo.ProveedorBrevo || !string.IsNullOrWhiteSpace(o.BrevoApiKey),
                "Falta Correo__BrevoApiKey.");

        var proveedor = configuration.GetSection(OpcionesCorreo.Seccion)[nameof(OpcionesCorreo.Proveedor)]
            ?? OpcionesCorreo.ProveedorBuzon;
        if (proveedor == OpcionesCorreo.ProveedorBrevo)
        {
            services.AddHttpClient<IEnvioCorreo, EnvioCorreoBrevo>(http =>
            {
                http.BaseAddress = new Uri("https://api.brevo.com/");
                http.Timeout = TimeSpan.FromSeconds(15);
            });
        }
        else
        {
            services.AddSingleton<BuzonCorreo>();
            services.AddSingleton<IEnvioCorreo>(sp => sp.GetRequiredService<BuzonCorreo>());
        }
    }

    /// <summary>reglas/05-ia: modelo y límites de configuración; la clave solo por variable de entorno.</summary>
    private static void AgregarIa(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpcionesIa>()
            .Bind(configuration.GetSection(OpcionesIa.Seccion))
            .ValidateDataAnnotations()
            .Validate(
                o => o.Proveedor != OpcionesIa.ProveedorClaude || !string.IsNullOrWhiteSpace(o.ApiKey),
                "Falta Ia__ApiKey.")
            .ValidateOnStart();
        services.AddSingleton<LimitadorIa>();

        var proveedor = configuration.GetSection(OpcionesIa.Seccion)[nameof(OpcionesIa.Proveedor)] ?? OpcionesIa.ProveedorSimulado;
        if (proveedor == OpcionesIa.ProveedorClaude)
        {
            services.AddSingleton(sp =>
            {
                var opciones = sp.GetRequiredService<IOptions<OpcionesIa>>().Value;
                return new AnthropicClient { ApiKey = opciones.ApiKey!, MaxRetries = opciones.Reintentos };
            });
            services.AddSingleton<IRedactorObservaciones, RedactorClaude>();
        }
        else
        {
            services.AddSingleton<IRedactorObservaciones, RedactorSimulado>();
        }
    }

    private static void AgregarTrabajos(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<EjecutorTrabajos>();
        services.AddScoped<ColaTrabajosHangfire>();
        services.AddScoped<IColaTrabajos>(sp => sp.GetRequiredService<ColaTrabajosHangfire>());
        services.AddScoped<IDespachoTrabajos>(sp => sp.GetRequiredService<ColaTrabajosHangfire>());
        services.AddHangfire(c => c
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                o => o.UseNpgsqlConnection(configuration.GetConnectionString(CadenaConexion)),
                new PostgreSqlStorageOptions { SchemaName = EsquemaTrabajos, PrepareSchemaIfNecessary = false }));

        // Sin cadena de conexión (por ejemplo al generar el OpenAPI en el build) no se arranca el servidor.
        if (configuration.GetValue("Trabajos:Servidor", true)
            && !string.IsNullOrWhiteSpace(configuration.GetConnectionString(CadenaConexion)))
        {
            services.AddHangfireServer(o => o.WorkerCount = 2);
        }
    }
}
