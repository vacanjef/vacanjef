using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VacanJef.Core.Constantes;
using VacanJef.Core.Entities;
using VacanJef.Core.Enums;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Models.Reservas;
using VacanJef.Web.Servicios;

namespace VacanJef.Web.Controllers;

// HU-005 / CU-002: el dueño reserva una estadía para su mascota.
// CA-HU-005-04: solo usuarios autenticados con rol Dueño (un visitante es enviado a iniciar sesión).
// La gestión de reservas del administrador es HU-006.
[Authorize(Roles = Roles.Dueno)]
public class ReservasController(
    VacanJefDbContext contexto,
    IConfiguration configuracion,
    ILogger<ReservasController> logger) : Controller
{
    public const string AccionCotizar = "cotizar";
    public const string AccionConfirmar = "confirmar";

    private OpcionesReservas Opciones => OpcionesReservas.Desde(configuracion);

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (ObtenerDuenoId() is not int duenoId)
            return Challenge();

        var reservas = await contexto.Set<Reserva>()
            .AsNoTracking()
            .Where(r => r.DuenoId == duenoId)
            .OrderByDescending(r => r.FechaIngreso)
            .Select(r => new ReservaResumenViewModel(
                r.Id, r.Mascota!.Nombre, r.FechaIngreso, r.FechaSalida, r.Servicio, r.CostoTotal, r.Estado))
            .ToListAsync();

        return View(reservas);
    }

    [HttpGet]
    public async Task<IActionResult> Nueva(int? mascotaId)
    {
        if (ObtenerDuenoId() is not int duenoId)
            return Challenge();

        var modelo = new NuevaReservaViewModel { MascotaId = mascotaId, Servicio = TipoServicio.Basico };
        await PrepararAsync(modelo, duenoId);
        return View(modelo);
    }

    [HttpPost]
    public async Task<IActionResult> Nueva(NuevaReservaViewModel modelo, string? accion,
        [FromServices] ServicioNotificaciones? notificaciones = null)
    {
        if (ObtenerDuenoId() is not int duenoId)
            return Challenge();

        if (!ModelState.IsValid)
        {
            await PrepararAsync(modelo, duenoId);
            return View(modelo);
        }

        var servicio = new ServicioReservas(contexto, Opciones);
        var hoy = ServicioReservas.HoyEnColombia();
        var mascotaId = modelo.MascotaId!.Value;
        var ingreso = modelo.FechaIngreso!.Value;
        var salida = modelo.FechaSalida!.Value;
        var tipo = modelo.Servicio!.Value;

        ResultadoReserva resultado;

        if (accion == AccionConfirmar)
        {
            resultado = await servicio.CrearAsync(duenoId, mascotaId, ingreso, salida, tipo, hoy);

            if (resultado.Exitoso && resultado.Reserva is { } reserva)
            {
                // En el log solo van identificadores
                logger.LogInformation("Reserva creada. ReservaId={ReservaId} MascotaId={MascotaId} DuenoId={DuenoId}",
                    reserva.Id, mascotaId, duenoId);

                // HU-009: avisa al dueño y a los administradores (CU-002 pasos 5 y 6)
                if (notificaciones is not null)
                    await notificaciones.NotificarReservaConfirmadaAsync(reserva.Id);

                TempData["Mensaje"] =
                    $"Reserva confirmada del {PresentacionReservas.Fecha(ingreso)} al {PresentacionReservas.Fecha(salida)}. " +
                    $"Costo total: {PresentacionReservas.Pesos(reserva.CostoTotal)}.";
                return RedirectToAction(nameof(Index));
            }
        }
        else
        {
            resultado = await servicio.RevisarAsync(duenoId, mascotaId, ingreso, salida, tipo, hoy);
        }

        foreach (var error in resultado.Errores)
            ModelState.AddModelError(error.Campo, error.Mensaje);

        modelo.Cotizacion = resultado.Exitoso ? resultado.Cotizacion : null;
        modelo.Alternativas = resultado.Alternativas.ToList();

        await PrepararAsync(modelo, duenoId);
        return View(modelo);
    }

    // Datos para mostrar el formulario: solo las mascotas activas del dueño (RN-001)
    private async Task PrepararAsync(NuevaReservaViewModel modelo, int duenoId)
    {
        modelo.Mascotas = await contexto.Set<Mascota>()
            .AsNoTracking()
            .Where(m => m.DuenoId == duenoId && m.Activo)
            .OrderBy(m => m.Nombre)
            .Select(m => new SelectListItem(m.Nombre, m.Id.ToString()))
            .ToListAsync();

        var opciones = Opciones;
        modelo.Tarifas = Enum.GetValues<TipoServicio>().ToDictionary(s => s, s => opciones.Tarifa(s));
        modelo.CapacidadDiaria = opciones.CapacidadDiaria;
        modelo.Hoy = ServicioReservas.HoyEnColombia();
    }

    private int? ObtenerDuenoId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
