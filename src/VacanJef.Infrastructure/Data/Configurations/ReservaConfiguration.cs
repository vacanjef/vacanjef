using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VacanJef.Core.Entities;

namespace VacanJef.Infrastructure.Data.Configurations;

public class ReservaConfiguration : IEntityTypeConfiguration<Reserva>
{
    public void Configure(EntityTypeBuilder<Reserva> b)
    {
        b.ToTable("Reservas", t =>
        {
            t.HasCheckConstraint("CK_Reservas_Fechas", "[FechaSalida] >= [FechaIngreso]");
            t.HasCheckConstraint("CK_Reservas_Costo", "[CostoTotal] >= 0");
        });
        b.Property(r => r.Servicio).HasConversion<string>().HasMaxLength(10);
        b.Property(r => r.Estado).HasConversion<string>().HasMaxLength(12);
        b.Property(r => r.CostoTotal).HasPrecision(10, 2);

        b.HasOne(r => r.Mascota).WithMany(m => m.Reservas)
            .HasForeignKey(r => r.MascotaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.Dueno).WithMany(u => u.Reservas)
            .HasForeignKey(r => r.DuenoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.Camara).WithMany(c => c.Reservas)
            .HasForeignKey(r => r.CamaraId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(r => new { r.FechaIngreso, r.FechaSalida }); // IDX-003: disponibilidad
        b.HasIndex(r => new { r.DuenoId, r.Estado });           // IDX-004: mis reservas
    }
}
