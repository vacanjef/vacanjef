using System.Globalization;
using VacanJef.Web.Servicios;

namespace VacanJef.Web.Models.Estadias;

// HU-014: panel del cuidador y del administrador
public class EstadiasPanelViewModel
{
    public DateOnly Hoy { get; init; }
    public IReadOnlyList<EstadiaPanel> Llegadas { get; init; } = [];
    public IReadOnlyList<EstadiaPanel> EnCurso { get; init; } = [];
    public IReadOnlyList<CamaraDisponible> Camaras { get; init; } = [];

    // Las horas se guardan en UTC; Colombia es UTC-5 todo el año
    public static string HoraColombia(DateTime? utc) =>
        utc is DateTime valor
            ? valor.AddHours(-5).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
            : "—";
}
