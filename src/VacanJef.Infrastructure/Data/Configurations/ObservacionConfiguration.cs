using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VacanJef.Core.Entities;

namespace VacanJef.Infrastructure.Data.Configurations;

public class ObservacionConfiguration : IEntityTypeConfiguration<Observacion>
{
    public void Configure(EntityTypeBuilder<Observacion> b)
    {
        b.ToTable("Observaciones");
        b.Property(o => o.Tipo).HasConversion<string>().HasMaxLength(15);
        b.Property(o => o.Detalle).IsRequired();

        b.HasOne(o => o.Reserva).WithMany(r => r.Observaciones)
            .HasForeignKey(o => o.ReservaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(o => o.Autor).WithMany()
            .HasForeignKey(o => o.AutorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(o => o.CorrigeA).WithMany(o => o.Correcciones)
            .HasForeignKey(o => o.CorrigeAId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(o => new { o.ReservaId, o.CreadoEn }); // IDX-005
    }
}
