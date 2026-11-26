namespace VacanJef.Web.Models.Notificaciones;

// HU-009: una notificación en la lista y en el aviso en tiempo real
public record NotificacionItemViewModel(int Id, string Titulo, string Mensaje, string? Enlace, DateTime CreadaEn, bool Leida);
