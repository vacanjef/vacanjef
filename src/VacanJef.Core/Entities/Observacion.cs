using VacanJef.Core.Enums;

namespace VacanJef.Core.Entities;

// DAT-OBSERVACION. RN-006: no se modifica ni se elimina;
// un error se corrige con una nueva observación que apunta a la original.
public class Observacion
{
    public int Id { get; set; }
    public int ReservaId { get; set; }
    public int AutorId { get; set; }
    public TipoObservacion Tipo { get; set; }
    public string Detalle { get; set; } = string.Empty;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public int? CorrigeAId { get; set; }

    public Reserva? Reserva { get; set; }
    public Usuario? Autor { get; set; }
    public Observacion? CorrigeA { get; set; }
    public ICollection<Observacion> Correcciones { get; set; } = new List<Observacion>();
}
