using VacanJef.Core.Enums;

namespace VacanJef.Core.Entities;

// DAT-NOTIFICACION. HU-009 / RF-010: aviso para un usuario sobre un evento (EVT-*).
// En esta versión el canal es la plataforma; el correo (INT-03) queda para V1.1.
public class Notificacion
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string Evento { get; set; } = string.Empty; // código EVT-* (ver EventosNotificacion)
    public CanalNotificacion Canal { get; set; }
    public EstadoNotificacion Estado { get; set; } = EstadoNotificacion.Pendiente;
    public DateTime? EnviadaEn { get; set; }

    // HU-009
    public string Titulo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public string? Enlace { get; set; }          // ruta local, ej. /Reservas
    public DateTime CreadaEn { get; set; } = DateTime.UtcNow;
    public DateTime? LeidaEn { get; set; }       // null = no leída

    public Usuario? Usuario { get; set; }
}
