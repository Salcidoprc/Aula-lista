using Aulisima.Domain.Cuentas;
using Aulisima.Domain.Planes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aulisima.Infrastructure.Persistencia.Configuraciones;

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
        builder.HasOne<PrecioProducto>().WithMany().HasForeignKey(s => s.PrecioId).OnDelete(DeleteBehavior.Restrict);
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
        builder.HasOne<PrecioProducto>().WithMany().HasForeignKey(p => p.PrecioId).OnDelete(DeleteBehavior.Restrict);
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

internal sealed class ProductoConfiguracion : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> builder)
    {
        builder.ToTable("producto", t =>
        {
            t.HasCheckConstraint("ck_producto_estado", "estado IN ('borrador', 'activo', 'archivado')");
            t.HasCheckConstraint("ck_producto_alcance", "alcance IN ('grupo')");
            t.HasCheckConstraint("ck_producto_orden", "orden BETWEEN 1 AND 999");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Nombre).HasMaxLength(Producto.LongitudNombre).IsRequired();
        builder.Property(p => p.Descripcion).HasMaxLength(Producto.LongitudDescripcion).IsRequired();
        builder.Property(p => p.Alcance).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Funciones).HasColumnType("text[]");
        builder.Property(p => p.MesesPrepago).HasColumnType("integer[]");
        builder.Property(p => p.Estado).HasMaxLength(20).IsRequired();
        builder.Property(p => p.IdExterno).HasMaxLength(100);
        builder.Property(p => p.ErrorSincronizacion).HasMaxLength(300);
        builder.Ignore(p => p.PrecioVigente);
        builder.Ignore(p => p.PrecioPendiente);
        builder.Ignore(p => p.SeVende);
        builder.Ignore(p => p.PorSincronizar);
        builder.HasMany(p => p.Precios).WithOne().HasForeignKey(v => v.ProductoId).OnDelete(DeleteBehavior.ClientCascade);
        builder.Navigation(p => p.Precios).HasField("_precios").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(p => p.IdExterno).IsUnique().HasFilter("id_externo IS NOT NULL");
    }
}

internal sealed class PrecioProductoConfiguracion : IEntityTypeConfiguration<PrecioProducto>
{
    public void Configure(EntityTypeBuilder<PrecioProducto> builder)
    {
        builder.ToTable("precio_producto", t =>
        {
            t.HasCheckConstraint("ck_precio_producto_estado", "estado IN ('pendiente', 'vigente', 'archivado')");
            t.HasCheckConstraint("ck_precio_producto_montos", "precio_primera >= 1 AND precio_adicional >= 0");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.PrecioPrimera).HasPrecision(10, 2);
        builder.Property(p => p.PrecioAdicional).HasPrecision(10, 2);
        builder.Property(p => p.Moneda).HasMaxLength(3).IsRequired();
        builder.Property(p => p.Estado).HasMaxLength(20).IsRequired();
        builder.Property(p => p.IdExternoPrimera).HasMaxLength(100);
        builder.Property(p => p.IdExternoAdicional).HasMaxLength(100);
        builder.Ignore(p => p.Sincronizado);
        builder.HasIndex(p => new { p.ProductoId, p.Version }).IsUnique();
    }
}
