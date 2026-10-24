using System.Text.Json;
using AulaLista.Application.Operacion;
using AulaLista.Domain.Evaluacion;
using AulaLista.Domain.Operacion;
using AulaLista.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AulaLista.Infrastructure.Operacion;

/// <summary>
/// SPEC-09: las lecturas entre docentes pasan por funciones <c>admin_*</c> (<c>SECURITY DEFINER</c>), que solo devuelven
/// conteos, correos y nombres de docentes (RN-090). El catálogo y su auditoría se escriben como el administrador.
/// </summary>
internal sealed class RepositorioAdmin(AulaListaDbContext db) : IRepositorioAdmin
{
    public async Task<IReadOnlyList<FilaMetricas>> MetricasAsync(
        DateOnly desde, int semanas, decimal umbralCambioMayor, CancellationToken cancellationToken) =>
        (await db.Database.SqlQuery<Metricas>($"SELECT * FROM admin_metricas({desde}, {semanas}, {umbralCambioMayor})")
            .ToListAsync(cancellationToken))
        .Select(m => new FilaMetricas(
            DateOnly.FromDateTime(m.Semana), m.DocentesActivos, m.DocentesCierran, m.MinutosCierre, m.Observaciones,
            m.ObservacionesMenores, m.Enlaces, m.Aperturas, m.ConPrueba, m.Pagan))
        .ToList();

    public async Task<IReadOnlyList<FilaDocente>> DocentesAsync(DateOnly mes, CancellationToken cancellationToken) =>
        (await db.Database.SqlQuery<Docente>($"SELECT * FROM admin_docentes({mes})").ToListAsync(cancellationToken))
        .Select(d => new FilaDocente(
            d.DocenteId, d.Correo, d.Nombre, d.Estado, d.RegistradoEn, d.UltimoAcceso, d.Grupos, d.GruposPro, d.EstadoSuscripcion,
            d.Modalidad, d.PruebaInicio, d.LlamadasIa, d.TokensEntrada, d.TokensSalida, d.TokensCache, d.MensajesWhatsapp, d.CostoWhatsapp))
        .ToList();

    public async Task<IReadOnlyList<FilaCosto>> CostosAsync(DateOnly mes, int diasMaximoPrueba, CancellationToken cancellationToken) =>
        (await db.Database.SqlQuery<Costo>($"SELECT * FROM admin_costos({mes}, {diasMaximoPrueba})").ToListAsync(cancellationToken))
        .Select(c => new FilaCosto(
            c.GrupoId, c.DocenteId, c.Correo, c.Nivel, c.Grado, c.Nombre, c.Materia, c.ProPagado, c.EnPrueba, c.LlamadasIa, c.TokensEntrada,
            c.TokensSalida, c.TokensCache, c.MensajesWhatsapp, c.CostoWhatsapp))
        .ToList();

    public async Task<IReadOnlyList<FilaError>> ErroresAsync(int limite, CancellationToken cancellationToken) =>
        (await db.Database.SqlQuery<Error>($"SELECT * FROM admin_errores({limite})").ToListAsync(cancellationToken))
        .Select(e => new FilaError(e.Id, e.Origen, e.Trabajo, e.DocenteId, e.Correo, e.ReferenciaId, e.TipoError, e.Fecha))
        .ToList();

    public async Task<IReadOnlyList<FilaAuditoria>> AuditoriaAsync(DateTimeOffset? antesDe, int limite, CancellationToken cancellationToken) =>
        (await db.Database.SqlQuery<Auditoria>($"SELECT * FROM admin_auditoria({antesDe}, {limite})").ToListAsync(cancellationToken))
        .Select(a => new FilaAuditoria(a.Id, a.AdminId, a.Correo, a.Accion, a.Entidad, a.EntidadId, a.Antes, a.Despues, a.Fecha))
        .ToList();

    public async Task<IReadOnlyDictionary<Guid, long>> RubrosPorTipoAsync(CancellationToken cancellationToken) =>
        (await db.Database.SqlQuery<UsoTipo>($"SELECT * FROM admin_rubros_por_tipo()").ToListAsync(cancellationToken))
        .ToDictionary(u => u.TipoRubroId, u => u.Rubros);

    public async Task<IReadOnlyList<TipoRubro>> TiposRubroAsync(CancellationToken cancellationToken) =>
        await db.TiposRubro.ToListAsync(cancellationToken);

    public Task<TipoRubro?> TipoRubroAsync(Guid id, CancellationToken cancellationToken) =>
        db.TiposRubro.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public void Agregar(TipoRubro tipo) => db.TiposRubro.Add(tipo);

    public void Agregar(RegistroAuditoriaAdmin registro) => db.AuditoriaAdmin.Add(registro);

    private sealed class Metricas
    {
        public DateTime Semana { get; init; }

        public int DocentesActivos { get; init; }

        public int DocentesCierran { get; init; }

        public decimal? MinutosCierre { get; init; }

        public int Observaciones { get; init; }

        public int ObservacionesMenores { get; init; }

        public int Enlaces { get; init; }

        public int Aperturas { get; init; }

        public int ConPrueba { get; init; }

        public int Pagan { get; init; }
    }

    private sealed class Docente
    {
        public Guid DocenteId { get; init; }

        public string Correo { get; init; } = string.Empty;

        public string? Nombre { get; init; }

        public string Estado { get; init; } = string.Empty;

        public DateTimeOffset RegistradoEn { get; init; }

        public DateTimeOffset? UltimoAcceso { get; init; }

        public int Grupos { get; init; }

        public int GruposPro { get; init; }

        public string? EstadoSuscripcion { get; init; }

        public string? Modalidad { get; init; }

        public DateTimeOffset? PruebaInicio { get; init; }

        public int LlamadasIa { get; init; }

        public long TokensEntrada { get; init; }

        public long TokensSalida { get; init; }

        public long TokensCache { get; init; }

        public int MensajesWhatsapp { get; init; }

        public decimal CostoWhatsapp { get; init; }
    }

    private sealed class Costo
    {
        public Guid GrupoId { get; init; }

        public Guid DocenteId { get; init; }

        public string Correo { get; init; } = string.Empty;

        public string Nivel { get; init; } = string.Empty;

        public int Grado { get; init; }

        public string Nombre { get; init; } = string.Empty;

        public string? Materia { get; init; }

        public bool ProPagado { get; init; }

        public bool EnPrueba { get; init; }

        public int LlamadasIa { get; init; }

        public long TokensEntrada { get; init; }

        public long TokensSalida { get; init; }

        public long TokensCache { get; init; }

        public int MensajesWhatsapp { get; init; }

        public decimal CostoWhatsapp { get; init; }
    }

    private sealed class Error
    {
        public Guid Id { get; init; }

        public string Origen { get; init; } = string.Empty;

        public string Trabajo { get; init; } = string.Empty;

        public Guid? DocenteId { get; init; }

        public string? Correo { get; init; }

        public Guid? ReferenciaId { get; init; }

        public string TipoError { get; init; } = string.Empty;

        public DateTimeOffset Fecha { get; init; }
    }

    private sealed class Auditoria
    {
        public Guid Id { get; init; }

        public Guid AdminId { get; init; }

        public string? Correo { get; init; }

        public string Accion { get; init; } = string.Empty;

        public string Entidad { get; init; } = string.Empty;

        public Guid? EntidadId { get; init; }

        public string? Antes { get; init; }

        public string? Despues { get; init; }

        public DateTimeOffset Fecha { get; init; }
    }

    private sealed class UsoTipo
    {
        public Guid TipoRubroId { get; init; }

        public long Rubros { get; init; }
    }
}

/// <summary>RF-090: inserta en la transacción en curso; la clave única descarta repetidos sin error.</summary>
internal sealed class EventosProducto(AulaListaDbContext db, TimeProvider reloj) : IEventosProducto
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task RegistrarAsync(
        string nombre, Guid docenteId, IReadOnlyDictionary<string, object?> propiedades, string? clave, CancellationToken cancellationToken)
    {
        var ahora = reloj.GetUtcNow();
        var json = JsonSerializer.Serialize(propiedades, Json);
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO evento_producto (id, docente_id, nombre, propiedades, clave, fecha)
            VALUES ({Guid.CreateVersion7(ahora)}, {docenteId}, {nombre}, CAST({json} AS jsonb), {clave}, {ahora})
            ON CONFLICT DO NOTHING
            """,
            cancellationToken);
    }
}

/// <summary>Errores de trabajos para el panel, en su propia conexión: el trabajo que falló pudo deshacer su transacción.</summary>
internal sealed class RegistroErrores(IServiceScopeFactory alcances, TimeProvider reloj) : IRegistroErrores
{
    public async Task RegistrarAsync(
        string origen, string trabajo, Guid? docenteId, Guid? referenciaId, string tipoError, CancellationToken cancellationToken)
    {
        var ahora = reloj.GetUtcNow();
        var tipo = tipoError.Length > 200 ? tipoError[..200] : tipoError;
        await using var alcance = alcances.CreateAsyncScope();
        var db = alcance.ServiceProvider.GetRequiredService<AulaListaDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO error_trabajo (id, origen, trabajo, docente_id, referencia_id, tipo_error, fecha)
            VALUES ({Guid.CreateVersion7(ahora)}, {origen}, {trabajo}, {docenteId}, {referenciaId}, {tipo}, {ahora})
            """,
            cancellationToken);
    }
}

internal sealed class RetencionGrupos(AulaListaDbContext db) : IRetencionGrupos
{
    public async Task<IReadOnlyList<Guid>> EliminarArchivadosAsync(DateOnly cicloTerminadoAntesDe, CancellationToken cancellationToken) =>
        await db.Database
            .SqlQuery<Guid>($"SELECT periodo_id AS \"Value\" FROM eliminar_grupos_archivados_vencidos({cicloTerminadoAntesDe})")
            .ToListAsync(cancellationToken);
}
