using VacanJef.Core.Enums;

namespace VacanJef.Core.Entities;

// DAT-RESERVA
public class Reserva
{
    public int Id { get; set; }
    public int MascotaId { get; set; }
    public int DuenoId { get; set; }
    public int? CamaraId { get; set; }
    public DateOnly FechaIngreso { get; set; }
    public DateOnly FechaSalida { get; set; }
    public TipoServicio Servicio { get; set; }
    public decimal CostoTotal { get; set; }   // RN-007: lo calcula el sistema
    public EstadoReserva Estado { get; set; } = EstadoReserva.Confirmada;
    public DateTime? IngresoReal { get; set; }
    public DateTime? SalidaReal { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public Mascota? Mascota { get; set; }
    public Usuario? Dueno { get; set; }
    public Camara? Camara { get; set; }
    public ICollection<Observacion> Observaciones { get; set; } = new List<Observacion>();
}
