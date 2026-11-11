using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VacanJef.Core.Entities;

namespace VacanJef.Infrastructure.Data.Configurations;

public class AuditoriaConfiguration : IEntityTypeConfiguration<Auditoria>
{
    public void Configure(EntityTypeBuilder<Auditoria> b)
    {
        b.ToTable("Auditorias");
        b.Property(a => a.Accion).HasMaxLength(60).IsRequired();
        b.Property(a => a.Recurso).HasMaxLength(60).IsRequired();
        b.Property(a => a.Resultado).HasMaxLength(20).IsRequired();

        b.HasOne(a => a.Usuario).WithMany()
            .HasForeignKey(a => a.UsuarioId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(a => new { a.Recurso, a.CreadoEn }); // IDX-007
    }
}
