using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VacanJef.Infrastructure.Data;

namespace VacanJef.Infrastructure;

// Uso en Program.cs: builder.Services.AddInfrastructure(cadenaDeConexion);
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string cadenaConexion)
    {
        services.AddDbContext<VacanJefDbContext>(o => o.UseSqlServer(cadenaConexion));
        return services;
    }
}
