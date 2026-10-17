using AulaLista.Application.Comun;
using AulaLista.Application.Cuentas;
using AulaLista.Application.Evaluacion;
using AulaLista.Application.Grupos;
using AulaLista.Infrastructure.Comun;
using AulaLista.Infrastructure.Cuentas;
using AulaLista.Infrastructure.Evaluacion;
using AulaLista.Infrastructure.Grupos;
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
