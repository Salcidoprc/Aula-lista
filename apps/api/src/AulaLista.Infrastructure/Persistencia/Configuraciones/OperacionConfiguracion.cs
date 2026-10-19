using AulaLista.Domain.Operacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AulaLista.Infrastructure.Persistencia.Configuraciones;

internal sealed class RegistroAuditoriaAdminConfiguracion : IEntityTypeConfiguration<RegistroAuditoriaAdmin>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoriaAdmin> builder)
    {
        builder.ToTable("auditoria_admin");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Accion).HasMaxLength(60).IsRequired();
        builder.Property(a => a.Entidad).HasMaxLength(40).IsRequired();
        builder.Property(a => a.Antes).HasColumnType("jsonb");
        builder.Property(a => a.Despues).HasColumnType("jsonb");
        builder.HasIndex(a => a.Fecha);
    }
}
