using VacanJef.Core.Enums;

namespace VacanJef.Core.Entities;

// DAT-MASCOTA
public class Mascota
{
    public int Id { get; set; }
    public int DuenoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Raza { get; set; } = string.Empty;
    public byte Edad { get; set; }
    public decimal PesoKg { get; set; }
    public SexoMascota Sexo { get; set; }
    public string? FotoUrl { get; set; }   // RN-008: JPG o PNG, máx. 5 MB
    public bool Activo { get; set; } = true; // borrado lógico
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public Usuario? Dueno { get; set; }
    public ICollection<RegistroSalud> RegistrosSalud { get; set; } = new List<RegistroSalud>();
    public ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
}
