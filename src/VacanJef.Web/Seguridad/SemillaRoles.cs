using Microsoft.AspNetCore.Identity;
using VacanJef.Core.Constantes;

namespace VacanJef.Web.Seguridad;

// Crea los roles del sistema si no existen. Se ejecuta al iniciar la aplicación.
public static class SemillaRoles
{
    public static async Task CrearAsync(IServiceProvider servicios)
    {
        using var alcance = servicios.CreateScope();
        var gestorRoles = alcance.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();

        foreach (var rol in new[] { Roles.Dueno, Roles.Cuidador, Roles.Administrador })
        {
            if (!await gestorRoles.RoleExistsAsync(rol))
                await gestorRoles.CreateAsync(new IdentityRole<int>(rol));
        }
    }
}
