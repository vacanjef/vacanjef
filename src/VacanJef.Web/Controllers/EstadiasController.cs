using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VacanJef.Core.Constantes;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Models.Estadias;
using VacanJef.Web.Servicios;

namespace VacanJef.Web.Controllers;

// HU-014 / RF-008: confirmar ingreso y salida y asignar la cámara.
// CA-HU-014-05: solo el cuidador y el administrador; un dueño recibe Acceso denegado.
[Authorize(Roles = Roles.Cuidador + "," + Roles.Administrador)]
public class EstadiasController(
    VacanJefDbContext contexto,
    ILogger<EstadiasController> logger) : Controller
{
    private ServicioEstadias Servicio => new(contexto);

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var servicio = Servicio;
        return View(new EstadiasPanelViewModel
        {
            Hoy = ServicioReservas.HoyEnColombia(),
            Llegadas = await servicio.LlegadasAsync(ServicioReservas.HoyEnColombia()),
            EnCurso = await servicio.EnCursoAsync(),
            Camaras = await servicio.CamarasDisponiblesAsync(),
        });
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmarIngreso(int id, int? camaraId)
    {
        if (ObtenerUsuarioId() is not int usuarioId)
            return Challenge();

        var resultado = await Servicio.ConfirmarIngresoAsync(id, usuarioId, camaraId, ServicioReservas.HoyEnColombia());
        return Informar(resultado, "Ingreso confirmado", id, usuarioId);
    }

    [HttpPost]
    public async Task<IActionResult> AsignarCamara(int id, int camaraId)
    {
        if (ObtenerUsuarioId() is not int usuarioId)
            return Challenge();

        var resultado = await Servicio.AsignarCamaraAsync(id, usuarioId, camaraId);
        return Informar(resultado, "Cámara asignada", id, usuarioId);
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmarSalida(int id)
    {
        if (ObtenerUsuarioId() is not int usuarioId)
            return Challenge();

        var resultado = await Servicio.ConfirmarSalidaAsync(id, usuarioId);
        return Informar(resultado, "Salida confirmada", id, usuarioId);
    }

    private IActionResult Informar(ResultadoEstadia resultado, string evento, int reservaId, int usuarioId)
    {
        if (resultado.NoEncontrada)
            return NotFound();

        if (resultado.Exitoso)
            logger.LogInformation("{Evento}. ReservaId={ReservaId} UsuarioId={UsuarioId}", evento, reservaId, usuarioId);

        TempData[resultado.Exitoso ? "Mensaje" : "Error"] = resultado.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    private int? ObtenerUsuarioId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
