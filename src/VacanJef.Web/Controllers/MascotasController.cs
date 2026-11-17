using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VacanJef.Core.Constantes;
using VacanJef.Core.Entities;
using VacanJef.Core.Enums;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Models.Mascotas;

namespace VacanJef.Web.Controllers;

// HU-003: el dueño registra y consulta sus mascotas.
// Todas las consultas se filtran por el dueño de la sesión: nadie ve mascotas ajenas.
[Authorize(Roles = Roles.Dueno)]
public class MascotasController(
    VacanJefDbContext contexto,
    ILogger<MascotasController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (ObtenerDuenoId() is not int duenoId)
            return Challenge();

        var mascotas = await contexto.Set<Mascota>()
            .AsNoTracking()
            .Where(m => m.DuenoId == duenoId && m.Activo)
            .OrderBy(m => m.Nombre)
            .Select(m => new MascotaResumenViewModel(
                m.Id, m.Nombre, m.Raza, m.Edad, m.PesoKg, m.Sexo,
                m.RegistrosSalud.Count(r => r.Tipo == TipoRegistroSalud.Vacuna),
                m.RegistrosSalud.Count(r => r.Tipo == TipoRegistroSalud.Alergia),
                m.RegistrosSalud.Count(r => r.Tipo == TipoRegistroSalud.Medicamento)))
            .ToListAsync();

        return View(mascotas);
    }

    [HttpGet]
    public IActionResult Registrar() => View(new RegistroMascotaViewModel
    {
        RegistrosSalud = [new RegistroSaludItemViewModel { Tipo = TipoRegistroSalud.Vacuna }],
    });

    [HttpPost]
    public async Task<IActionResult> Registrar(RegistroMascotaViewModel modelo)
    {
        if (ObtenerDuenoId() is not int duenoId)
            return Challenge();

        if (!ModelState.IsValid)
        {
            if (modelo.RegistrosSalud.Count == 0)
                modelo.RegistrosSalud.Add(new RegistroSaludItemViewModel());
            return View(modelo);
        }

        var mascota = modelo.CrearMascota(duenoId);
        contexto.Set<Mascota>().Add(mascota);
        await contexto.SaveChangesAsync();

        // En el log solo van identificadores
        logger.LogInformation("Mascota registrada. MascotaId={MascotaId} DuenoId={DuenoId}", mascota.Id, duenoId);

        TempData["Mensaje"] = $"Se registró a {mascota.Nombre}.";
        return RedirectToAction(nameof(Index));
    }

    private int? ObtenerDuenoId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
