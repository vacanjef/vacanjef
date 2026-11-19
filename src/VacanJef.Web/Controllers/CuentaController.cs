using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VacanJef.Core.Constantes;
using VacanJef.Core.Entities;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Models.Cuenta;
using VacanJef.Web.Seguridad;

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

        // TT-004: prueba de la autorización de datos (Ley 1581): quién, cuándo y qué versión de la política
        contexto.Auditorias.Add(PoliticaDatos.Registrar(usuario.Id, PoliticaDatos.AccionAutorizacion));
        await contexto.SaveChangesAsync();

        await transaccion.CommitAsync();

        // En el log solo va el Id: nunca correos ni contraseñas
        logger.LogInformation("Nuevo dueño registrado. UsuarioId={UsuarioId}", usuario.Id);

        await gestorSesion.SignInAsync(usuario, isPersistent: false);
        return RedirectToAction("Index", "Home");
    }

    // HU-002 / RF-002: inicio de sesión
    [HttpGet]
    public IActionResult IniciarSesion(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirigirDespuesDeIniciar(returnUrl);

        return View(new IniciarSesionViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> IniciarSesion(IniciarSesionViewModel modelo)
    {
        if (!ModelState.IsValid)
            return View(modelo);

        var correo = modelo.Correo.Trim();
        var usuario = await gestorUsuarios.FindByEmailAsync(correo);

        if (usuario is not null)
        {
            // RN-005: lockoutOnFailure cuenta cada intento fallido y bloquea al quinto
            var resultado = await gestorSesion.CheckPasswordSignInAsync(
                usuario, modelo.Contrasena, lockoutOnFailure: true);

            if (resultado.Succeeded)
            {
                // HU-015: una cuenta desactivada por el administrador no entra.
                // Se avisa solo después de validar la contraseña, para no revelar qué cuentas existen.
                if (!usuario.Activo)
                {
                    logger.LogWarning("Intento de inicio de sesión de una cuenta desactivada. UsuarioId={UsuarioId}", usuario.Id);
                    ModelState.AddModelError(string.Empty,
                        "Su cuenta está desactivada. Comuníquese con el administrador de la guardería.");
                    return View(modelo);
                }

                await gestorSesion.SignInAsync(usuario, modelo.Recordarme);
                logger.LogInformation("Inicio de sesión correcto. UsuarioId={UsuarioId}", usuario.Id);
                return RedirigirDespuesDeIniciar(modelo.ReturnUrl);
            }

            if (resultado.IsLockedOut)
            {
                logger.LogWarning("Cuenta bloqueada por intentos fallidos. UsuarioId={UsuarioId}", usuario.Id);
                ModelState.AddModelError(string.Empty,
                    $"Su cuenta quedó bloqueada por {OpcionesIdentity.MaximoIntentosFallidos} intentos fallidos. " +
                    $"Intente de nuevo en {OpcionesIdentity.MinutosBloqueo} minutos.");
                return View(modelo);
            }
        }

        // Mismo mensaje si el correo no existe o la contraseña está mal:
        // así no se revela qué correos están registrados.
        logger.LogInformation("Intento de inicio de sesión fallido.");
        ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
        return View(modelo);
    }

    [HttpPost]
    public async Task<IActionResult> CerrarSesion()
    {
        await gestorSesion.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccesoDenegado() => View();

    // Evita redirecciones abiertas: solo se vuelve a rutas de este mismo sitio
    private IActionResult RedirigirDespuesDeIniciar(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Index", "Home");

    private void AgregarErrores(IdentityResult resultado)
    {
        foreach (var descripcion in resultado.Errors.Select(e => e.Description).Distinct())
            ModelState.AddModelError(string.Empty, descripcion);
    }
}
