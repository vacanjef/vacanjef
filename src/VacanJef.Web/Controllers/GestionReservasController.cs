using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VacanJef.Core.Constantes;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Models.GestionReservas;
using VacanJef.Web.Models.Reservas;
using VacanJef.Web.Servicios;

namespace VacanJef.Web.Controllers;

// HU-006 / RF-008: gestión de reservas del administrador (CA-HU-006-05: solo Administrador).
// La página del dueño sigue siendo /Reservas (HU-005).
[Authorize(Roles = Roles.Administrador)]
public class GestionReservasController(
    VacanJefDbContext contexto,
    IConfiguration configuracion,
    ILogger<GestionReservasController> logger) : Controller
{
    private ServicioGestionReservas Servicio => new(contexto);

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] FiltroReservas filtro)
    {
        var capacidad = OpcionesReservas.Desde(configuracion).CapacidadDiaria;
        var (reservas, hayMas) = await Servicio.ListarAsync(filtro);

        return View(new GestionReservasViewModel
        {
            Filtro = filtro,
            Reservas = reservas,
            HayMas = hayMas,
            Ocupacion = await Servicio.OcupacionAsync(ServicioReservas.HoyEnColombia(), capacidad),
        });
    }

    // Página de confirmación (sin JavaScript: la CSP solo permite 'self')
    [HttpGet]
    public async Task<IActionResult> Cancelar(int id)
    {
        var reserva = await Servicio.ObtenerAsync(id);
        return reserva is null ? NotFound() : View(reserva);
    }

    [HttpPost]
    [ActionName(nameof(Cancelar))]
    public async Task<IActionResult> CancelarConfirmado(int id, [FromServices] ServicioNotificaciones? notificaciones = null)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var administradorId))
            return Challenge();

        var resultado = await Servicio.CancelarAsync(id, administradorId);
        if (resultado.ReservaNoEncontrada)
            return NotFound();

        if (!resultado.Exitoso)
        {
            TempData["Error"] = resultado.Mensaje;
            return RedirectToAction(nameof(Index));
        }

        logger.LogInformation("Reserva cancelada por el administrador. ReservaId={ReservaId} AdministradorId={AdministradorId}",
            id, administradorId);

        // HU-009: un fallo al notificar no deshace la cancelación
        if (notificaciones is not null)
        {
            try
            {
                await notificaciones.NotificarAsync(
                    resultado.DuenoId,
                    EventosNotificacion.ReservaCancelada,
                    "Reserva cancelada",
                    $"La reserva de {resultado.Mascota} del {PresentacionReservas.Fecha(resultado.FechaIngreso)} al {PresentacionReservas.Fecha(resultado.FechaSalida)} fue cancelada por la guardería.",
                    "/Reservas");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "No se pudo notificar la cancelación. ReservaId={ReservaId}", id);
            }
        }

        TempData["Mensaje"] = $"Se canceló la reserva #{id}. El cupo quedó libre y se avisó al dueño.";
        return RedirectToAction(nameof(Index));
    }
}
