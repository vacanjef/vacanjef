using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VacanJef.Core.Constantes;
using VacanJef.Core.Entities;
using VacanJef.Web.Models.Contrasena;
using VacanJef.Web.Models.Usuarios;
using VacanJef.Web.Servicios;

namespace VacanJef.Web.Controllers;

// HU-015: gestión de usuarios. Solo el administrador entra.
[Authorize(Roles = Roles.Administrador)]
public class UsuariosController(
    GestionUsuarios gestion,
    ServicioContrasenas contrasenas,
    UserManager<Usuario> gestorUsuarios,
    ILogger<UsuariosController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? busqueda, string? rol)
    {
        if (ObtenerAdministradorId() is not int administradorId)
            return Challenge();

        return View(new UsuariosIndexViewModel
        {
            Usuarios = await gestion.ListarAsync(busqueda, rol),
            Busqueda = busqueda,
            Rol = rol,
            AdministradorId = administradorId,
        });
    }

    [HttpGet]
    public IActionResult Crear() => View(new NuevoUsuarioViewModel { Rol = Roles.Cuidador });

    [HttpPost]
    public async Task<IActionResult> Crear(NuevoUsuarioViewModel modelo)
    {
        if (ObtenerAdministradorId() is not int administradorId)
            return Challenge();

        if (!ModelState.IsValid)
            return View(modelo);

        var resultado = await gestion.CrearAsync(
            modelo.Nombre, modelo.Correo, modelo.Telefono, modelo.Contrasena, modelo.Rol, administradorId);

        if (!resultado.Exito)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje);
            return View(modelo);
        }

        logger.LogInformation("Cuenta de personal creada por el administrador. AdministradorId={AdministradorId}", administradorId);
        TempData["Mensaje"] = resultado.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> CambiarRol(int id, string rol)
    {
        if (ObtenerAdministradorId() is not int administradorId)
            return Challenge();

        return Informar(await gestion.CambiarRolAsync(id, rol, administradorId));
    }

    [HttpPost]
    public async Task<IActionResult> CambiarEstado(int id, bool activo)
    {
        if (ObtenerAdministradorId() is not int administradorId)
            return Challenge();

        return Informar(await gestion.CambiarEstadoAsync(id, activo, administradorId));
    }

    [HttpPost]
    public async Task<IActionResult> Desbloquear(int id)
    {
        if (ObtenerAdministradorId() is not int administradorId)
            return Challenge();

        return Informar(await gestion.DesbloquearAsync(id, administradorId));
    }

    // HU-016: el administrador asigna una contraseña temporal (por ejemplo, si la persona la olvidó)
    [HttpGet]
    public async Task<IActionResult> RestablecerContrasena(int id)
    {
        if (ObtenerAdministradorId() is not int administradorId)
            return Challenge();

        if (id == administradorId)
            return Informar(ResultadoGestion.Error("Para cambiar su propia contraseña use la opción Cambiar contraseña de Mis datos."));

        var usuario = await gestorUsuarios.FindByIdAsync(id.ToString());
        if (usuario is null)
            return NotFound();

        return View(new RestablecerContrasenaViewModel { Id = usuario.Id, Nombre = usuario.Nombre });
    }

    [HttpPost]
    public async Task<IActionResult> RestablecerContrasena(int id, RestablecerContrasenaViewModel modelo)
    {
        if (ObtenerAdministradorId() is not int administradorId)
            return Challenge();

        modelo.Id = id;
        if (!ModelState.IsValid)
            return View(modelo);

        var resultado = await contrasenas.RestablecerAsync(id, modelo.NuevaContrasena, administradorId);
        if (!resultado.Exito)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje);
            return View(modelo);
        }

        logger.LogInformation("Contraseña restablecida por el administrador. AdministradorId={AdministradorId} UsuarioId={UsuarioId}",
            administradorId, id);
        return Informar(resultado);
    }

    private IActionResult Informar(ResultadoGestion resultado)
    {
        TempData[resultado.Exito ? "Mensaje" : "Error"] = resultado.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    private int? ObtenerAdministradorId() =>
        int.TryParse(gestorUsuarios.GetUserId(User), out var id) ? id : null;
}
