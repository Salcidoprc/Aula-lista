using Anthropic;
using Aulisima.Application.Avisos;
using Aulisima.Application.Boletas;
using Aulisima.Application.Cierre;
using Aulisima.Application.Comun;
using Aulisima.Application.Cuentas;
using Aulisima.Application.Evaluacion;
using Aulisima.Application.Grupos;
using Aulisima.Application.Operacion;
using Aulisima.Application.Planes;
using Aulisima.Application.Tutores;
using Aulisima.Infrastructure.Avisos;
using Aulisima.Infrastructure.Boletas;
using Aulisima.Infrastructure.Cierre;
using Aulisima.Infrastructure.Comun;
using Aulisima.Infrastructure.Cuentas;
using Aulisima.Infrastructure.Evaluacion;
using Aulisima.Infrastructure.Grupos;
using Aulisima.Infrastructure.Ia;
using Aulisima.Infrastructure.Persistencia;
using Aulisima.Infrastructure.Planes;
using Aulisima.Infrastructure.Trabajos;
using Aulisima.Infrastructure.Tutores;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aulisima.Infrastructure;

public static class DependencyInjection
{
    public const string CadenaConexion = "Aulisima";
    public const string EsquemaTrabajos = "hangfire";

    public static IServiceCollection AddInfraestructura(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<VariablesRlsInterceptor>();
        services.AddDbContext<AulisimaDbContext>((sp, options) => options
            .UseNpgsql(configuration.GetConnectionString(CadenaConexion))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<VariablesRlsInterceptor>()));
        services.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();
        services.AddScoped<IAuditoria, Auditoria>();
        services.AddScoped<IEventosProducto, Operacion.EventosProducto>();
        services.AddMetrics();
        services.AddSingleton<Operacion.MetricasAulisima>();
        services.AddSingleton<IRegistroErrores, Operacion.RegistroErrores>();
        services.AddScoped<IRepositorioAdmin, Operacion.RepositorioAdmin>();
        services.AddScoped<IRetencionGrupos, Operacion.RetencionGrupos>();
        services.AddOptions<OpcionesOperacion>().Bind(configuration.GetSection(OpcionesOperacion.Seccion)).ValidateDataAnnotations().ValidateOnStart();

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
        services.AddScoped<ITutores, Grupos.Tutores>();
        services.AddSingleton<IHojasDeCalculo, HojasDeCalculo>();

        services.AddOptions<OpcionesAlmacen>().Bind(configuration.GetSection(OpcionesAlmacen.Seccion)).ValidateDataAnnotations();
        services.AddSingleton<IAlmacenArchivos, AlmacenArchivosLocal>();

        services.AddScoped<IRepositorioCierre, RepositorioCierre>();
        services.AddScoped<IRepositorioBoletas, RepositorioBoletas>();
        services.AddSingleton<IGeneradorBoletas, GeneradorBoletas>();
        services.AddScoped<IRepositorioEnlaces, RepositorioEnlaces>();
        services.AddScoped<IConsultasTutor, ConsultasTutor>();
        services.AddScoped<IRepositorioAvisos, RepositorioAvisos>();
        services.AddScoped<IAvisosDelTutor, AvisosDelTutor>();
        AgregarIa(services, configuration);
        AgregarCanales(services, configuration);
        AgregarPagos(services, configuration);

        AgregarCorreo(services, configuration);
        AgregarTrabajos(services, configuration);

        services.AddHealthChecks().AddDbContextCheck<AulisimaDbContext>("base_de_datos", tags: ["lista"]);
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

    /// <summary>SPEC-07: push y WhatsApp reales solo con credenciales en variables de entorno; si no, simulados.</summary>
    private static void AgregarCanales(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpcionesPush>()
            .Bind(configuration.GetSection(OpcionesPush.Seccion))
            .ValidateDataAnnotations()
            .Validate(
                o => o.Proveedor != OpcionesPush.ProveedorVapid
                    || (!string.IsNullOrWhiteSpace(o.ClavePublica) && !string.IsNullOrWhiteSpace(o.ClavePrivada)),
                "Faltan Push__ClavePublica y Push__ClavePrivada.")
            .ValidateOnStart();
        if ((configuration.GetSection(OpcionesPush.Seccion)[nameof(OpcionesPush.Proveedor)] ?? OpcionesPush.ProveedorSimulado)
            == OpcionesPush.ProveedorVapid)
        {
            services.AddHttpClient<ICanalPush, CanalPushVapid>(http => http.Timeout = TimeSpan.FromSeconds(15));
        }
        else
        {
            services.AddSingleton<CanalPushSimulado>();
            services.AddSingleton<ICanalPush>(sp => sp.GetRequiredService<CanalPushSimulado>());
        }

        services.AddOptions<OpcionesWhatsapp>()
            .Bind(configuration.GetSection(OpcionesWhatsapp.Seccion))
            .ValidateDataAnnotations()
            .Validate(
                o => o.Proveedor != OpcionesWhatsapp.ProveedorMeta
                    || (!string.IsNullOrWhiteSpace(o.Token) && !string.IsNullOrWhiteSpace(o.IdNumero) && !string.IsNullOrWhiteSpace(o.SecretoApp)),
                "Faltan Whatsapp__Token, Whatsapp__IdNumero y Whatsapp__SecretoApp.")
            .ValidateOnStart();
        if ((configuration.GetSection(OpcionesWhatsapp.Seccion)[nameof(OpcionesWhatsapp.Proveedor)] ?? OpcionesWhatsapp.ProveedorSimulado)
            == OpcionesWhatsapp.ProveedorMeta)
        {
            services.AddHttpClient<ICanalWhatsapp, CanalWhatsappMeta>(http =>
            {
                http.BaseAddress = new Uri("https://graph.facebook.com/");
                http.Timeout = TimeSpan.FromSeconds(15);
            });
        }
        else
        {
            services.AddSingleton<CanalWhatsappSimulado>();
            services.AddSingleton<ICanalWhatsapp>(sp => sp.GetRequiredService<CanalWhatsappSimulado>());
        }
    }

    /// <summary>SPEC-08: Stripe solo con sus claves en variables de entorno; si no, la pasarela simulada.</summary>
    private static void AgregarPagos(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpcionesPlanes>()
            .Bind(configuration.GetSection(OpcionesPlanes.Seccion))
            .ValidateDataAnnotations()
            .Validate(o => o.DiasMaximoPrueba >= o.DiasPrueba, "Planes__DiasMaximoPrueba debe ser mayor o igual a Planes__DiasPrueba.")
            .Validate(o => o.MontoMaximoOxxo > o.MontoMinimoOxxo, "Planes__MontoMaximoOxxo debe ser mayor que Planes__MontoMinimoOxxo.")
            .ValidateOnStart();
        services.AddOptions<OpcionesPagos>()
            .Bind(configuration.GetSection(OpcionesPagos.Seccion))
            .ValidateDataAnnotations()
            .Validate(
                o => o.Proveedor != OpcionesPagos.ProveedorStripe
                    || (!string.IsNullOrWhiteSpace(o.ClaveSecreta) && !string.IsNullOrWhiteSpace(o.SecretoWebhook)),
                "Faltan Pagos__ClaveSecreta y Pagos__SecretoWebhook.")
            .ValidateOnStart();
        services.AddScoped<IRepositorioPlanes, RepositorioPlanes>();
        services.AddScoped<IRepositorioCatalogo, RepositorioCatalogo>();
        if ((configuration.GetSection(OpcionesPagos.Seccion)[nameof(OpcionesPagos.Proveedor)] ?? OpcionesPagos.ProveedorSimulado)
            == OpcionesPagos.ProveedorStripe)
        {
            services.AddHttpClient<IPasarelaPagos, PasarelaStripe>(http =>
            {
                http.BaseAddress = new Uri("https://api.stripe.com/");
                http.Timeout = TimeSpan.FromSeconds(20);
            });
            services.AddHttpClient<ICatalogoPagos, CatalogoStripe>(http =>
            {
                http.BaseAddress = new Uri("https://api.stripe.com/");
                http.Timeout = TimeSpan.FromSeconds(20);
            });
        }
        else
        {
            services.AddSingleton<SesionesPagoSimuladas>();
            services.AddScoped<IPasarelaPagos, PasarelaSimulada>();
            services.AddScoped<ICatalogoPagos, CatalogoSimulado>();
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
            services.AddSingleton<ILectorListas, LectorClaude>();
        }
        else
        {
            services.AddSingleton<IRedactorObservaciones, RedactorSimulado>();
            services.AddSingleton<ILectorListas, LectorSimulado>();
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
