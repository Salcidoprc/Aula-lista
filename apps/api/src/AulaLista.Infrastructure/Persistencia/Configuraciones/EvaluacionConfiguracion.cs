using AulaLista.Domain.Cuentas;
using AulaLista.Domain.Evaluacion;
using AulaLista.Domain.Grupos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AulaLista.Infrastructure.Persistencia.Configuraciones;

internal static class RestriccionesEvaluacion
{
    public const string Formas = "'numerica', 'cumplimiento'";
    public const string Marcas = "'ninguna', 'cumplio', 'no_cumplio', 'no_entrego', 'exento'";
}

internal sealed class TipoRubroConfiguracion : IEntityTypeConfiguration<TipoRubro>
{
    public void Configure(EntityTypeBuilder<TipoRubro> builder)
    {
        builder.ToTable("tipo_rubro", t =>
        {
            t.HasCheckConstraint("ck_tipo_rubro_comportamiento", "comportamiento IN ('actividades', 'asistencia')");
            t.HasCheckConstraint("ck_tipo_rubro_forma_sugerida", $"forma_sugerida IN ({RestriccionesEvaluacion.Formas})");
        });
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Nombre).HasMaxLength(40).IsRequired();
        builder.Property(t => t.Descripcion).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Icono).HasMaxLength(40).IsRequired();
        builder.Property(t => t.Comportamiento).HasMaxLength(20).IsRequired();
        builder.Property(t => t.FormaSugerida).HasMaxLength(20).IsRequired();
        builder.Property(t => t.NombresSugeridos).IsRequired();
        builder.HasIndex(t => t.Nombre).IsUnique();
    }
}

internal sealed class RubroConfiguracion : IEntityTypeConfiguration<Rubro>
{
    public void Configure(EntityTypeBuilder<Rubro> builder)
    {
        builder.ToTable("rubro", t =>
        {
            t.HasCheckConstraint("ck_rubro_peso", "peso BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_rubro_forma", $"forma IN ({RestriccionesEvaluacion.Formas})");
            t.HasCheckConstraint("ck_rubro_calif_minima", "calif_minima BETWEEN 0 AND 10");
        });
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Nombre).HasMaxLength(Rubro.LongitudNombre).IsRequired();
        builder.Property(r => r.Peso).HasPrecision(5, 2);
        builder.Property(r => r.Forma).HasMaxLength(20).IsRequired();
        builder.Property(r => r.CalifMinima).HasPrecision(4, 2);
        builder.HasIndex(r => r.PeriodoId);
        builder.HasIndex(r => r.DocenteId);
        builder.HasIndex(r => r.TipoRubroId);
        builder.HasOne<Periodo>().WithMany().HasForeignKey(r => r.PeriodoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<TipoRubro>().WithMany().HasForeignKey(r => r.TipoRubroId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(r => r.Criterios).WithOne().HasForeignKey(c => c.RubroId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Criterios).HasField("_criterios").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CriterioConfiguracion : IEntityTypeConfiguration<Criterio>
{
    public void Configure(EntityTypeBuilder<Criterio> builder)
    {
        builder.ToTable("criterio", t =>
        {
            t.HasCheckConstraint("ck_criterio_peso", "peso BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_criterio_forma", $"forma IN ({RestriccionesEvaluacion.Formas})");
            t.HasCheckConstraint("ck_criterio_calif_minima", "calif_minima BETWEEN 0 AND 10");
        });
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Nombre).HasMaxLength(Criterio.LongitudNombre).IsRequired();
        builder.Property(c => c.Peso).HasPrecision(5, 2);
        builder.Property(c => c.Forma).HasMaxLength(20).IsRequired();
        builder.Property(c => c.CalifMinima).HasPrecision(4, 2);
        builder.HasIndex(c => c.RubroId);
        builder.HasIndex(c => c.DocenteId);
    }
}

internal sealed class ActividadConfiguracion : IEntityTypeConfiguration<Actividad>
{
    public void Configure(EntityTypeBuilder<Actividad> builder)
    {
        builder.ToTable("actividad", t => t.HasCheckConstraint("ck_actividad_modo", "modo IN ('individual', 'por_equipo')"));
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Nombre).HasMaxLength(Actividad.LongitudNombre).IsRequired();
        builder.Property(a => a.Modo).HasMaxLength(20).IsRequired();
        builder.HasIndex(a => a.RubroId);
        builder.HasIndex(a => a.DocenteId);
        builder.HasOne<Rubro>().WithMany().HasForeignKey(a => a.RubroId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CalificacionConfiguracion : IEntityTypeConfiguration<Calificacion>
{
    public void Configure(EntityTypeBuilder<Calificacion> builder)
    {
        builder.ToTable("calificacion", t =>
        {
            t.HasCheckConstraint("ck_calificacion_valor", "valor IS NULL OR valor BETWEEN 0 AND 10");
            t.HasCheckConstraint("ck_calificacion_marca", $"marca IN ({RestriccionesEvaluacion.Marcas})");
            t.HasCheckConstraint("ck_calificacion_origen", "origen IN ('individual', 'equipo', 'ajustada')");
        });
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Valor).HasPrecision(4, 2);
        builder.Property(c => c.Marca).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Origen).HasMaxLength(20).IsRequired();
        builder.Ignore(c => c.EstaVacia);
        builder.HasIndex(c => new { c.ActividadId, c.AlumnoId }).IsUnique();
        builder.HasIndex(c => c.AlumnoId);
        builder.HasIndex(c => c.DocenteId);
        builder.HasOne<Actividad>().WithMany().HasForeignKey(c => c.ActividadId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Alumno>().WithMany().HasForeignKey(c => c.AlumnoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.Criterios).WithOne().HasForeignKey(d => d.CalificacionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Criterios).HasField("_criterios").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CalificacionCriterioConfiguracion : IEntityTypeConfiguration<CalificacionCriterio>
{
    public void Configure(EntityTypeBuilder<CalificacionCriterio> builder)
    {
        builder.ToTable("calificacion_criterio", t =>
        {
            t.HasCheckConstraint("ck_calificacion_criterio_valor", "valor IS NULL OR valor BETWEEN 0 AND 10");
            t.HasCheckConstraint("ck_calificacion_criterio_marca", "marca IN ('ninguna', 'cumplio', 'no_cumplio')");
        });
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Valor).HasPrecision(4, 2);
        builder.Property(c => c.Marca).HasMaxLength(20).IsRequired();
        builder.HasIndex(c => new { c.CalificacionId, c.CriterioId }).IsUnique();
        builder.HasIndex(c => c.CriterioId);
        builder.HasIndex(c => c.DocenteId);
        builder.HasOne<Criterio>().WithMany().HasForeignKey(c => c.CriterioId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CambioSincronizadoConfiguracion : IEntityTypeConfiguration<CambioSincronizado>
{
    public void Configure(EntityTypeBuilder<CambioSincronizado> builder)
    {
        builder.ToTable("cambio_sincronizado");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.HasIndex(c => c.DocenteId);
        builder.HasOne<Docente>().WithMany().HasForeignKey(c => c.DocenteId).OnDelete(DeleteBehavior.Cascade);
    }
}
