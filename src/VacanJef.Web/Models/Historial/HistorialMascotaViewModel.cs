using VacanJef.Core.Enums;
using VacanJef.Web.Models.Observaciones;

namespace VacanJef.Web.Models.Historial;

// HU-013: lo que el dueño ve de su mascota: salud y observaciones por estadía
public class HistorialMascotaViewModel
{
    public int MascotaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Raza { get; set; } = string.Empty;
    public int Edad { get; set; }
    public List<RegistroSaludResumen> Salud { get; set; } = [];
    public List<EstadiaHistorialViewModel> Estadias { get; set; } = [];

    // CA-HU-013-03
    public bool TieneObservaciones => Estadias.Any(e => e.Observaciones.Count > 0);
}

public class EstadiaHistorialViewModel
{
    public int ReservaId { get; set; }
    public DateOnly FechaIngreso { get; set; }
    public DateOnly FechaSalida { get; set; }
    public EstadoReserva Estado { get; set; }
    public List<ObservacionItemViewModel> Observaciones { get; set; } = [];
}
