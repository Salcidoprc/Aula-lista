using AulaLista.Domain.Avisos;
using AulaLista.Domain.Grupos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AulaLista.Infrastructure.Persistencia.Configuraciones;

internal sealed class AvisoConfiguracion : IEntityTypeConfiguration<Aviso>
{
    public void Configure(EntityTypeBuilder<Aviso> builder)
    {
        builder.ToTable("aviso");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Titulo).HasMaxLength(Aviso.LongitudTitulo).IsRequired();
        builder.Property(a => a.Cuerpo).HasMaxLength(Aviso.LongitudCuerpo).IsRequired();
        builder.HasIndex(a => new { a.GrupoId, a.CreatedAt });
        builder.HasOne<Grupo>().WithMany().HasForeignKey(a => a.GrupoId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EntregaAvisoConfiguracion : IEntityTypeConfiguration<EntregaAviso>
{
    public void Configure(EntityTypeBuilder<EntregaAviso> builder)
    {
        builder.ToTable("entrega_aviso");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Ignore(e => e.PorCompartir);
        builder.HasIndex(e => new { e.AvisoId, e.TutorId }).IsUnique();
        builder.HasIndex(e => e.TutorId);
        builder.HasOne<Aviso>().WithMany().HasForeignKey(e => e.AvisoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Tutor>().WithMany().HasForeignKey(e => e.TutorId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SuscripcionPushConfiguracion : IEntityTypeConfiguration<SuscripcionPush>
{
    public void Configure(EntityTypeBuilder<SuscripcionPush> builder)
    {
        builder.ToTable("suscripcion_push");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Endpoint).HasMaxLength(SuscripcionPush.LongitudEndpoint).IsRequired();
        builder.Property(s => s.P256dh).HasMaxLength(120).IsRequired();
        builder.Property(s => s.Auth).HasMaxLength(32).IsRequired();
        builder.HasIndex(s => new { s.TutorId, s.Endpoint }).IsUnique();
        builder.HasOne<Tutor>().WithMany().HasForeignKey(s => s.TutorId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MensajeWhatsappConfiguracion : IEntityTypeConfiguration<MensajeWhatsapp>
{
    public void Configure(EntityTypeBuilder<MensajeWhatsapp> builder)
    {
        builder.ToTable("mensaje_whatsapp");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Plantilla).HasMaxLength(40).IsRequired();
        builder.Property(m => m.Estado).HasMaxLength(20).IsRequired();
        builder.Property(m => m.IdMeta).HasMaxLength(128);
        builder.Property(m => m.CostoEstimado).HasPrecision(10, 4);
        builder.HasIndex(m => m.IdMeta).IsUnique().HasFilter("id_meta IS NOT NULL");
        builder.HasIndex(m => new { m.GrupoId, m.Plantilla, m.TutorId, m.CreatedAt });
        builder.HasIndex(m => m.ReferenciaId);
        builder.HasOne<Grupo>().WithMany().HasForeignKey(m => m.GrupoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Tutor>().WithMany().HasForeignKey(m => m.TutorId).OnDelete(DeleteBehavior.Cascade);
    }
}
