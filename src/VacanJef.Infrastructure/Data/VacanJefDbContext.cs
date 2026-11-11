using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VacanJef.Core.Entities;

namespace VacanJef.Infrastructure.Data;

public class VacanJefDbContext : IdentityDbContext<Usuario, IdentityRole<int>, int>
{
    public VacanJefDbContext(DbContextOptions<VacanJefDbContext> options) : base(options) { }

    public DbSet<Mascota> Mascotas => Set<Mascota>();
    public DbSet<RegistroSalud> RegistrosSalud => Set<RegistroSalud>();
    public DbSet<Camara> Camaras => Set<Camara>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<Observacion> Observaciones => Set<Observacion>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Tablas de Identity con nombres en español
        builder.Entity<IdentityRole<int>>().ToTable("Roles");
        builder.Entity<IdentityUserRole<int>>().ToTable("UsuarioRoles");
        builder.Entity<IdentityUserClaim<int>>().ToTable("UsuarioClaims");
        builder.Entity<IdentityUserLogin<int>>().ToTable("UsuarioLogins");
        builder.Entity<IdentityUserToken<int>>().ToTable("UsuarioTokens");
        builder.Entity<IdentityRoleClaim<int>>().ToTable("RolClaims");

        builder.ApplyConfigurationsFromAssembly(typeof(VacanJefDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidarObservacionesInmodificables();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ValidarObservacionesInmodificables();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // RN-006: las observaciones no se modifican ni se eliminan
    private void ValidarObservacionesInmodificables()
    {
        var hayCambioProhibido = ChangeTracker.Entries<Observacion>()
            .Any(e => e.State is EntityState.Modified or EntityState.Deleted);

        if (hayCambioProhibido)
            throw new InvalidOperationException(
                "RN-006: las observaciones no se modifican ni se eliminan. Registre una corrección con CorrigeAId.");
    }
}
