using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VacanJef.Core.Constantes;
using VacanJef.Core.Entities;
using VacanJef.Web.Models.Auditoria;
using VacanJef.Web.Servicios;

namespace VacanJef.Web.Controllers;

// HU-017: solo el administrador consulta la auditoría
[Authorize(Roles = Roles.Administrador)]
public class AuditoriaController(
    ConsultaAuditoria consulta,
    UserManager<Usuario> gestorUsuarios,
    ILogger<AuditoriaController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? accion, DateOnly? desde, DateOnly? hasta, string? texto)
    {
        var filtro = new FiltroAuditoria(accion, desde, hasta, texto);
        return View(new AuditoriaIndexViewModel
        {
            Pendientes = await consulta.SolicitudesPendientesAsync(),
            Registros = await consulta.ListarAsync(filtro),
            Filtro = filtro,
        });
    }

    [HttpPost]
    public async Task<IActionResult> AtenderSupresion(int usuarioId)
    {
        if (!int.TryParse(gestorUsuarios.GetUserId(User), out var administradorId))
            return Challenge();

        var resultado = await consulta.MarcarAtendidaAsync(usuarioId, administradorId);
        if (resultado.Exito)
            logger.LogInformation("Solicitud de supresión atendida. AdministradorId={AdministradorId} UsuarioId={UsuarioId}",
                administradorId, usuarioId);

        TempData[resultado.Exito ? "Mensaje" : "Error"] = resultado.Mensaje;
        return RedirectToAction(nameof(Index));
    }
}
