using AulaLista.Domain.Grupos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AulaLista.Infrastructure.Persistencia.Configuraciones;

internal sealed class CicloEscolarConfiguracion : IEntityTypeConfiguration<CicloEscolar>
{
    public void Configure(EntityTypeBuilder<CicloEscolar> builder)
    {
        builder.ToTable("ciclo_escolar");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Nombre).HasMaxLength(20).IsRequired();
        builder.HasIndex(c => c.Nombre).IsUnique();
    }
}
