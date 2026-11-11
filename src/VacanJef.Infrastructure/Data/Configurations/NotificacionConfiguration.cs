using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VacanJef.Core.Entities;

namespace VacanJef.Infrastructure.Data.Configurations;

public class NotificacionConfiguration : IEntityTypeConfiguration<Notificacion>
{
    public void Configure(EntityTypeBuilder<Notificacion> b)
    {
        b.ToTable("Notificaciones");
        b.Property(n => n.Evento).HasMaxLength(60).IsRequired();
        b.Property(n => n.Canal).HasConversion<string>().HasMaxLength(12);
        b.Property(n => n.Estado).HasConversion<string>().HasMaxLength(10);

        b.HasOne(n => n.Usuario).WithMany()
            .HasForeignKey(n => n.UsuarioId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(n => new { n.UsuarioId, n.EnviadaEn }); // IDX-006
    }
}
