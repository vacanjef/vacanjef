using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VacanJef.Core.Entities;

namespace VacanJef.Infrastructure.Data.Configurations;

public class CamaraConfiguration : IEntityTypeConfiguration<Camara>
{
    public void Configure(EntityTypeBuilder<Camara> b)
    {
        b.ToTable("Camaras");
        b.Property(c => c.Nombre).HasMaxLength(80).IsRequired();
        b.Property(c => c.UrlStream).HasMaxLength(255).IsRequired();
    }
}
