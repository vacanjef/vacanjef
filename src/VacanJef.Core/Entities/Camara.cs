namespace VacanJef.Core.Entities;

// DAT-CAMARA
public class Camara
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string UrlStream { get; set; } = string.Empty; // dato sensible: nunca se envía al navegador
    public bool Activa { get; set; } = true;

    public ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
}
