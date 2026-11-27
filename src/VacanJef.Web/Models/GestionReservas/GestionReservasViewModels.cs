using VacanJef.Core.Enums;

namespace VacanJef.Web.Models.GestionReservas;

// HU-006: filtros del listado (llegan por la URL, GET)
public class FiltroReservas
{
    public EstadoReserva? Estado { get; set; }
    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }
    public string? Texto { get; set; }
}

public record ReservaAdminItem(
    int Id,
    string Mascota,
    string Dueno,
    string Correo,
    DateOnly FechaIngreso,
    DateOnly FechaSalida,
    TipoServicio Servicio,
    decimal CostoTotal,
    EstadoReserva Estado)
{
    // CA-HU-006-02/03: solo una reserva Confirmada se puede cancelar
    public bool SePuedeCancelar => Estado == EstadoReserva.Confirmada;
}

// CA-HU-006-04: cupos usados frente a la capacidad de cada día
public record OcupacionDia(DateOnly Fecha, int Ocupados, int Capacidad)
{
    public int Porcentaje => Capacidad <= 0 ? 0 : (int)Math.Round(Ocupados * 100.0 / Capacidad);
    public bool Lleno => Ocupados >= Capacidad;
}

public class GestionReservasViewModel
{
    public FiltroReservas Filtro { get; set; } = new();
    public List<ReservaAdminItem> Reservas { get; set; } = [];
    public List<OcupacionDia> Ocupacion { get; set; } = [];
    public bool HayMas { get; set; }
}
