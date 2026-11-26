using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VacanJef.Web.Servicios;

namespace VacanJef.Web.Controllers;

// HU-009 / RF-010: cada usuario consulta sus notificaciones y las marca como leídas.
// CA-HU-009-04: un visitante es enviado a iniciar sesión; nadie ve las notificaciones de otro.
[Authorize]
public class NotificacionesController(ServicioNotificaciones servicio) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (ObtenerUsuarioId() is not int usuarioId)
            return Challenge();

        return View(await servicio.ListarAsync(usuarioId));
    }

    // Marca la notificación como leída y lleva a la página relacionada (solo rutas del sitio)
    [HttpPost]
    public async Task<IActionResult> Abrir(int id)
    {
        if (ObtenerUsuarioId() is not int usuarioId)
            return Challenge();

        var (encontrada, enlace) = await servicio.MarcarLeidaAsync(usuarioId, id);
        if (!encontrada)
            return NotFound();

        return ServicioNotificaciones.EsRutaLocal(enlace)
            ? LocalRedirect(enlace!)
            : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> MarcarTodas()
    {
        if (ObtenerUsuarioId() is not int usuarioId)
            return Challenge();

        var marcadas = await servicio.MarcarTodasAsync(usuarioId);
        TempData["Mensaje"] = marcadas == 0
            ? "No tenía notificaciones pendientes."
            : marcadas == 1 ? "Se marcó 1 notificación como leída." : $"Se marcaron {marcadas} notificaciones como leídas.";

        return RedirectToAction(nameof(Index));
    }

    private int? ObtenerUsuarioId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
