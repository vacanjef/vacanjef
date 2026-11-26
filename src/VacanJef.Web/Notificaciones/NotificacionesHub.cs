using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using VacanJef.Web.Models.Notificaciones;
using VacanJef.Web.Servicios;

namespace VacanJef.Web.Notificaciones;

// HU-009 / CA-HU-009-03: canal de tiempo real. Solo usuarios con sesión.
// Cada usuario recibe únicamente lo suyo: SignalR identifica al usuario por el claim NameIdentifier (su Id).
[Authorize]
public class NotificacionesHub : Hub
{
    public const string Ruta = "/hubs/notificaciones";
    public const string Evento = "notificacion";
}

public class EmisorNotificacionesSignalR(IHubContext<NotificacionesHub> hub) : IEmisorNotificaciones
{
    public Task EnviarAsync(int usuarioId, NotificacionItemViewModel? notificacion, int noLeidas) =>
        hub.Clients.User(usuarioId.ToString(CultureInfo.InvariantCulture)).SendAsync(
            NotificacionesHub.Evento,
            new
            {
                noLeidas,
                notificacion = notificacion is null
                    ? null
                    : new { notificacion.Id, notificacion.Titulo, notificacion.Mensaje },
            });
}
