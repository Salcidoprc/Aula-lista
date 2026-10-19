using AulaLista.Domain.Cuentas;
using AulaLista.Domain.Operacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AulaLista.Infrastructure.Persistencia.Configuraciones;

internal sealed class CuentaConfiguracion : IEntityTypeConfiguration<Cuenta>
{
    public void Configure(EntityTypeBuilder<Cuenta> builder)
    {
        builder.ToTable("cuenta", t =>
        {
            t.HasCheckConstraint("ck_cuenta_estado", "estado IN ('activa', 'eliminacion_solicitada')");
            t.HasCheckConstraint("ck_cuenta_rol", "rol IN ('docente', 'admin')");
        });
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Correo).HasMaxLength(254).IsRequired();
        builder.HasIndex(c => c.Correo).IsUnique();
        builder.Property(c => c.HashContrasena).HasMaxLength(200);
        builder.Property(c => c.GoogleSub).HasMaxLength(64);
        builder.HasIndex(c => c.GoogleSub).IsUnique();
        builder.Property(c => c.Estado).HasMaxLength(30).IsRequired();
        builder.Property(c => c.Rol).HasMaxLength(20).IsRequired().HasDefaultValue(RolCuenta.Docente);
        builder.Ignore(c => c.EsAdmin);
        builder.Ignore(c => c.EstaActiva);
        builder.Ignore(c => c.CorreoVerificado);
    }
}

internal sealed class TokenCuentaConfiguracion : IEntityTypeConfiguration<TokenCuenta>
{
    public void Configure(EntityTypeBuilder<TokenCuenta> builder)
    {
        builder.ToTable("token_cuenta", t => t.HasCheckConstraint(
            "ck_token_cuenta_proposito", "proposito IN ('verificar_correo', 'restablecer_contrasena')"));
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Proposito).HasMaxLength(30).IsRequired();
        builder.Property(t => t.Hash).HasMaxLength(64).IsRequired();
        builder.HasIndex(t => t.Hash).IsUnique();
        builder.HasIndex(t => new { t.CuentaId, t.Proposito, t.CreatedAt });
        builder.HasOne<Cuenta>().WithMany().HasForeignKey(t => t.CuentaId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SesionConfiguracion : IEntityTypeConfiguration<Sesion>
{
    public void Configure(EntityTypeBuilder<Sesion> builder)
    {
        builder.ToTable("sesion", t => t.HasCheckConstraint("ck_sesion_metodo", "metodo IN ('contrasena', 'google')"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Hash).HasMaxLength(64).IsRequired();
        builder.HasIndex(s => s.Hash).IsUnique();
        builder.HasIndex(s => s.CuentaId);
        builder.HasIndex(s => s.FamiliaId);
        builder.Property(s => s.Metodo).HasMaxLength(20).IsRequired();
        builder.HasOne<Cuenta>().WithMany().HasForeignKey(s => s.CuentaId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DocenteConfiguracion : IEntityTypeConfiguration<Docente>
{
    public void Configure(EntityTypeBuilder<Docente> builder)
    {
        builder.ToTable("docente", t => t.HasCheckConstraint("ck_docente_nivel", "nivel IS NULL OR nivel IN ('primaria', 'secundaria')"));
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Nombre).HasMaxLength(120);
        builder.Property(d => d.Nivel).HasMaxLength(20);
        builder.Property(d => d.EscuelaNombre).HasMaxLength(160);
        builder.Property(d => d.EscuelaCct).HasMaxLength(10);
        builder.Property(d => d.EntidadFederativa).HasMaxLength(40);
        builder.Property(d => d.ZonaHoraria).HasMaxLength(64).IsRequired();
        builder.Ignore(d => d.PerfilCompleto);
        builder.HasOne<Cuenta>().WithOne().HasForeignKey<Docente>(d => d.Id).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AceptacionLegalConfiguracion : IEntityTypeConfiguration<AceptacionLegal>
{
    public void Configure(EntityTypeBuilder<AceptacionLegal> builder)
    {
        builder.ToTable("aceptacion_legal", t => t.HasCheckConstraint(
            "ck_aceptacion_legal_documento", "documento IN ('terminos', 'privacidad', 'declaracion_datos')"));
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Documento).HasMaxLength(30).IsRequired();
        builder.Property(a => a.Version).HasMaxLength(20).IsRequired();
        builder.HasIndex(a => new { a.DocenteId, a.Documento, a.Version }).IsUnique();
        builder.HasOne<Docente>().WithMany().HasForeignKey(a => a.DocenteId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ExportacionConfiguracion : IEntityTypeConfiguration<Exportacion>
{
    public void Configure(EntityTypeBuilder<Exportacion> builder)
    {
        builder.ToTable("exportacion", t => t.HasCheckConstraint(
            "ck_exportacion_estado", "estado IN ('pendiente', 'lista', 'fallida')"));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Estado).HasMaxLength(20).IsRequired();
        builder.Property(e => e.RutaExcel).HasMaxLength(300);
        builder.Property(e => e.RutaPdf).HasMaxLength(300);
        builder.HasIndex(e => e.DocenteId);
        builder.HasOne<Docente>().WithMany().HasForeignKey(e => e.DocenteId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RegistroAuditoriaConfiguracion : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> builder)
    {
        builder.ToTable("auditoria");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.ActorTipo).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Accion).HasMaxLength(60).IsRequired();
        builder.Property(a => a.Entidad).HasMaxLength(40).IsRequired();
        builder.Property(a => a.Ip).HasMaxLength(45);
        builder.HasIndex(a => new { a.ActorId, a.Fecha });
    }
}
