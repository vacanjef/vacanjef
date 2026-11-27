using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using VacanJef.Core.Constantes;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Models.Monitoreo;
using VacanJef.Web.Seguridad;
using VacanJef.Web.Servicios;

namespace VacanJef.Web.Controllers;

// HU-007 / RF-007 / RN-004: ver a la mascota en tiempo real.
// El dueño ve solo su mascota durante una estadía activa; el cuidador y el administrador ven las estadías activas.
// Flujo: Ver (autoriza, audita y emite un token de 5 minutos) -> el navegador envía su oferta WebRTC a Senal
// -> VacanJef valida el token y la reserva y reenvía la oferta a MediaMTX -> el video llega directo al navegador.
[Authorize(Roles = Roles.Dueno + "," + Roles.Cuidador + "," + Roles.Administrador)]
public class MonitoreoController(
    VacanJefDbContext contexto,
    IConfiguration configuracion,
    IDataProtectionProvider proteccion,
    ILogger<MonitoreoController> logger) : Controller
{
    public const int TamanoMaximoOferta = 64 * 1024;

    private ServicioMonitoreo Servicio => new(contexto);
    private TokenMonitoreo Tokens => new(proteccion);
    private bool EsPersonal => User.IsInRole(Roles.Cuidador) || User.IsInRole(Roles.Administrador);

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (ObtenerUsuarioId() is not int usuarioId)
            return Challenge();

        return View(await Servicio.EstadiasVisiblesAsync(usuarioId, EsPersonal));
    }

    [HttpGet]
    public async Task<IActionResult> Ver(int id)
    {
        if (ObtenerUsuarioId() is not int usuarioId)
            return Challenge();

        var servicio = Servicio;
        var autorizacion = await servicio.AutorizarAsync(id, usuarioId, EsPersonal);
        await servicio.RegistrarAccesoAsync(usuarioId, autorizacion);

        if (!autorizacion.Permitido)
        {
            // ESC-HU-007-03: se deniega y queda registrado
            logger.LogWarning("Acceso a cámara denegado. ReservaId={ReservaId} UsuarioId={UsuarioId}", id, usuarioId);
            return Forbid();
        }

        logger.LogInformation("Acceso a cámara permitido. ReservaId={ReservaId} UsuarioId={UsuarioId}", id, usuarioId);

        return View(new MonitoreoVerViewModel
        {
            ReservaId = id,
            MascotaId = autorizacion.MascotaId,
            Mascota = autorizacion.Mascota,
            UrlSenal = autorizacion.Ruta is null
                ? null
                : $"/Monitoreo/Senal?token={Uri.EscapeDataString(Tokens.Crear(id, usuarioId))}",
            MostrarFoto = !EsPersonal && autorizacion.TieneFoto,
        });
    }

    // Lo llama monitoreo.js con la oferta SDP. No usa el token antifalsificación del formulario:
    // lo reemplaza el token temporal, que solo sirve para este usuario y esta reserva.
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Senal([FromQuery] string? token)
    {
        if (ObtenerUsuarioId() is not int usuarioId)
            return StatusCode(StatusCodes.Status401Unauthorized);

        if (!Tokens.Validar(token, usuarioId, out var reservaId))
            return StatusCode(StatusCodes.Status403Forbidden);

        // La estadía pudo terminar después de abrir la página
        var autorizacion = await Servicio.AutorizarAsync(reservaId, usuarioId, EsPersonal);
        if (!autorizacion.Permitido)
            return StatusCode(StatusCodes.Status403Forbidden);

        if (autorizacion.Ruta is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);

        if (Request.ContentLength is > TamanoMaximoOferta)
            return StatusCode(StatusCodes.Status413PayloadTooLarge);

        string oferta;
        using (var lector = new StreamReader(Request.Body, Encoding.UTF8))
            oferta = await lector.ReadToEndAsync(HttpContext.RequestAborted);

        if (!oferta.StartsWith("v=0", StringComparison.Ordinal) || oferta.Length > TamanoMaximoOferta)
            return BadRequest();

        var cliente = new ClienteMediaMtx(ClienteMediaMtx.UrlDesde(configuracion));
        var respuesta = await cliente.NegociarAsync(autorizacion.Ruta, oferta, HttpContext.RequestAborted);

        if (respuesta is null)
        {
            // CA-HU-007-02: la página muestra el mensaje y la última fotografía
            logger.LogWarning("Cámara sin transmisión. ReservaId={ReservaId}", reservaId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return Content(respuesta, "application/sdp");
    }

    private int? ObtenerUsuarioId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
