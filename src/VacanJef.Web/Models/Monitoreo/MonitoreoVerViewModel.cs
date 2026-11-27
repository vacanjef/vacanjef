namespace VacanJef.Web.Models.Monitoreo;

// HU-007: página de video en vivo de una estadía
public class MonitoreoVerViewModel
{
    public int ReservaId { get; init; }
    public int MascotaId { get; init; }
    public string Mascota { get; init; } = string.Empty;

    // Dirección de la negociación en VacanJef, con el token temporal. Null si no hay cámara disponible.
    public string? UrlSenal { get; init; }

    // La foto de respaldo solo se muestra al dueño (MascotasController.Foto la entrega solo a él)
    public bool MostrarFoto { get; init; }
}
