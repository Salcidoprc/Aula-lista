using AulaLista.Application.Comun;
using AulaLista.Domain.Avisos;
using AulaLista.Domain.Boletas;
using AulaLista.Domain.Cierre;
using AulaLista.Domain.Cuentas;
using AulaLista.Domain.Evaluacion;
using AulaLista.Domain.Grupos;
using AulaLista.Domain.Operacion;
using AulaLista.Domain.Planes;
using AulaLista.Domain.Tutores;
using Microsoft.EntityFrameworkCore;

namespace AulaLista.Infrastructure.Persistencia;

public sealed class AulaListaDbContext : DbContext
{
    private readonly IContextoUsuario? _contexto;

    public AulaListaDbContext(DbContextOptions<AulaListaDbContext> options, IContextoUsuario contexto)
        : base(options) => _contexto = contexto;

    /// <summary>Para migraciones y herramientas, sin docente: los filtros por dueño no devuelven filas.</summary>
    public AulaListaDbContext(DbContextOptions<AulaListaDbContext> options)
        : base(options)
    {
    }

    public DbSet<CicloEscolar> CiclosEscolares => Set<CicloEscolar>();

    public DbSet<Unidad> Unidades => Set<Unidad>();

    public DbSet<Grupo> Grupos => Set<Grupo>();

    public DbSet<Periodo> Periodos => Set<Periodo>();

    public DbSet<Alumno> Alumnos => Set<Alumno>();

    public DbSet<VinculoTutor> VinculosTutor => Set<VinculoTutor>();

    public DbSet<Importacion> Importaciones => Set<Importacion>();

    public DbSet<TipoRubro> TiposRubro => Set<TipoRubro>();

    public DbSet<Rubro> Rubros => Set<Rubro>();

    public DbSet<Criterio> Criterios => Set<Criterio>();

    public DbSet<Actividad> Actividades => Set<Actividad>();

    public DbSet<Calificacion> Calificaciones => Set<Calificacion>();

    public DbSet<CalificacionCriterio> CalificacionesCriterio => Set<CalificacionCriterio>();

    public DbSet<CambioSincronizado> CambiosSincronizados => Set<CambioSincronizado>();

    public DbSet<Equipo> Equipos => Set<Equipo>();

    public DbSet<EquipoAlumno> EquiposAlumno => Set<EquipoAlumno>();

    public DbSet<ActividadEquipo> ActividadesEquipo => Set<ActividadEquipo>();

    public DbSet<Nota> Notas => Set<Nota>();

    public DbSet<TomaAsistencia> TomasAsistencia => Set<TomaAsistencia>();

    public DbSet<Asistencia> Asistencias => Set<Asistencia>();

    public DbSet<Observacion> Observaciones => Set<Observacion>();

    public DbSet<EstiloObservaciones> EstilosObservaciones => Set<EstiloObservaciones>();

    public DbSet<LlamadaIa> LlamadasIa => Set<LlamadaIa>();

    public DbSet<Boleta> Boletas => Set<Boleta>();

    /// <summary>Sin filtro por dueño ni RLS, como <see cref="Sesiones"/>: se buscan por hash del token (ADR-0013).</summary>
    public DbSet<EnlaceTutor> EnlacesTutor => Set<EnlaceTutor>();

    public DbSet<SesionTutor> SesionesTutor => Set<SesionTutor>();

    public DbSet<Aviso> Avisos => Set<Aviso>();

    public DbSet<EntregaAviso> EntregasAviso => Set<EntregaAviso>();

    public DbSet<SuscripcionPush> SuscripcionesPush => Set<SuscripcionPush>();

    public DbSet<MensajeWhatsapp> MensajesWhatsapp => Set<MensajeWhatsapp>();

    public DbSet<Suscripcion> Suscripciones => Set<Suscripcion>();

    public DbSet<Pago> Pagos => Set<Pago>();

    public DbSet<EventoPago> EventosPago => Set<EventoPago>();

    public DbSet<Cuenta> Cuentas => Set<Cuenta>();

    public DbSet<TokenCuenta> TokensCuenta => Set<TokenCuenta>();

    public DbSet<Sesion> Sesiones => Set<Sesion>();

    public DbSet<Docente> Docentes => Set<Docente>();

    public DbSet<AceptacionLegal> AceptacionesLegales => Set<AceptacionLegal>();

    public DbSet<Exportacion> Exportaciones => Set<Exportacion>();

    public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();

    /// <summary>Docente de la solicitud para los filtros globales por dueño (además de RLS).</summary>
    private Guid? DocenteActual => _contexto?.DocenteId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AulaListaDbContext).Assembly);

        modelBuilder.Entity<Docente>().HasQueryFilter(d => d.Id == DocenteActual);
        modelBuilder.Entity<AceptacionLegal>().HasQueryFilter(a => a.DocenteId == DocenteActual);
        modelBuilder.Entity<Exportacion>().HasQueryFilter(e => e.DocenteId == DocenteActual);
        modelBuilder.Entity<CicloEscolar>().HasQueryFilter(c => c.DocenteId == DocenteActual);
        modelBuilder.Entity<Unidad>().HasQueryFilter(u => u.DocenteId == DocenteActual);
        modelBuilder.Entity<Grupo>().HasQueryFilter(g => g.DocenteId == DocenteActual);
        modelBuilder.Entity<Periodo>().HasQueryFilter(p => p.DocenteId == DocenteActual);
        modelBuilder.Entity<Alumno>().HasQueryFilter(a => a.DocenteId == DocenteActual);
        modelBuilder.Entity<VinculoTutor>().HasQueryFilter(v => v.DocenteId == DocenteActual);
        modelBuilder.Entity<Importacion>().HasQueryFilter(i => i.DocenteId == DocenteActual);
        modelBuilder.Entity<Rubro>().HasQueryFilter(r => r.DocenteId == DocenteActual);
        modelBuilder.Entity<Criterio>().HasQueryFilter(c => c.DocenteId == DocenteActual);
        modelBuilder.Entity<Actividad>().HasQueryFilter(a => a.DocenteId == DocenteActual);
        modelBuilder.Entity<Calificacion>().HasQueryFilter(c => c.DocenteId == DocenteActual);
        modelBuilder.Entity<CalificacionCriterio>().HasQueryFilter(c => c.DocenteId == DocenteActual);
        modelBuilder.Entity<CambioSincronizado>().HasQueryFilter(c => c.DocenteId == DocenteActual);
        modelBuilder.Entity<Equipo>().HasQueryFilter(e => e.DocenteId == DocenteActual);
        modelBuilder.Entity<EquipoAlumno>().HasQueryFilter(e => e.DocenteId == DocenteActual);
        modelBuilder.Entity<ActividadEquipo>().HasQueryFilter(a => a.DocenteId == DocenteActual);
        modelBuilder.Entity<Nota>().HasQueryFilter(n => n.DocenteId == DocenteActual);
        modelBuilder.Entity<TomaAsistencia>().HasQueryFilter(t => t.DocenteId == DocenteActual);
        modelBuilder.Entity<Asistencia>().HasQueryFilter(a => a.DocenteId == DocenteActual);
        modelBuilder.Entity<Observacion>().HasQueryFilter(o => o.DocenteId == DocenteActual);
        modelBuilder.Entity<EstiloObservaciones>().HasQueryFilter(e => e.DocenteId == DocenteActual);
        modelBuilder.Entity<LlamadaIa>().HasQueryFilter(l => l.DocenteId == DocenteActual);
        modelBuilder.Entity<Boleta>().HasQueryFilter(b => b.DocenteId == DocenteActual);
        modelBuilder.Entity<Aviso>().HasQueryFilter(a => a.DocenteId == DocenteActual);
        modelBuilder.Entity<EntregaAviso>().HasQueryFilter(e => e.DocenteId == DocenteActual);
        modelBuilder.Entity<MensajeWhatsapp>().HasQueryFilter(m => m.DocenteId == DocenteActual);
        modelBuilder.Entity<Suscripcion>().HasQueryFilter(s => s.DocenteId == DocenteActual);
        modelBuilder.Entity<Pago>().HasQueryFilter(p => p.DocenteId == DocenteActual);
        modelBuilder.Entity<EventoPago>().HasQueryFilter(e => e.DocenteId == DocenteActual);
    }
}
