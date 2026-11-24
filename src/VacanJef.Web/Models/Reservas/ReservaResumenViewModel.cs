using VacanJef.Core.Enums;
using VacanJef.Web.Servicios;

namespace VacanJef.Web.Models.Reservas;

// Fila del listado "Mis reservas"
public record ReservaResumenViewModel(
    int Id,
    string Mascota,
    DateOnly FechaIngreso,
    DateOnly FechaSalida,
    TipoServicio Servicio,
    decimal CostoTotal,
    EstadoReserva Estado)
{
    public int Dias => ServicioReservas.Dias(FechaIngreso, FechaSalida);
}
