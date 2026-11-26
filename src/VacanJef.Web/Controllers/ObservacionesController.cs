using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VacanJef.Core.Constantes;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Models.Observaciones;
using VacanJef.Web.Servicios;

namespace VacanJef.Web.Controllers;

// HU-008 / RF-009: el cuidador (y el administrador) registra observaciones durante una estadía activa.
// CA-HU-008-05: un dueño o un visitante no tiene acceso. La consulta del dueño es HU-013.
[Authorize(Roles = Roles.Cuidador + "," + Roles.Administrador)]
public class ObservacionesController(
    VacanJefDbContext contexto,
    ILogger<ObservacionesController> logger) : Controller
{
    private ServicioObservaciones Servicio => new(contexto);

    private bool EsAdministrador => User.IsInRole(Roles.Administrador);

    [HttpGet]
    public async Task<IActionResult> Index() => View(await Servicio.EstadiasActivasAsync());

    [HttpGet]
    public async Task<IActionResult> Estadia(int id, int? corrigeA)
    {
        var modelo = await Servicio.EstadiaAsync(id, EsAdministrador);
        if (modelo is null)
            return NotFound();

        if (modelo.PermiteRegistrar && corrigeA is int originalId &&
            modelo.Observaciones.FirstOrDefault(o => o.Id == originalId) is { } original)
        {
            modelo.Corrigiendo = original;
            modelo.Nueva = new NuevaObservacionViewModel { Tipo = original.Tipo, CorrigeAId = original.Id };
        }

        return View(modelo);
    }

    [HttpPost]
    public async Task<IActionResult> Estadia(
        int id,
        [Bind(Prefix = nameof(EstadiaDetalleViewModel.Nueva))] NuevaObservacionViewModel nueva,
        [FromServices] ServicioNotificaciones? notificaciones = null)
    {
        if (ObtenerUsuarioId() is not int autorId)
            return Challenge();

        if (ModelState.IsValid)
        {
            var resultado = await Servicio.RegistrarAsync(id, autorId, nueva.Tipo!.Value, nueva.Detalle, nueva.CorrigeAId);

            if (resultado.ReservaNoEncontrada)
                return NotFound();

            if (resultado.Exitoso)
            {
                // En el log solo van identificadores, nunca el texto de la observación
                logger.LogInformation(
                    "Observación registrada. ObservacionId={ObservacionId} ReservaId={ReservaId} AutorId={AutorId} Correccion={Correccion}",
                    resultado.Observacion!.Id, id, autorId, nueva.CorrigeAId is not null);

                // HU-009: avisa al dueño de la mascota
                if (notificaciones is not null)
                    await notificaciones.NotificarObservacionAsync(resultado.Observacion!.Id);

                TempData["Mensaje"] = nueva.CorrigeAId is null
                    ? "Se registró la observación."
                    : "Se registró la corrección. La observación original se conserva en el historial.";
                return RedirectToAction(nameof(Estadia), new { id });
            }

            var clave = resultado.Campo.Length == 0
                ? string.Empty
                : $"{nameof(EstadiaDetalleViewModel.Nueva)}.{resultado.Campo}";
            ModelState.AddModelError(clave, resultado.Mensaje);
        }

        var modelo = await Servicio.EstadiaAsync(id, EsAdministrador);
        if (modelo is null)
            return NotFound();

        modelo.Nueva = nueva;
        if (nueva.CorrigeAId is int corrigeAId)
            modelo.Corrigiendo = modelo.Observaciones.FirstOrDefault(o => o.Id == corrigeAId);

        return View(modelo);
    }

    private int? ObtenerUsuarioId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
