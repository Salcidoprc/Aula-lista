using System.Text.Json;
using AulaLista.Domain.Boletas;
using AulaLista.Domain.Grupos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AulaLista.Infrastructure.Persistencia.Configuraciones;

internal sealed class BoletaConfiguracion : IEntityTypeConfiguration<Boleta>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<Boleta> builder)
    {
        builder.ToTable("boleta", t =>
        {
            t.HasCheckConstraint("ck_boleta_version", "version >= 1");
            t.HasCheckConstraint("ck_boleta_reemplazada", "reemplazada_por IS NULL OR publicada_en IS NOT NULL");
        });
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.Codigo).HasMaxLength(Boleta.LongitudCodigo).IsFixedLength().IsRequired();
        builder.Property(b => b.PdfRuta).HasMaxLength(300);

        // RN-051: la foto completa en jsonb; se compara por contenido para que EF note cambios en la lista de rubros.
        builder.Property(b => b.Datos)
            .HasColumnType("jsonb")
            .HasConversion(
                d => JsonSerializer.Serialize(d, Json),
                s => JsonSerializer.Deserialize<DatosBoleta>(s, Json)!,
                new ValueComparer<DatosBoleta>(
                    (a, b) => a == null ? b == null : b != null && a.MismoContenido(b),
                    d => d.Alumno.GetHashCode(StringComparison.Ordinal),
                    d => JsonSerializer.Deserialize<DatosBoleta>(JsonSerializer.Serialize(d, Json), Json)!))
            .IsRequired();
        builder.Ignore(b => b.EstaPublicada);
        builder.Ignore(b => b.EsVigente);
        builder.Ignore(b => b.EsCorreccion);

        builder.HasIndex(b => new { b.AlumnoId, b.PeriodoId, b.Version }).IsUnique(); // reglas/03: versiones únicas
        builder.HasIndex(b => b.PeriodoId);
        builder.HasIndex(b => b.Codigo).IsUnique();
        builder.HasOne<Alumno>().WithMany().HasForeignKey(b => b.AlumnoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Periodo>().WithMany().HasForeignKey(b => b.PeriodoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Boleta>().WithMany().HasForeignKey(b => b.ReemplazadaPor).OnDelete(DeleteBehavior.Restrict);
    }
}
