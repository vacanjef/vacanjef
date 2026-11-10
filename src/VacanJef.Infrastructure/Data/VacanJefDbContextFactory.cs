using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VacanJef.Infrastructure.Data;

// Lo usan los comandos "dotnet ef" para crear migraciones sin arrancar la web.
// La cadena se toma de la variable de entorno VACANJEF_DB (nunca se escribe en el código).
public class VacanJefDbContextFactory : IDesignTimeDbContextFactory<VacanJefDbContext>
{
    public VacanJefDbContext CreateDbContext(string[] args)
    {
        var cadena = Environment.GetEnvironmentVariable("VACANJEF_DB")
            ?? "Server=localhost;Database=VacanJef;Trusted_Connection=True;TrustServerCertificate=True";

        var opciones = new DbContextOptionsBuilder<VacanJefDbContext>()
            .UseSqlServer(cadena)
            .Options;

        return new VacanJefDbContext(opciones);
    }
}
