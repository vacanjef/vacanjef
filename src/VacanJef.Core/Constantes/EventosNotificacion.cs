namespace VacanJef.Core.Constantes;

// HU-009: códigos de los eventos que generan una notificación (campo Notificacion.Evento)
public static class EventosNotificacion
{
    public const string ReservaConfirmada = "EVT-RESERVA-CONFIRMADA";      // al dueño (CU-002 paso 5)
    public const string ReservaNueva = "EVT-RESERVA-NUEVA";                // a los administradores (CU-002 paso 6)
    public const string ObservacionRegistrada = "EVT-OBSERVACION";         // al dueño (HU-008)
    public const string ObservacionCorregida = "EVT-OBSERVACION-CORREGIDA"; // al dueño (HU-008, RN-006)
}
