using System.Text.Json;
using AulaLista.Application.Cierre;
using AulaLista.Application.Comun;
using AulaLista.Application.Cuentas;
using AulaLista.Application.Planes;
using AulaLista.Domain.Comun;
using AulaLista.Domain.Cuentas;
using AulaLista.Domain.Evaluacion;
using AulaLista.Domain.Operacion;
using AulaLista.Domain.Planes;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace AulaLista.Application.Operacion;

/// <summary>
/// SPEC-09: comprueba que quien llama a <c>/admin</c> es administrador (rol en la cuenta o correo en configuración, con el
/// correo verificado) y lo asume para RLS. Si no lo es, la ruta responde 404 como si no existiera.
/// </summary>
public sealed class AccesoAdmin(IRepositorioCuentas cuentas, IContextoUsuario usuario, IOptions<OpcionesOperacion> opciones)
{
    public async Task<bool> ComprobarAsync(CancellationToken ct)
    {
        if (usuario.DocenteId is not { } id || await cuentas.PorIdAsync(id, ct) is not { EstaActiva: true, CorreoVerificado: true } cuenta)
        {
            return false;
        }

        var porConfiguracion = opciones.Value.CorreosAdmin
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Any(c => Cuenta.NormalizarCorreo(c) == cuenta.Correo);
        if (!cuenta.EsAdmin && !porConfiguracion)
        {
            return false;
        }

        usuario.AsumirAdmin(id);
        return true;
    }
}

/// <summary>Semana del lunes (hora del centro de México) en que cae un instante.</summary>
internal static class Calendario
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById(Docente.ZonaHorariaPorDefecto);

    public static DateOnly Hoy(DateTimeOffset ahora) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(ahora, Zona).DateTime);

    public static DateOnly LunesDe(DateOnly dia) => dia.AddDays(-(((int)dia.DayOfWeek + 6) % 7));

    public static DateOnly MesDe(DateOnly dia) => new(dia.Year, dia.Month, 1);
}

public sealed record SemanaMetricasDto(
    DateOnly Semana,
    int DocentesActivos,
    int DocentesQueCierran,
    decimal? PorcentajeCierran,
    decimal? MinutosCierre,
    int ObservacionesAprobadas,
    decimal? PorcentajeObservacionesAceptadas,
    int EnlacesEnviados,
    int EnlacesAbiertos,
    decimal? PorcentajeTutoresAbren,
    int DocentesConPrueba,
    int DocentesQuePagan,
    decimal? PorcentajeConversion);

public sealed record MetasDto(
    int DocentesActivos, decimal PorcentajeCierran, decimal MinutosCierre, decimal PorcentajeObservacionesAceptadas,
    decimal PorcentajeTutoresAbren, decimal PorcentajeConversion);

/// <summary>Las 6 métricas del piloto (00-prd, sección 4) por semana, de la más antigua a la actual, con sus metas.</summary>
public sealed record MetricasDto(MetasDto Metas, IReadOnlyList<SemanaMetricasDto> Semanas);

/// <summary>
/// RF-091: métricas del piloto calculadas desde <c>evento_producto</c>. Activos: capturaron en los 14 días previos al fin
/// de la semana. Los porcentajes de cierre, tutores y conversión son acumulados hasta esa semana; el de observaciones y el
/// tiempo de cierre (mediana), de la semana.
/// </summary>
public sealed class MetricasCasoUso(IRepositorioAdmin repositorio, IOptions<OpcionesOperacion> opciones, TimeProvider reloj)
{
    public async Task<MetricasDto> EjecutarAsync(CancellationToken ct)
    {
        var semanas = opciones.Value.SemanasMetricas;
        var desde = Calendario.LunesDe(Calendario.Hoy(reloj.GetUtcNow())).AddDays(-7 * (semanas - 1));
        var filas = await repositorio.MetricasAsync(desde, semanas, DistanciaEdicion.CambioMayor, ct);
        var m = opciones.Value.Metas;
        return new MetricasDto(
            new MetasDto(m.DocentesActivos, m.PorcentajeCierran, m.MinutosCierre, m.PorcentajeObservacionesAceptadas, m.PorcentajeTutoresAbren, m.PorcentajeConversion),
            filas.Select(f => new SemanaMetricasDto(
                f.Semana,
                f.DocentesActivos,
                f.DocentesCierran,
                Porcentaje(f.DocentesCierran, f.DocentesActivos),
                f.MinutosCierre is { } minutos ? decimal.Round(minutos, 0) : null,
                f.Observaciones,
                Porcentaje(f.ObservacionesMenores, f.Observaciones),
                f.Enlaces,
                f.Aperturas,
                Porcentaje(f.Aperturas, f.Enlaces),
                f.ConPrueba,
                f.Pagan,
                Porcentaje(f.Pagan, f.ConPrueba))).ToList());
    }

    internal static decimal? Porcentaje(int parte, int total) =>
        total == 0 ? null : decimal.Round(100m * parte / total, 1, MidpointRounding.AwayFromZero);
}

/// <summary>Costo variable estimado: tokens de IA al precio vigente en configuración y el estimado de cada WhatsApp.</summary>
public sealed class Costeo(IOptions<OpcionesIa> ia, IOptions<OpcionesOperacion> operacion)
{
    private const decimal Millon = 1_000_000m;

    public decimal Ia(long entrada, long salida, long cache)
    {
        var precio = ia.Value;
        var usd = ((entrada * precio.PrecioEntradaUsdPorMillon) + (salida * precio.PrecioSalidaUsdPorMillon)
            + (cache * precio.PrecioCacheLeidoUsdPorMillon)) / Millon;
        return decimal.Round(usd * operacion.Value.TipoCambioUsdMxn, 2);
    }
}

public sealed record ConsumoDto(int LlamadasIa, long TokensIa, int MensajesWhatsapp, decimal CostoMxn);

/// <summary>Plan del docente: <c>gratis</c>, <c>prueba</c>, <c>tarjeta</c>, <c>oxxo</c> o <c>en_gracia</c>.</summary>
public sealed record DocenteAdminDto(
    Guid Id,
    string Correo,
    string? Nombre,
    bool CuentaActiva,
    string Plan,
    int Grupos,
    int GruposPro,
    DateTimeOffset RegistradoEn,
    DateTimeOffset? UltimoAcceso,
    ConsumoDto Consumo);

public sealed record DocentesAdminDto(DateOnly Mes, IReadOnlyList<DocenteAdminDto> Docentes);

/// <summary>RF-091: docentes con sus grupos, su plan, su último acceso y el consumo del mes. Nada de alumnos ni tutores (RN-090).</summary>
public sealed class DocentesAdminCasoUso(
    IRepositorioAdmin repositorio, Costeo costeo, IOptions<OpcionesPlanes> planes, TimeProvider reloj)
{
    public async Task<DocentesAdminDto> EjecutarAsync(CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var mes = Calendario.MesDe(Calendario.Hoy(ahora));
        var filas = await repositorio.DocentesAsync(mes, ct);
        return new DocentesAdminDto(mes, filas.Select(f => new DocenteAdminDto(
            f.DocenteId,
            f.Correo,
            string.IsNullOrWhiteSpace(f.Nombre) ? null : f.Nombre,
            f.Estado == EstadoCuenta.Activa,
            Plan(f, ahora),
            f.Grupos,
            f.GruposPro,
            f.RegistradoEn,
            f.UltimoAcceso,
            new ConsumoDto(
                f.LlamadasIa,
                f.TokensEntrada + f.TokensSalida + f.TokensCache,
                f.MensajesWhatsapp,
                costeo.Ia(f.TokensEntrada, f.TokensSalida, f.TokensCache) + decimal.Round(f.CostoWhatsapp, 2)))).ToList());
    }

    private string Plan(FilaDocente f, DateTimeOffset ahora) => f.EstadoSuscripcion switch
    {
        EstadoSuscripcion.EnGracia => "en_gracia",
        EstadoSuscripcion.Activa when f.Modalidad == ModalidadPago.PrepagoOxxo => "oxxo",
        EstadoSuscripcion.Activa => "tarjeta",
        _ when f.PruebaInicio is { } inicio && ahora < inicio.AddDays(planes.Value.DiasMaximoPrueba) => "prueba",
        _ => "gratis",
    };
}

/// <summary><see cref="Plan"/>: <c>pro</c>, <c>prueba</c> o <c>gratis</c>. El ingreso de un grupo Pro es su parte del cobro mensual.</summary>
public sealed record CostoGrupoDto(
    Guid GrupoId,
    Guid DocenteId,
    string Correo,
    string Nivel,
    int Grado,
    string Nombre,
    string? Materia,
    string Plan,
    int LlamadasIa,
    long TokensIa,
    int MensajesWhatsapp,
    decimal CostoIa,
    decimal CostoWhatsapp,
    decimal Costo,
    decimal Ingreso,
    bool SobreUmbral,
    bool IaSobreUmbral);

public sealed record CostosDto(
    DateOnly Mes, string Moneda, decimal UmbralGrupo, decimal UmbralIa, decimal CostoTotal, decimal IngresoTotal, IReadOnlyList<CostoGrupoDto> Grupos);

/// <summary>
/// RF-091 y RNF-022: costo estimado del mes por grupo contra su ingreso. Cada grupo Pro pagado recibe una parte igual del
/// cobro mensual de su docente (79 + 29 por adicional, de configuración); la prueba y Gratis no generan ingreso.
/// </summary>
public sealed class CostosCasoUso(
    IRepositorioAdmin repositorio, Costeo costeo, IOptions<OpcionesPlanes> planes, IOptions<OpcionesOperacion> operacion, TimeProvider reloj)
{
    public async Task<CostosDto> EjecutarAsync(CancellationToken ct)
    {
        var mes = Calendario.MesDe(Calendario.Hoy(reloj.GetUtcNow()));
        var filas = await repositorio.CostosAsync(mes, planes.Value.DiasMaximoPrueba, ct);
        var p = planes.Value;
        var o = operacion.Value;
        var proPorDocente = filas.Where(f => f.ProPagado).GroupBy(f => f.DocenteId).ToDictionary(g => g.Key, g => g.Count());
        var grupos = filas.Select(f =>
        {
            var ia = costeo.Ia(f.TokensEntrada, f.TokensSalida, f.TokensCache);
            var whatsapp = decimal.Round(f.CostoWhatsapp, 2);
            var ingreso = f.ProPagado && proPorDocente.TryGetValue(f.DocenteId, out var n)
                ? decimal.Round(Tarifa.Mensual(n, p.PrecioPrimerGrupo, p.PrecioGrupoAdicional) / n, 2)
                : 0m;
            var costo = ia + whatsapp;
            return new CostoGrupoDto(
                f.GrupoId, f.DocenteId, f.Correo, f.Nivel, f.Grado, f.Nombre, f.Materia,
                f.ProPagado ? PlanVigente.Pro : f.EnPrueba ? PlanVigente.Prueba : PlanVigente.Gratis,
                f.LlamadasIa, f.TokensEntrada + f.TokensSalida + f.TokensCache, f.MensajesWhatsapp,
                ia, whatsapp, costo, ingreso, costo > o.AlertaCostoGrupoMxn, ia > o.AlertaCostoIaGrupoMxn);
        })
        .OrderByDescending(g => g.Costo).ThenBy(g => g.Correo, StringComparer.Ordinal).ThenBy(g => g.Grado)
        .ToList();
        return new CostosDto(mes, p.Moneda, o.AlertaCostoGrupoMxn, o.AlertaCostoIaGrupoMxn, grupos.Sum(g => g.Costo), grupos.Sum(g => g.Ingreso), grupos);
    }
}

public sealed record ErrorTrabajoDto(
    Guid Id, string Origen, string Trabajo, string? Correo, Guid? ReferenciaId, string TipoError, DateTimeOffset Fecha);

/// <summary>RF-091: los errores más recientes de los trabajos en segundo plano (IA, WhatsApp, correo, pagos, boletas).</summary>
public sealed class ErroresAdminCasoUso(IRepositorioAdmin repositorio)
{
    public const int Limite = 100;

    public async Task<IReadOnlyList<ErrorTrabajoDto>> EjecutarAsync(CancellationToken ct) =>
        (await repositorio.ErroresAsync(Limite, ct))
            .Select(e => new ErrorTrabajoDto(e.Id, e.Origen, e.Trabajo, e.Correo, e.ReferenciaId, e.TipoError, e.Fecha)).ToList();
}

/// <summary>Un campo que cambió en una acción del panel, con su valor como texto.</summary>
public sealed record CambioAuditoriaDto(string Campo, string? Antes, string? Despues);

public sealed record AuditoriaAdminDto(
    Guid Id, string? Correo, string Accion, string Entidad, Guid? EntidadId, IReadOnlyList<CambioAuditoriaDto> Cambios, DateTimeOffset Fecha);

/// <summary><see cref="Siguiente"/> es el cursor para pedir los anteriores; nulo si no hay más.</summary>
public sealed record PaginaAuditoriaDto(IReadOnlyList<AuditoriaAdminDto> Registros, DateTimeOffset? Siguiente);

/// <summary>RF-093 y RN-095: acciones de los administradores, de la más reciente a la más antigua, por cursor de fecha.</summary>
public sealed class AuditoriaAdminCasoUso(IRepositorioAdmin repositorio)
{
    public const int TamanoPagina = 50;

    public async Task<PaginaAuditoriaDto> EjecutarAsync(DateTimeOffset? antesDe, CancellationToken ct)
    {
        var filas = await repositorio.AuditoriaAsync(antesDe, TamanoPagina + 1, ct);
        var pagina = filas.Take(TamanoPagina).Select(f => new AuditoriaAdminDto(
            f.Id, f.Correo, f.Accion, f.Entidad, f.EntidadId, Cambios(f.Antes, f.Despues), f.Fecha)).ToList();
        return new PaginaAuditoriaDto(pagina, filas.Count > TamanoPagina ? pagina[^1].Fecha : null);
    }

    /// <summary>Los campos de las fotos antes y después que no coinciden; sin foto previa, todos los de después.</summary>
    internal static IReadOnlyList<CambioAuditoriaDto> Cambios(string? antes, string? despues)
    {
        var previos = Campos(antes);
        var nuevos = Campos(despues);
        return previos.Keys.Union(nuevos.Keys)
            .Select(c => new CambioAuditoriaDto(c, previos.GetValueOrDefault(c), nuevos.GetValueOrDefault(c)))
            .Where(c => c.Antes != c.Despues)
            .ToList();
    }

    private static Dictionary<string, string?> Campos(string? json)
    {
        var campos = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
        {
            return campos;
        }

        using var documento = JsonDocument.Parse(json);
        foreach (var propiedad in documento.RootElement.EnumerateObject())
        {
            campos[propiedad.Name] = Texto(propiedad.Value);
        }

        return campos;
    }

    private static string? Texto(JsonElement valor) => valor.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => valor.GetString(),
        JsonValueKind.Array => string.Join(", ", valor.EnumerateArray().Select(Texto)),
        _ => valor.GetRawText(),
    };
}

/// <summary>RN-091 y RN-093: comportamientos del código desplegado; el panel solo deja elegir estos.</summary>
public static class ComportamientosCasoUso
{
    public static IReadOnlyList<string> Ejecutar() => Comportamiento.Todos;
}

public sealed record TipoRubroAdminDto(
    Guid Id,
    string Nombre,
    string Descripcion,
    string Icono,
    string Comportamiento,
    string FormaSugerida,
    IReadOnlyList<string> NombresSugeridos,
    int Orden,
    bool Activo,
    long Rubros);

public sealed record TipoRubroEntrada(
    string Nombre, string Descripcion, string Icono, string Comportamiento, string FormaSugerida, IReadOnlyList<string> NombresSugeridos,
    int Orden, bool Activo);

internal sealed class TipoRubroEntradaValidador : AbstractValidator<TipoRubroEntrada>
{
    public TipoRubroEntradaValidador()
    {
        RuleFor(t => t.Nombre).NotNull().MaximumLength(ReglasTipoRubro.LongitudNombre * 2);
        RuleFor(t => t.Descripcion).NotNull().MaximumLength(ReglasTipoRubro.LongitudDescripcion * 2);
        RuleFor(t => t.Icono).NotNull().MaximumLength(ReglasTipoRubro.LongitudIcono);
        RuleFor(t => t.Comportamiento).NotNull().MaximumLength(20);
        RuleFor(t => t.FormaSugerida).NotNull().MaximumLength(20);
        RuleFor(t => t.NombresSugeridos).NotNull().Must(n => n is null || n.Count <= ReglasTipoRubro.MaximoNombresSugeridos * 2);
    }
}

/// <summary>RF-092: el catálogo completo (también los inactivos) con cuántos rubros usan cada tipo.</summary>
public sealed class ListarTiposRubroAdminCasoUso(IRepositorioAdmin repositorio)
{
    public async Task<IReadOnlyList<TipoRubroAdminDto>> EjecutarAsync(CancellationToken ct)
    {
        var uso = await repositorio.RubrosPorTipoAsync(ct);
        return (await repositorio.TiposRubroAsync(ct))
            .OrderBy(t => t.Orden).ThenBy(t => t.Nombre, StringComparer.CurrentCulture)
            .Select(t => CatalogoTipos.Dto(t, uso.GetValueOrDefault(t.Id))).ToList();
    }
}

internal static class CatalogoTipos
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static TipoRubroAdminDto Dto(TipoRubro t, long rubros) => new(
        t.Id, t.Nombre, t.Descripcion, t.Icono, t.Comportamiento, t.FormaSugerida, t.NombresSugeridos, t.Orden, t.Activo, rubros);

    /// <summary>La foto del tipo para la auditoría (RN-095): solo datos del catálogo.</summary>
    public static string Foto(TipoRubro t) => JsonSerializer.Serialize(
        new { t.Nombre, t.Descripcion, t.Icono, t.Comportamiento, t.FormaSugerida, t.NombresSugeridos, t.Orden, t.Activo }, Json);

    public static DatosTipoRubro Datos(TipoRubroEntrada e) =>
        new(e.Nombre ?? string.Empty, e.Descripcion ?? string.Empty, e.Icono, e.Comportamiento, e.FormaSugerida, e.NombresSugeridos ?? [], e.Orden, e.Activo);

    public static bool NombreRepetido(IEnumerable<TipoRubro> tipos, string nombre, Guid? excepto) =>
        tipos.Any(t => t.Id != excepto && string.Equals(t.Nombre.Trim(), nombre.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>RN-092: un tipo nuevo con un comportamiento existente, disponible para los docentes al instante.</summary>
public sealed class CrearTipoRubroCasoUso(IRepositorioAdmin repositorio, IContextoUsuario usuario, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<TipoRubroAdminDto>> EjecutarAsync(TipoRubroEntrada entrada, CancellationToken ct)
    {
        var datos = CatalogoTipos.Datos(entrada);
        if (ReglasTipoRubro.Validar(datos) is { } invalido)
        {
            return Resultado.Fallo<TipoRubroAdminDto>(invalido);
        }

        if (CatalogoTipos.NombreRepetido(await repositorio.TiposRubroAsync(ct), datos.Nombre, null))
        {
            return Resultado.Fallo<TipoRubroAdminDto>(ErroresOperacion.TipoRubroRepetido);
        }

        var ahora = reloj.GetUtcNow();
        var tipo = TipoRubro.Crear(datos, ahora);
        await unidad.EnTransaccionAsync(
            () =>
            {
                repositorio.Agregar(tipo);
                repositorio.Agregar(new RegistroAuditoriaAdmin(
                    usuario.AdminId!.Value, AccionAuditoriaAdmin.TipoRubroCreado, "tipo_rubro", tipo.Id, null, CatalogoTipos.Foto(tipo), ahora));
                return Task.FromResult(true);
            },
            ct);
        return Resultado.Exito(CatalogoTipos.Dto(tipo, 0));
    }
}

/// <summary>
/// RF-092 y RN-094: edita o desactiva un tipo. Nunca se borra; con rubros que lo usan su comportamiento no cambia, para que
/// esos rubros sigan calculándose igual.
/// </summary>
public sealed class EditarTipoRubroCasoUso(IRepositorioAdmin repositorio, IContextoUsuario usuario, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<Resultado<TipoRubroAdminDto>?> EjecutarAsync(Guid id, TipoRubroEntrada entrada, CancellationToken ct)
    {
        if (await repositorio.TipoRubroAsync(id, ct) is not { } tipo)
        {
            return null;
        }

        var datos = CatalogoTipos.Datos(entrada);
        if (ReglasTipoRubro.Validar(datos) is { } invalido)
        {
            return Resultado.Fallo<TipoRubroAdminDto>(invalido);
        }

        if (CatalogoTipos.NombreRepetido(await repositorio.TiposRubroAsync(ct), datos.Nombre, id))
        {
            return Resultado.Fallo<TipoRubroAdminDto>(ErroresOperacion.TipoRubroRepetido);
        }

        var rubros = (await repositorio.RubrosPorTipoAsync(ct)).GetValueOrDefault(id);
        if (rubros > 0 && datos.Comportamiento != tipo.Comportamiento)
        {
            return Resultado.Fallo<TipoRubroAdminDto>(ErroresOperacion.ComportamientoEnUso);
        }

        var antes = CatalogoTipos.Foto(tipo);
        var estabaActivo = tipo.Activo;
        var ahora = reloj.GetUtcNow();
        tipo.Editar(datos, ahora);
        var despues = CatalogoTipos.Foto(tipo);
        if (antes == despues)
        {
            return Resultado.Exito(CatalogoTipos.Dto(tipo, rubros));
        }

        var accion = (estabaActivo, tipo.Activo) switch
        {
            (true, false) => AccionAuditoriaAdmin.TipoRubroDesactivado,
            (false, true) => AccionAuditoriaAdmin.TipoRubroActivado,
            _ => AccionAuditoriaAdmin.TipoRubroEditado,
        };
        await unidad.EnTransaccionAsync(
            () =>
            {
                repositorio.Agregar(new RegistroAuditoriaAdmin(usuario.AdminId!.Value, accion, "tipo_rubro", tipo.Id, antes, despues, ahora));
                return Task.FromResult(true);
            },
            ct);
        return Resultado.Exito(CatalogoTipos.Dto(tipo, rubros));
    }
}

/// <summary>
/// SPEC-09: <c>--admin correo</c> da el rol de administrador a una cuenta existente con el correo verificado. Queda en la
/// bitácora general como acción del sistema.
/// </summary>
public sealed class HacerAdminCasoUso(
    IRepositorioCuentas cuentas, IAuditoria auditoria, IContextoUsuario usuario, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    public async Task<bool> EjecutarAsync(string correo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(correo)
            || await cuentas.PorCorreoAsync(Cuenta.NormalizarCorreo(correo), ct) is not { EstaActiva: true, CorreoVerificado: true } encontrada)
        {
            return false;
        }

        // Corre fuera de una solicitud: se asume la cuenta para que RLS deje cambiarla.
        usuario.AsumirDocente(encontrada.Id);
        return await unidad.EnTransaccionAsync(async () =>
        {
            if (await cuentas.PorIdAsync(encontrada.Id, ct) is not { } cuenta)
            {
                return false;
            }

            if (!cuenta.EsAdmin)
            {
                cuenta.HacerAdmin(reloj.GetUtcNow());
                auditoria.Registrar(AccionAuditoria.RolAdminAsignado, "cuenta", cuenta.Id, null);
            }

            return true;
        }, ct);
    }
}
