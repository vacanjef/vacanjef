using Microsoft.AspNetCore.Identity;
using VacanJef.Core.Constantes;
using VacanJef.Core.Entities;

namespace VacanJef.Web.Seguridad;

// HU-015: crea el primer administrador al iniciar, solo si no existe ninguno.
// El correo y la contraseña vienen de variables de entorno: nunca del código.
public static class SemillaAdministrador
{
    public const string VariableCorreo = "VACANJEF_ADMIN_CORREO";
    public const string VariableContrasena = "VACANJEF_ADMIN_CONTRASENA";

    public static async Task<bool> CrearAsync(IServiceProvider servicios, IConfiguration configuracion)
    {
        var correo = configuracion[VariableCorreo]?.Trim();
        var contrasena = configuracion[VariableContrasena];
        if (string.IsNullOrEmpty(correo) || string.IsNullOrEmpty(contrasena))
            return false;

        using var alcance = servicios.CreateScope();
        var gestor = alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var logger = alcance.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(SemillaAdministrador));

        if ((await gestor.GetUsersInRoleAsync(Roles.Administrador)).Count > 0)
            return false;

        if (await gestor.FindByEmailAsync(correo) is not null)
        {
            logger.LogWarning("No se creó el administrador inicial: ya existe una cuenta con ese correo.");
            return false;
        }

        var administrador = new Usuario
        {
            UserName = correo,
            Email = correo,
            Nombre = "Administrador",
            EmailConfirmed = true,
        };

        var creacion = await gestor.CreateAsync(administrador, contrasena);
        if (!creacion.Succeeded)
        {
            logger.LogError("No se pudo crear el administrador inicial: {Errores}",
                string.Join(" ", creacion.Errors.Select(e => e.Description)));
            return false;
        }

        await gestor.AddToRoleAsync(administrador, Roles.Administrador);
        logger.LogInformation("Administrador inicial creado. UsuarioId={UsuarioId}", administrador.Id);
        return true;
    }
}
