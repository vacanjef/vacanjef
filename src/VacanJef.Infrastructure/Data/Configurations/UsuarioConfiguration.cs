using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VacanJef.Core.Entities;

namespace VacanJef.Infrastructure.Data.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("Usuarios");
        b.Property(u => u.Nombre).HasMaxLength(100).IsRequired();
        b.Property(u => u.Email).HasMaxLength(150);
        b.Property(u => u.PhoneNumber).HasMaxLength(20);
        b.HasIndex(u => u.NormalizedEmail).IsUnique(); // IDX-001: correo único
    }
}
