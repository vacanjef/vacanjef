using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VacanJef.Core.Constantes;
using VacanJef.Core.Entities;
using VacanJef.Core.Enums;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Models.Mascotas;
using VacanJef.Web.Servicios;

namespace VacanJef.Web.Controllers;

// HU-003 y HU-004: el dueño registra sus mascotas, con salud y foto.
// Todas las consultas se filtran por el dueño de la sesión: nadie ve mascotas ajenas.
[Authorize(Roles = Roles.Dueno)]
public class MascotasController(
    VacanJefDbContext contexto,
    AlmacenFotosMascotas almacenFotos,
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
                m.RegistrosSalud.Count(r => r.Tipo == TipoRegistroSalud.Medicamento),
                m.FotoUrl != null))
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

        var extension = await ValidarFotoAsync(modelo.Foto, nameof(modelo.Foto));

        if (!ModelState.IsValid)
        {
            if (modelo.RegistrosSalud.Count == 0)
                modelo.RegistrosSalud.Add(new RegistroSaludItemViewModel());
            return View(modelo);
        }

        var mascota = modelo.CrearMascota(duenoId);
        if (modelo.Foto is not null)
            mascota.FotoUrl = await almacenFotos.GuardarAsync(modelo.Foto, extension!);

        contexto.Set<Mascota>().Add(mascota);
        await GuardarOEliminarFotoAsync(mascota.FotoUrl);

        // En el log solo van identificadores
        logger.LogInformation("Mascota registrada. MascotaId={MascotaId} DuenoId={DuenoId}", mascota.Id, duenoId);

        TempData["Mensaje"] = $"Se registró a {mascota.Nombre}.";
        return RedirectToAction(nameof(Index));
    }

    // HU-004: la foto solo se entrega al dueño de la mascota
    [HttpGet]
    public async Task<IActionResult> Foto(int id)
    {
        if (ObtenerDuenoId() is not int duenoId)
            return Challenge();

        var nombre = await contexto.Set<Mascota>()
            .AsNoTracking()
            .Where(m => m.Id == id && m.DuenoId == duenoId && m.Activo)
            .Select(m => m.FotoUrl)
            .FirstOrDefaultAsync();

        if (almacenFotos.RutaCompleta(nombre) is not string ruta)
            return NotFound();

        Response.Headers.CacheControl = "private, no-cache";
        return PhysicalFile(ruta, AlmacenFotosMascotas.TipoContenido(ruta));
    }

    [HttpGet]
    public async Task<IActionResult> CambiarFoto(int id)
    {
        if (ObtenerDuenoId() is not int duenoId)
            return Challenge();

        var mascota = await BuscarMascotaAsync(id, duenoId);
        if (mascota is null)
            return NotFound();

        return View(new CambiarFotoViewModel
        {
            Id = mascota.Id,
            Nombre = mascota.Nombre,
            TieneFoto = mascota.FotoUrl is not null,
        });
    }

    [HttpPost]
    public async Task<IActionResult> CambiarFoto(int id, CambiarFotoViewModel modelo)
    {
        if (ObtenerDuenoId() is not int duenoId)
            return Challenge();

        var mascota = await BuscarMascotaAsync(id, duenoId);
        if (mascota is null)
            return NotFound();

        if (modelo.Foto is null)
            ModelState.AddModelError(nameof(modelo.Foto), "Seleccione una foto.");

        var extension = await ValidarFotoAsync(modelo.Foto, nameof(modelo.Foto));

        if (!ModelState.IsValid)
        {
            modelo.Id = mascota.Id;
            modelo.Nombre = mascota.Nombre;
            modelo.TieneFoto = mascota.FotoUrl is not null;
            return View(modelo);
        }

        var anterior = mascota.FotoUrl;
        mascota.FotoUrl = await almacenFotos.GuardarAsync(modelo.Foto!, extension!);
        await GuardarOEliminarFotoAsync(mascota.FotoUrl);
        almacenFotos.Eliminar(anterior);

        logger.LogInformation("Foto de mascota actualizada. MascotaId={MascotaId} DuenoId={DuenoId}", mascota.Id, duenoId);

        TempData["Mensaje"] = $"Se actualizó la foto de {mascota.Nombre}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<string?> ValidarFotoAsync(IFormFile? foto, string campo)
    {
        if (foto is null)
            return null;

        var (extension, error) = await AlmacenFotosMascotas.ValidarAsync(foto);
        if (error is not null)
            ModelState.AddModelError(campo, error);

        return extension;
    }

    // Si la base de datos falla, no deja la foto nueva huérfana en el disco
    private async Task GuardarOEliminarFotoAsync(string? fotoNueva)
    {
        try
        {
            await contexto.SaveChangesAsync();
        }
        catch
        {
            almacenFotos.Eliminar(fotoNueva);
            throw;
        }
    }

    private Task<Mascota?> BuscarMascotaAsync(int id, int duenoId) =>
        contexto.Set<Mascota>().FirstOrDefaultAsync(m => m.Id == id && m.DuenoId == duenoId && m.Activo);

    private int? ObtenerDuenoId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
