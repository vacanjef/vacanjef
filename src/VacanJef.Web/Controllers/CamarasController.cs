using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VacanJef.Core.Constantes;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Models.Camaras;
using VacanJef.Web.Servicios;

namespace VacanJef.Web.Controllers;

// HU-014: el administrador registra las cámaras que luego se asignan a las estadías.
// La dirección del video nunca se muestra en las vistas.
[Authorize(Roles = Roles.Administrador)]
public class CamarasController(
    VacanJefDbContext contexto,
    ILogger<CamarasController> logger) : Controller
{
    private ServicioCamaras Servicio => new(contexto);

    [HttpGet]
    public async Task<IActionResult> Index() => View(await Servicio.ListarAsync());

    [HttpGet]
    public IActionResult Crear() => View(new NuevaCamaraViewModel());

    [HttpPost]
    public async Task<IActionResult> Crear(NuevaCamaraViewModel modelo)
    {
        if (ObtenerAdministradorId() is not int administradorId)
            return Challenge();

        if (!ModelState.IsValid)
            return View(modelo);

        var resultado = await Servicio.CrearAsync(modelo.Nombre, modelo.UrlStream, administradorId);
        if (!resultado.Exito)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje);
            return View(modelo);
        }

        // Nunca se registra la dirección del video en el log
        logger.LogInformation("Cámara registrada. AdministradorId={AdministradorId}", administradorId);
        TempData["Mensaje"] = resultado.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> CambiarEstado(int id, bool activa)
    {
        if (ObtenerAdministradorId() is not int administradorId)
            return Challenge();

        var resultado = await Servicio.CambiarEstadoAsync(id, activa, administradorId);
        TempData[resultado.Exito ? "Mensaje" : "Error"] = resultado.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    private int? ObtenerAdministradorId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
