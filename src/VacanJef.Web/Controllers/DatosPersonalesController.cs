using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VacanJef.Core.Constantes;
using VacanJef.Core.Entities;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Models.DatosPersonales;
using VacanJef.Web.Seguridad;

namespace VacanJef.Web.Controllers;

// TT-004: el titular consulta, actualiza y pide la supresión de sus datos (Ley 1581, art. 8).
// Cualquier usuario autenticado, de cualquier rol, ve solo sus propios datos.
[Authorize]
public class DatosPersonalesController(
    UserManager<Usuario> gestorUsuarios,
    VacanJefDbContext contexto,
    ILogger<DatosPersonalesController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var usuario = await gestorUsuarios.GetUserAsync(User);
        if (usuario is null)
            return Challenge();

        var datos = new ActualizarDatosViewModel { Nombre = usuario.Nombre, Telefono = usuario.PhoneNumber ?? string.Empty };
        return View(await CrearModeloAsync(usuario, datos));
    }

    [HttpPost]
    public async Task<IActionResult> Actualizar([Bind(Prefix = "Datos")] ActualizarDatosViewModel datos)
    {
        var usuario = await gestorUsuarios.GetUserAsync(User);
        if (usuario is null)
            return Challenge();

        if (!ModelState.IsValid)
            return View(nameof(Index), await CrearModeloAsync(usuario, datos));

        usuario.Nombre = datos.Nombre.Trim();
        usuario.PhoneNumber = datos.Telefono.Trim();

        var resultado = await gestorUsuarios.UpdateAsync(usuario);
        if (!resultado.Succeeded)
        {
            foreach (var error in resultado.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(nameof(Index), await CrearModeloAsync(usuario, datos));
        }

        contexto.Auditorias.Add(PoliticaDatos.Registrar(usuario.Id, PoliticaDatos.AccionActualizacion));
        await contexto.SaveChangesAsync();

        logger.LogInformation("Datos personales actualizados. UsuarioId={UsuarioId}", usuario.Id);
        TempData["Mensaje"] = "Sus datos se actualizaron.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SolicitarSupresion()
    {
        var usuario = await gestorUsuarios.GetUserAsync(User);
        if (usuario is null)
            return Challenge();

        contexto.Auditorias.Add(PoliticaDatos.Registrar(usuario.Id, PoliticaDatos.AccionSolicitudSupresion));
        await contexto.SaveChangesAsync();

        logger.LogWarning("Solicitud de supresión de datos. UsuarioId={UsuarioId}", usuario.Id);
        TempData["Mensaje"] =
            $"Su solicitud quedó registrada. Se responderá en máximo {PoliticaDatos.DiasHabilesReclamo} días hábiles.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<MisDatosViewModel> CrearModeloAsync(Usuario usuario, ActualizarDatosViewModel datos)
    {
        var historial = await contexto.Auditorias
            .AsNoTracking()
            .Where(a => a.UsuarioId == usuario.Id && a.Recurso.StartsWith(PoliticaDatos.Recurso))
            .OrderByDescending(a => a.CreadoEn)
            .Select(a => new { a.Accion, a.Recurso, a.CreadoEn })
            .ToListAsync();

        var roles = await gestorUsuarios.GetRolesAsync(usuario);

        return new MisDatosViewModel
        {
            Correo = usuario.Email ?? string.Empty,
            FechaRegistro = usuario.FechaRegistro,
            Roles = roles.Select(NombreRol).ToList(),
            Historial = historial
                .Select(a => new RegistroDatosViewModel(
                    PoliticaDatos.Describir(a.Accion),
                    a.Recurso.Replace(PoliticaDatos.Recurso, "").Trim(),
                    a.CreadoEn))
                .ToList(),
            Datos = datos,
        };
    }

    private static string NombreRol(string rol) => rol == Roles.Dueno ? "Dueño" : rol;
}
