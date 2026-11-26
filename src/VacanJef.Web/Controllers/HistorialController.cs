using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VacanJef.Core.Constantes;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Servicios;

namespace VacanJef.Web.Controllers;

// HU-013: el dueño consulta el historial de salud y las observaciones de SU mascota.
// El cuidador y el administrador consultan desde /Observaciones (HU-008).
[Authorize(Roles = Roles.Dueno)]
public class HistorialController(VacanJefDbContext contexto) : Controller
{
    [HttpGet("Mascotas/{id:int}/Historial")]
    public async Task<IActionResult> Index(int id)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var duenoId))
            return Challenge();

        // CA-HU-013-04: la mascota de otro dueño responde igual que una que no existe
        var historial = await new ServicioHistorialMascota(contexto).HistorialAsync(id, duenoId);
        return historial is null ? NotFound() : View(historial);
    }
}
