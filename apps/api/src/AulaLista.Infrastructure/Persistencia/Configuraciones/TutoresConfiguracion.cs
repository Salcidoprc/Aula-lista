using AulaLista.Domain.Grupos;
using AulaLista.Domain.Tutores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AulaLista.Infrastructure.Persistencia.Configuraciones;

internal sealed class EnlaceTutorConfiguracion : IEntityTypeConfiguration<EnlaceTutor>
{
    public void Configure(EntityTypeBuilder<EnlaceTutor> builder)
    {
        builder.ToTable("enlace_tutor");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(e => e.TokenHash).IsUnique();
        builder.HasIndex(e => e.TutorId);

        // Sin llave foránea a alumno ni a docente: el enlace es del tutor y sobrevive a la baja del alumno o la cuenta.
        builder.HasOne<Tutor>().WithMany().HasForeignKey(e => e.TutorId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SesionTutorConfiguracion : IEntityTypeConfiguration<SesionTutor>
{
    public void Configure(EntityTypeBuilder<SesionTutor> builder)
    {
        builder.ToTable("sesion_tutor");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(s => s.TokenHash).IsUnique();
        builder.HasIndex(s => s.TutorId);
        builder.HasIndex(s => new { s.EnlaceId, s.CreatedAt });
        builder.HasOne<Tutor>().WithMany().HasForeignKey(s => s.TutorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<EnlaceTutor>().WithMany().HasForeignKey(s => s.EnlaceId).OnDelete(DeleteBehavior.Cascade);
    }
}
