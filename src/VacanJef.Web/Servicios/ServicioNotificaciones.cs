using Microsoft.EntityFrameworkCore;
using VacanJef.Core.Constantes;
using VacanJef.Core.Entities;
using VacanJef.Core.Enums;
using VacanJef.Infrastructure.Data;
using VacanJef.Infrastructure.Data.Configurations;
using VacanJef.Web.Models.Notificaciones;
using VacanJef.Web.Models.Observaciones;
using VacanJef.Web.Models.Reservas;

namespace VacanJef.Web.Servicios;

// HU-009 / RF-010: envía al usuario el aviso en tiempo real (SignalR). En las pruebas se reemplaza.
public interface IEmisorNotificaciones
{
    Task EnviarAsync(int usuarioId, NotificacionItemViewModel? notificacion, int noLeidas);
}

// HU-009 / RF-010: guarda las notificaciones, las envía en tiempo real y las marca como leídas.
// Una falla al notificar nunca deshace la acción que la originó (la reserva o la observación ya quedaron guardadas).
// Los mensajes no incluyen el texto de las observaciones: el detalle se consulta en el historial (Ley 1581).
public class ServicioNotificaciones(
    VacanJefDbContext contexto,
    IEmisorNotificaciones emisor,
    ILogger<ServicioNotificaciones> logger)
{
    public const int MaximoLista = 50;

    // CA-HU-009-01 (CU-002 pasos 5 y 6): al dueño y a los administradores activos
    public async Task NotificarReservaConfirmadaAsync(int reservaId)
    {
        try
        {
            var datos = await contexto.Reservas
                .AsNoTracking()
                .Where(r => r.Id == reservaId)
                .Select(r => new
                {
                    r.DuenoId,
                    Mascota = r.Mascota!.Nombre,
                    Dueno = r.Dueno!.Nombre,
                    r.FechaIngreso,
                    r.FechaSalida,
                    r.CostoTotal,
                })
                .FirstOrDefaultAsync();

            if (datos is null)
                return;

            var fechas = $"del {PresentacionReservas.Fecha(datos.FechaIngreso)} al {PresentacionReservas.Fecha(datos.FechaSalida)}";

            await NotificarAsync(datos.DuenoId, EventosNotificacion.ReservaConfirmada, "Reserva confirmada",
                $"La estadía de {datos.Mascota} {fechas} quedó confirmada. Costo total: {PresentacionReservas.Pesos(datos.CostoTotal)}.",
                "/Reservas");

            foreach (var administradorId in await AdministradoresActivosAsync())
            {
                await NotificarAsync(administradorId, EventosNotificacion.ReservaNueva, "Nueva reserva",
                    $"{datos.Dueno} reservó una estadía para {datos.Mascota} {fechas}.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudieron crear las notificaciones de la reserva. ReservaId={ReservaId}", reservaId);
        }
    }

    // CA-HU-009-02: al dueño de la mascota cuando el cuidador registra o corrige una observación
    public async Task NotificarObservacionAsync(int observacionId)
    {
        try
        {
            var datos = await contexto.Observaciones
                .AsNoTracking()
                .Where(o => o.Id == observacionId)
                .Select(o => new
                {
                    o.Tipo,
                    o.CorrigeAId,
                    o.Reserva!.DuenoId,
                    o.Reserva!.MascotaId,
                    Mascota = o.Reserva!.Mascota!.Nombre,
                })
                .FirstOrDefaultAsync();

            if (datos is null)
                return;

            var tipo = PresentacionObservaciones.Tipo(datos.Tipo).ToLowerInvariant();
            var (evento, titulo, mensaje) = datos.CorrigeAId is null
                ? (EventosNotificacion.ObservacionRegistrada, "Nueva observación",
                    $"El cuidador registró una observación de {tipo} sobre {datos.Mascota}.")
                : (EventosNotificacion.ObservacionCorregida, "Observación corregida",
                    $"El cuidador corrigió una observación de {tipo} sobre {datos.Mascota}.");

            await NotificarAsync(datos.DuenoId, evento, titulo, mensaje, $"/Mascotas/{datos.MascotaId}/Historial");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo crear la notificación de la observación. ObservacionId={ObservacionId}", observacionId);
        }
    }

    // Base para cualquier evento nuevo (por ejemplo, el ingreso y la salida de HU-014)
    public async Task<Notificacion> NotificarAsync(int usuarioId, string evento, string titulo, string mensaje, string? enlace = null)
    {
        var ahora = DateTime.UtcNow;
        var notificacion = new Notificacion
        {
            UsuarioId = usuarioId,
            Evento = evento,
            Canal = CanalNotificacion.Plataforma,
            Estado = EstadoNotificacion.Enviada,
            Titulo = Recortar(titulo, NotificacionConfiguration.MaximoTitulo),
            Mensaje = Recortar(mensaje, NotificacionConfiguration.MaximoMensaje),
            Enlace = enlace,
            CreadaEn = ahora,
            EnviadaEn = ahora,
        };

        contexto.Notificaciones.Add(notificacion);
        await contexto.SaveChangesAsync();

        // En el log solo van identificadores y el código del evento, nunca el texto
        logger.LogInformation("Notificación creada. NotificacionId={NotificacionId} UsuarioId={UsuarioId} Evento={Evento}",
            notificacion.Id, usuarioId, evento);

        await EmitirAsync(usuarioId, Item(notificacion));
        return notificacion;
    }

    // CA-HU-009-04 y -05: solo las del usuario, de la más reciente a la más antigua
    public Task<List<NotificacionItemViewModel>> ListarAsync(int usuarioId) =>
        contexto.Notificaciones
            .AsNoTracking()
            .Where(n => n.UsuarioId == usuarioId)
            .OrderByDescending(n => n.CreadaEn).ThenByDescending(n => n.Id)
            .Take(MaximoLista)
            .Select(n => new NotificacionItemViewModel(n.Id, n.Titulo, n.Mensaje, n.Enlace, n.CreadaEn, n.LeidaEn != null))
            .ToListAsync();

    public Task<int> ContarNoLeidasAsync(int usuarioId) =>
        contexto.Notificaciones.CountAsync(n => n.UsuarioId == usuarioId && n.LeidaEn == null);

    // CA-HU-009-04: una notificación de otro usuario se trata como inexistente
    public async Task<(bool Encontrada, string? Enlace)> MarcarLeidaAsync(int usuarioId, int notificacionId)
    {
        var notificacion = await contexto.Notificaciones
            .FirstOrDefaultAsync(n => n.Id == notificacionId && n.UsuarioId == usuarioId);

        if (notificacion is null)
            return (false, null);

        if (notificacion.LeidaEn is null)
        {
            notificacion.LeidaEn = DateTime.UtcNow;
            await contexto.SaveChangesAsync();
            await EmitirAsync(usuarioId, null);
        }

        return (true, notificacion.Enlace);
    }

    public async Task<int> MarcarTodasAsync(int usuarioId)
    {
        var pendientes = await contexto.Notificaciones
            .Where(n => n.UsuarioId == usuarioId && n.LeidaEn == null)
            .ToListAsync();

        if (pendientes.Count == 0)
            return 0;

        var ahora = DateTime.UtcNow;
        foreach (var notificacion in pendientes)
            notificacion.LeidaEn = ahora;

        await contexto.SaveChangesAsync();
        await EmitirAsync(usuarioId, null);
        return pendientes.Count;
    }

    // Solo rutas del mismo sitio (evita redirecciones abiertas)
    public static bool EsRutaLocal(string? ruta) =>
        !string.IsNullOrEmpty(ruta) && ruta[0] == '/' && (ruta.Length == 1 || (ruta[1] != '/' && ruta[1] != '\\'));

    private Task<List<int>> AdministradoresActivosAsync() =>
        (from usuarioRol in contexto.UserRoles
         join rol in contexto.Roles on usuarioRol.RoleId equals rol.Id
         join usuario in contexto.Users on usuarioRol.UserId equals usuario.Id
         where rol.Name == Roles.Administrador && usuario.Activo
         select usuario.Id)
        .Distinct()
        .ToListAsync();

    // CA-HU-009-03: el aviso en tiempo real es un extra; si falla, la notificación ya quedó guardada
    private async Task EmitirAsync(int usuarioId, NotificacionItemViewModel? item)
    {
        try
        {
            await emisor.EnviarAsync(usuarioId, item, await ContarNoLeidasAsync(usuarioId));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo enviar la notificación en tiempo real. UsuarioId={UsuarioId}", usuarioId);
        }
    }

    private static NotificacionItemViewModel Item(Notificacion n) =>
        new(n.Id, n.Titulo, n.Mensaje, n.Enlace, n.CreadaEn, n.LeidaEn is not null);

    private static string Recortar(string texto, int maximo) =>
        texto.Length <= maximo ? texto : texto[..(maximo - 1)] + "…";
}
