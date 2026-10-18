using AulaLista.Domain.Cuentas;
using AulaLista.Domain.Planes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AulaLista.Infrastructure.Persistencia.Configuraciones;

internal sealed class SuscripcionConfiguracion : IEntityTypeConfiguration<Suscripcion>
{
    public void Configure(EntityTypeBuilder<Suscripcion> builder)
    {
        builder.ToTable("suscripcion", t =>
        {
            t.HasCheckConstraint("ck_suscripcion_estado", "estado IN ('ninguna', 'activa', 'en_gracia', 'cancelada')");
            t.HasCheckConstraint("ck_suscripcion_modalidad", "modalidad IS NULL OR modalidad IN ('tarjeta', 'prepago_oxxo')");
        });
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Proveedor).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Modalidad).HasMaxLength(20);
        builder.Property(s => s.Estado).HasMaxLength(20).IsRequired();
        builder.Property(s => s.ClienteExterno).HasMaxLength(100);
        builder.Property(s => s.IdExterno).HasMaxLength(100);
        builder.Property(s => s.GruposPro).HasColumnType("uuid[]");
        builder.Ignore(s => s.PruebaUsada);
        builder.Ignore(s => s.EsTarjetaActiva);
        builder.Ignore(s => s.EsPrepagoVigente);
        builder.HasIndex(s => s.DocenteId).IsUnique(); // una por docente
        builder.HasIndex(s => s.IdExterno).IsUnique().HasFilter("id_externo IS NOT NULL");
        builder.HasOne<Docente>().WithMany().HasForeignKey(s => s.DocenteId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PagoConfiguracion : IEntityTypeConfiguration<Pago>
{
    public void Configure(EntityTypeBuilder<Pago> builder)
    {
        builder.ToTable("pago", t =>
        {
            t.HasCheckConstraint("ck_pago_estado", "estado IN ('pendiente', 'pagado', 'vencido')");
            t.HasCheckConstraint("ck_pago_monto", "monto > 0");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Tipo).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Monto).HasPrecision(10, 2);
        builder.Property(p => p.Grupos).HasColumnType("uuid[]");
        builder.Property(p => p.SesionExterna).HasMaxLength(200).IsRequired();
        builder.Property(p => p.UrlFicha).HasMaxLength(500);
        builder.Property(p => p.Estado).HasMaxLength(20).IsRequired();
        builder.HasIndex(p => p.SesionExterna).IsUnique();
        builder.HasIndex(p => new { p.DocenteId, p.CreatedAt });
        builder.HasOne<Docente>().WithMany().HasForeignKey(p => p.DocenteId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EventoPagoConfiguracion : IEntityTypeConfiguration<EventoPago>
{
    public void Configure(EntityTypeBuilder<EventoPago> builder)
    {
        builder.ToTable("evento_pago");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.IdExterno).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Tipo).HasMaxLength(40).IsRequired();
        builder.HasIndex(e => e.IdExterno).IsUnique(); // RN-087: cada evento una vez
        builder.HasOne<Docente>().WithMany().HasForeignKey(e => e.DocenteId).OnDelete(DeleteBehavior.Cascade);
    }
}
