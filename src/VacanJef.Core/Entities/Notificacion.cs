using VacanJef.Core.Enums;

namespace VacanJef.Core.Entities;

// DAT-NOTIFICACION
public class Notificacion
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string Evento { get; set; } = string.Empty; // código EVT-*
    public CanalNotificacion Canal { get; set; }
    public EstadoNotificacion Estado { get; set; } = EstadoNotificacion.Pendiente;
    public DateTime? EnviadaEn { get; set; }

    public Usuario? Usuario { get; set; }
}
