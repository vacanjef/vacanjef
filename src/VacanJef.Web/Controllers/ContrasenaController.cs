using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VacanJef.Core.Entities;
using VacanJef.Web.Models.Contrasena;
using VacanJef.Web.Seguridad;
using VacanJef.Web.Servicios;

namespace VacanJef.Web.Controllers;

// HU-016: cualquier usuario autenticado cambia su propia contraseña
[Authorize]
public class ContrasenaController(
    ServicioContrasenas servicio,
    UserManager<Usuario> gestorUsuarios,
    SignInManager<Usuario> gestorSesion,
    ILogger<ContrasenaController> logger) : Controller
{
    [HttpGet]
    public IActionResult Cambiar()
    {
        ViewData["Obligatorio"] = ContrasenaObligatoria.DebeCambiar(User);
        return View(new CambiarContrasenaViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Cambiar(CambiarContrasenaViewModel modelo)
    {
        ViewData["Obligatorio"] = ContrasenaObligatoria.DebeCambiar(User);

        var usuario = await gestorUsuarios.GetUserAsync(User);
        if (usuario is null)
            return Challenge();

        if (!ModelState.IsValid)
            return View(modelo);

        var resultado = await servicio.CambiarAsync(usuario, modelo.ContrasenaActual, modelo.NuevaContrasena);
        if (!resultado.Exito)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje);
            return View(modelo);
        }

        // Renueva la sesión actual con el sello de seguridad nuevo y sin la marca de cambio obligatorio
        await gestorSesion.RefreshSignInAsync(usuario);
        logger.LogInformation("Contraseña cambiada por el titular. UsuarioId={UsuarioId}", usuario.Id);

        TempData["Mensaje"] = resultado.Mensaje;
        return RedirectToAction("Index", "DatosPersonales");
    }
}
