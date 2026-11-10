using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VacanJef.Core.Entities;

namespace VacanJef.Infrastructure.Data.Configurations;

public class MascotaConfiguration : IEntityTypeConfiguration<Mascota>
{
    public void Configure(EntityTypeBuilder<Mascota> b)
    {
        b.ToTable("Mascotas", t =>
        {
            t.HasCheckConstraint("CK_Mascotas_Edad", "[Edad] BETWEEN 0 AND 30");
            t.HasCheckConstraint("CK_Mascotas_Peso", "[PesoKg] > 0");
        });
        b.Property(m => m.Nombre).HasMaxLength(80).IsRequired();
        b.Property(m => m.Raza).HasMaxLength(80).IsRequired();
        b.Property(m => m.PesoKg).HasPrecision(5, 2);
        b.Property(m => m.Sexo).HasConversion<string>().HasMaxLength(10);
        b.Property(m => m.FotoUrl).HasMaxLength(255);

        b.HasOne(m => m.Dueno).WithMany(u => u.Mascotas)
            .HasForeignKey(m => m.DuenoId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(m => m.DuenoId); // IDX-002
    }
}
