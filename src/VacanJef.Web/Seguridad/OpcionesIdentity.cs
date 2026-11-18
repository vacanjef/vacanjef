using Microsoft.AspNetCore.Identity;

namespace VacanJef.Web.Seguridad;

// Configuración de Identity en un solo lugar: la usa Program.cs y también las pruebas,
// así las pruebas verifican exactamente las reglas que corren en la aplicación.
public static class OpcionesIdentity
{
    // RN-005: bloqueo temporal tras intentos fallidos
    public const int MaximoIntentosFallidos = 5;
    public const int MinutosBloqueo = 15;

    public static void Configurar(IdentityOptions opciones)
    {
        // RNF-003: contraseñas seguras
        opciones.Password.RequiredLength = 8;
        opciones.Password.RequireDigit = true;
        opciones.Password.RequireUppercase = true;
        opciones.Password.RequireLowercase = true;
        opciones.Password.RequireNonAlphanumeric = true;

        // RN-005: bloqueo de 15 minutos tras 5 intentos fallidos
        opciones.Lockout.MaxFailedAccessAttempts = MaximoIntentosFallidos;
        opciones.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(MinutosBloqueo);
        opciones.Lockout.AllowedForNewUsers = true;

        opciones.User.RequireUniqueEmail = true;
    }
}
