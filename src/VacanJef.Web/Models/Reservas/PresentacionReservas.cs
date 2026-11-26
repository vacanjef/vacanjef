using System.Globalization;
using VacanJef.Core.Enums;

namespace VacanJef.Web.Models.Reservas;

// Textos y formatos de las vistas de reservas.
// No depende de la cultura del servidor (Azure corre en Linux con cultura invariable).
public static class PresentacionReservas
{
    public static string Servicio(TipoServicio servicio) => servicio switch
    {
        TipoServicio.Basico => "Básico",
        TipoServicio.Premium => "Premium",
        TipoServicio.Spa => "Con spa",
        _ => servicio.ToString(),
    };

    // 180000 -> "$180.000"
    public static string Pesos(decimal valor) =>
        "$" + valor.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');

    public static string Fecha(DateOnly fecha) =>
        fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    // Formato que espera <input type="date">
    public static string FechaFormulario(DateOnly? fecha) =>
        fecha?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    public static string ClaseEstado(EstadoReserva estado) => estado switch
    {
        EstadoReserva.Confirmada => "text-bg-primary",
        EstadoReserva.Activa => "text-bg-success",
        EstadoReserva.Pagada => "text-bg-info",
        EstadoReserva.Finalizada => "text-bg-secondary",
        EstadoReserva.Cancelada => "text-bg-danger",
        _ => "text-bg-light",
    };
}
