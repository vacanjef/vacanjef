using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VacanJef.Infrastructure.Data;

namespace VacanJef.Infrastructure;

// Uso en Program.cs: builder.Services.AddInfrastructure(cadenaDeConexion);
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string cadenaConexion)
    {
        // Azure SQL serverless se pausa cuando no se usa y puede cortar conexiones
        // momentáneamente (por ejemplo, error 40613 mientras se reanuda).
        // EnableRetryOnFailure reintenta automáticamente esos fallos transitorios.
        services.AddDbContext<VacanJefDbContext>(o => o.UseSqlServer(cadenaConexion, sql =>
            sql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null)));
        return services;
    }
}