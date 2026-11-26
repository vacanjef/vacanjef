using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VacanJef.Core.Entities;

namespace VacanJef.Infrastructure.Data.Configurations;

public class NotificacionConfiguration : IEntityTypeConfiguration<Notificacion>
{
    public const int MaximoTitulo = 120;
    public const int MaximoMensaje = 400;
    public const int MaximoEnlace = 200;

    public void Configure(EntityTypeBuilder<Notificacion> b)
    {
        b.ToTable("Notificaciones");
        b.Property(n => n.Evento).HasMaxLength(60).IsRequired();
        b.Property(n => n.Canal).HasConversion<string>().HasMaxLength(12);
        b.Property(n => n.Estado).HasConversion<string>().HasMaxLength(10);

        // HU-009
        b.Property(n => n.Titulo).HasMaxLength(MaximoTitulo).IsRequired();
        b.Property(n => n.Mensaje).HasMaxLength(MaximoMensaje).IsRequired();
        b.Property(n => n.Enlace).HasMaxLength(MaximoEnlace);

        b.HasOne(n => n.Usuario).WithMany()
            .HasForeignKey(n => n.UsuarioId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(n => new { n.UsuarioId, n.EnviadaEn }); // IDX-006
        b.HasIndex(n => new { n.UsuarioId, n.LeidaEn, n.CreadaEn }); // HU-009: lista y contador de no leídas
    }
}
