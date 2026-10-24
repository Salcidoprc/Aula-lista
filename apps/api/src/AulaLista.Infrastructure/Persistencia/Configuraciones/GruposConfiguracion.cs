using AulaLista.Domain.Cuentas;
using AulaLista.Domain.Grupos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AulaLista.Infrastructure.Persistencia.Configuraciones;

internal sealed class CicloEscolarConfiguracion : IEntityTypeConfiguration<CicloEscolar>
{
    public void Configure(EntityTypeBuilder<CicloEscolar> builder)
    {
        builder.ToTable("ciclo_escolar", t => t.HasCheckConstraint(
            "ck_ciclo_escolar_tipo_unidad",
            "tipo_unidad IN ('bimestre', 'trimestre', 'cuatrimestre', 'semestre', 'mensual', 'personalizada')"));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Nombre).HasMaxLength(CicloEscolar.LongitudNombre).IsRequired();
        builder.Property(c => c.TipoUnidad).HasMaxLength(20).IsRequired();
        builder.HasIndex(c => new { c.DocenteId, c.Nombre }).IsUnique();
        builder.HasOne<Docente>().WithMany().HasForeignKey(c => c.DocenteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.Unidades).WithOne().HasForeignKey(u => u.CicloEscolarId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Unidades).HasField("_unidades").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class UnidadConfiguracion : IEntityTypeConfiguration<Unidad>
{
    public void Configure(EntityTypeBuilder<Unidad> builder)
    {
        builder.ToTable("unidad", t => t.HasCheckConstraint("ck_unidad_fechas", "fin >= inicio"));
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.Nombre).HasMaxLength(Unidad.LongitudNombre).IsRequired();
        builder.HasIndex(u => u.DocenteId);
    }
}

internal sealed class GrupoConfiguracion : IEntityTypeConfiguration<Grupo>
{
    public void Configure(EntityTypeBuilder<Grupo> builder)
    {
        builder.ToTable("grupo", t =>
        {
            t.HasCheckConstraint("ck_grupo_nivel", "nivel IN ('primaria', 'secundaria')");
            t.HasCheckConstraint("ck_grupo_grado", "grado BETWEEN 1 AND 6");
            t.HasCheckConstraint("ck_grupo_plan", "plan IN ('gratis', 'pro')");
            t.HasCheckConstraint("ck_grupo_calificaciones", "calif_minima BETWEEN 0 AND 10 AND calif_aprobatoria BETWEEN calif_minima AND 10");
            t.HasCheckConstraint("ck_grupo_decimales_final", "decimales_final BETWEEN 0 AND 2");
            t.HasCheckConstraint("ck_grupo_redondeo", "redondeo IN ('arriba', 'abajo', 'cercano')");
            t.HasCheckConstraint("ck_grupo_valor_retardo", "valor_retardo IN (0, 0.5, 1)");
        });
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();
        builder.Property(g => g.Nombre).HasMaxLength(Grupo.LongitudNombre).IsRequired();
        builder.Property(g => g.Nivel).HasMaxLength(20).IsRequired();
        builder.Property(g => g.Materia).HasMaxLength(Grupo.LongitudMateria);
        builder.Property(g => g.Plan).HasMaxLength(10).IsRequired();
        builder.Property(g => g.CalifMinima).HasPrecision(4, 2);
        builder.Property(g => g.CalifAprobatoria).HasPrecision(4, 2);
        builder.Property(g => g.Redondeo).HasMaxLength(10).IsRequired();
        builder.Property(g => g.ValorRetardo).HasPrecision(3, 2).HasDefaultValue(ValorRetardo.PorDefecto);
        builder.Ignore(g => g.EstaArchivado);
        builder.Ignore(g => g.Configuracion);
        builder.HasIndex(g => g.DocenteId);
        builder.HasIndex(g => g.CicloEscolarId);
        builder.HasOne<Docente>().WithMany().HasForeignKey(g => g.DocenteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<CicloEscolar>().WithMany().HasForeignKey(g => g.CicloEscolarId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PeriodoConfiguracion : IEntityTypeConfiguration<Periodo>
{
    public void Configure(EntityTypeBuilder<Periodo> builder)
    {
        builder.ToTable("periodo", t => t.HasCheckConstraint("ck_periodo_estado", "estado IN ('abierto', 'en_cierre', 'cerrado')"));
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Estado).HasMaxLength(20).IsRequired();
        builder.HasIndex(p => new { p.GrupoId, p.UnidadId }).IsUnique();
        builder.HasIndex(p => p.UnidadId);
        builder.HasIndex(p => p.DocenteId);
        builder.HasOne<Grupo>().WithMany().HasForeignKey(p => p.GrupoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Unidad>().WithMany().HasForeignKey(p => p.UnidadId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AlumnoConfiguracion : IEntityTypeConfiguration<Alumno>
{
    public void Configure(EntityTypeBuilder<Alumno> builder)
    {
        builder.ToTable("alumno", t => t.HasCheckConstraint("ck_alumno_trato", "trato IN ('neutro', 'el', 'ella')"));
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Nombres).HasMaxLength(Alumno.LongitudNombres).IsRequired();
        builder.Property(a => a.Apellidos).HasMaxLength(Alumno.LongitudApellidos).IsRequired();
        builder.Property(a => a.Trato).HasMaxLength(10).IsRequired();
        builder.Property(a => a.Seudonimo).HasMaxLength(20).IsRequired();
        builder.Ignore(a => a.EstaActivo);
        builder.HasIndex(a => a.GrupoId);
        builder.HasIndex(a => a.DocenteId);
        builder.HasIndex(a => new { a.GrupoId, a.Seudonimo }).IsUnique();
        // Número de lista único entre activos: restricción de exclusión diferible en la migración (permite intercambios).
        builder.HasOne<Grupo>().WithMany().HasForeignKey(a => a.GrupoId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TutorConfiguracion : IEntityTypeConfiguration<Tutor>
{
    public void Configure(EntityTypeBuilder<Tutor> builder)
    {
        builder.ToTable("tutor");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.TelefonoE164).HasMaxLength(16).IsRequired();
        builder.HasIndex(t => t.TelefonoE164).IsUnique();
        builder.Property(t => t.Nombre).HasMaxLength(VinculoTutor.LongitudNombre).IsRequired();
        builder.Property(t => t.Correo).HasMaxLength(VinculoTutor.LongitudCorreo);
    }
}

internal sealed class VinculoTutorConfiguracion : IEntityTypeConfiguration<VinculoTutor>
{
    public void Configure(EntityTypeBuilder<VinculoTutor> builder)
    {
        builder.ToTable("vinculo_tutor");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.TelefonoE164).HasMaxLength(16).IsRequired();
        builder.Property(v => v.Nombre).HasMaxLength(VinculoTutor.LongitudNombre).IsRequired();
        builder.Property(v => v.Correo).HasMaxLength(VinculoTutor.LongitudCorreo);
        builder.Property(v => v.Parentesco).HasMaxLength(VinculoTutor.LongitudParentesco);
        builder.HasIndex(v => new { v.AlumnoId, v.TutorId }).IsUnique();
        builder.HasIndex(v => v.TutorId);
        builder.HasIndex(v => v.DocenteId);
        builder.HasOne<Alumno>().WithMany().HasForeignKey(v => v.AlumnoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Tutor>().WithMany().HasForeignKey(v => v.TutorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ImportacionConfiguracion : IEntityTypeConfiguration<Importacion>
{
    public void Configure(EntityTypeBuilder<Importacion> builder)
    {
        builder.ToTable("importacion", t => t.HasCheckConstraint("ck_importacion_origen", "origen IN ('excel', 'csv', 'texto', 'foto')"));
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Origen).HasMaxLength(10).IsRequired();
        builder.HasIndex(i => i.GrupoId);
        builder.HasIndex(i => i.DocenteId);
        builder.HasOne<Grupo>().WithMany().HasForeignKey(i => i.GrupoId).OnDelete(DeleteBehavior.Cascade);
    }
}
