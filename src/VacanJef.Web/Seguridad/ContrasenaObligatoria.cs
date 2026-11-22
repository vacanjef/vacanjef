using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace VacanJef.Web.Seguridad;

// HU-016: quien entra con una contraseña asignada por el administrador debe cambiarla antes de usar el sistema.
// La marca es un claim del usuario (tabla UsuarioClaims): no requiere migración.
public static class ContrasenaObligatoria
{
    public const string TipoClaim = "vacanjef:debe-cambiar-contrasena";

    public static Claim CrearMarca() => new(TipoClaim, "si");

    public static bool DebeCambiar(ClaimsPrincipal usuario) =>
        usuario.Identity?.IsAuthenticated == true && usuario.HasClaim(c => c.Type == TipoClaim);

    // Mientras no la cambie, solo puede cambiar la contraseña, cerrar sesión o ver la página de error
    public static bool RutaPermitida(string? controlador, string? accion) =>
        string.Equals(controlador, "Contrasena", StringComparison.OrdinalIgnoreCase)
        || (string.Equals(controlador, "Cuenta", StringComparison.OrdinalIgnoreCase)
            && string.Equals(accion, "CerrarSesion", StringComparison.OrdinalIgnoreCase))
        || (string.Equals(controlador, "Home", StringComparison.OrdinalIgnoreCase)
            && string.Equals(accion, "Error", StringComparison.OrdinalIgnoreCase));
}

// Filtro global: redirige a /Contrasena/Cambiar mientras la marca exista
public class FiltroCambioContrasena : IAsyncActionFilter
{
    public Task OnActionExecutionAsync(ActionExecutingContext contexto, ActionExecutionDelegate siguiente)
    {
        var controlador = contexto.RouteData.Values["controller"]?.ToString();
        var accion = contexto.RouteData.Values["action"]?.ToString();

        if (ContrasenaObligatoria.DebeCambiar(contexto.HttpContext.User)
            && !ContrasenaObligatoria.RutaPermitida(controlador, accion))
        {
            contexto.Result = new RedirectToActionResult("Cambiar", "Contrasena", null);
            return Task.CompletedTask;
        }

        return siguiente();
    }
}
