using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VacanJef.Core.Entities;

namespace VacanJef.Infrastructure.Data.Configurations;

public class RegistroSaludConfiguration : IEntityTypeConfiguration<RegistroSalud>
{
    public void Configure(EntityTypeBuilder<RegistroSalud> b)
    {
        b.ToTable("RegistrosSalud");
        b.Property(r => r.Tipo).HasConversion<string>().HasMaxLength(15);
        b.Property(r => r.Descripcion).HasMaxLength(255).IsRequired();

        b.HasOne(r => r.Mascota).WithMany(m => m.RegistrosSalud)
            .HasForeignKey(r => r.MascotaId).OnDelete(DeleteBehavior.Restrict);
    }
}
