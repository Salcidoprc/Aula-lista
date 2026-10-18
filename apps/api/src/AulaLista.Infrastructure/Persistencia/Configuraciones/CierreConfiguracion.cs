using AulaLista.Domain.Cierre;
using AulaLista.Domain.Cuentas;
using AulaLista.Domain.Grupos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AulaLista.Infrastructure.Persistencia.Configuraciones;

internal sealed class ObservacionConfiguracion : IEntityTypeConfiguration<Observacion>
{
    public void Configure(EntityTypeBuilder<Observacion> builder)
    {
        builder.ToTable("observacion", t =>
        {
            t.HasCheckConstraint("ck_observacion_estado", $"estado IN ({string.Join(", ", EstadoObservacion.Todos.Select(e => $"'{e}'"))})");
            t.HasCheckConstraint("ck_observacion_regeneraciones", $"regeneraciones BETWEEN 0 AND {Observacion.RegeneracionesMaximas}");
            t.HasCheckConstraint("ck_observacion_aprobada", "estado NOT IN ('aprobada', 'publicada') OR texto_final IS NOT NULL");
        });
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Estado).HasMaxLength(20).IsRequired();
        builder.Property(o => o.Texto).HasMaxLength(Observacion.LongitudTexto);
        builder.Property(o => o.BorradorIa).HasMaxLength(Observacion.LongitudTexto);
        builder.Property(o => o.TextoFinal).HasMaxLength(Observacion.LongitudTexto);
        builder.Property(o => o.Instruccion).HasMaxLength(Observacion.LongitudInstruccion);
        builder.Property(o => o.Modelo).HasMaxLength(60);
        builder.Property(o => o.VersionPrompt).HasMaxLength(40);
        builder.Property(o => o.Fallo).HasMaxLength(20);
        builder.Ignore(o => o.EstaAprobada);
        builder.HasIndex(o => new { o.AlumnoId, o.PeriodoId }).IsUnique(); // reglas/03: una por alumno y periodo
        builder.HasIndex(o => o.PeriodoId);
        builder.HasIndex(o => new { o.DocenteId, o.Estado });
        builder.HasOne<Alumno>().WithMany().HasForeignKey(o => o.AlumnoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Periodo>().WithMany().HasForeignKey(o => o.PeriodoId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EstiloObservacionesConfiguracion : IEntityTypeConfiguration<EstiloObservaciones>
{
    public void Configure(EntityTypeBuilder<EstiloObservaciones> builder)
    {
        builder.ToTable("estilo_observaciones", t =>
        {
            t.HasCheckConstraint("ck_estilo_observaciones_longitud", "longitud IN ('breve', 'normal', 'detallada')");
            t.HasCheckConstraint("ck_estilo_observaciones_persona", "persona IN ('segunda', 'tercera')");
            t.HasCheckConstraint("ck_estilo_observaciones_ejemplos", $"cardinality(ejemplos) <= {EstiloObservaciones.MaximoEjemplos}");
        });
        builder.HasKey(e => e.DocenteId);
        builder.Property(e => e.DocenteId).ValueGeneratedNever();
        builder.Property(e => e.Longitud).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Persona).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Ejemplos).HasColumnType("text[]").IsRequired();
        builder.HasOne<Docente>().WithOne().HasForeignKey<EstiloObservaciones>(e => e.DocenteId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LlamadaIaConfiguracion : IEntityTypeConfiguration<LlamadaIa>
{
    public void Configure(EntityTypeBuilder<LlamadaIa> builder)
    {
        builder.ToTable("llamada_ia");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Modelo).HasMaxLength(60).IsRequired();
        builder.Property(l => l.VersionPrompt).HasMaxLength(40).IsRequired();
        builder.Property(l => l.Resultado).HasMaxLength(20).IsRequired();
        builder.HasIndex(l => new { l.DocenteId, l.CreatedAt });
        builder.HasIndex(l => new { l.GrupoId, l.CreatedAt });
        builder.HasOne<Docente>().WithMany().HasForeignKey(l => l.DocenteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Grupo>().WithMany().HasForeignKey(l => l.GrupoId).OnDelete(DeleteBehavior.Cascade);
    }
}
