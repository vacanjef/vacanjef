using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VacanJef.Core.Constantes;
using VacanJef.Core.Entities;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Models.Cuenta;

namespace VacanJef.Web.Controllers;

[AllowAnonymous]
public class CuentaController(
    UserManager<Usuario> gestorUsuarios,
    SignInManager<Usuario> gestorSesion,
    VacanJefDbContext contexto,
    ILogger<CuentaController> logger) : Controller
{
    [HttpGet]
    public IActionResult Registro() => View(new RegistroDuenoViewModel());

    [HttpPost]
    public async Task<IActionResult> Registro(RegistroDuenoViewModel modelo)
    {
        if (!modelo.AceptaTratamientoDatos)
            ModelState.AddModelError(nameof(modelo.AceptaTratamientoDatos),
                "Debe autorizar el tratamiento de datos personales para crear la cuenta.");

        if (!ModelState.IsValid)
            return View(modelo);

        var correo = modelo.Correo.Trim();
        var usuario = new Usuario
        {
            UserName = correo,
            Email = correo,
            Nombre = modelo.Nombre.Trim(),
            PhoneNumber = modelo.Telefono.Trim(),
        };

        // Usuario y rol se guardan juntos: si algo falla, no queda nada a medias
        await using var transaccion = await contexto.Database.BeginTransactionAsync();

        var creacion = await gestorUsuarios.CreateAsync(usuario, modelo.Contrasena);
        if (!creacion.Succeeded)
        {
            AgregarErrores(creacion);
            return View(modelo);
        }

        // RF-001: el registro público siempre asigna el rol Dueño
        var asignacion = await gestorUsuarios.AddToRoleAsync(usuario, Roles.Dueno);
        if (!asignacion.Succeeded)
        {
            AgregarErrores(asignacion);
            return View(modelo);
        }

        await transaccion.CommitAsync();

        // En el log solo va el Id: nunca correos ni contraseñas
        logger.LogInformation("Nuevo dueño registrado. UsuarioId={UsuarioId}", usuario.Id);

        await gestorSesion.SignInAsync(usuario, isPersistent: false);
        return RedirectToAction("Index", "Home");
    }

    private void AgregarErrores(IdentityResult resultado)
    {
        foreach (var descripcion in resultado.Errors.Select(e => e.Description).Distinct())
            ModelState.AddModelError(string.Empty, descripcion);
    }
}
