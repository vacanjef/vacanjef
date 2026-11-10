using VacanJef.Core.Enums;

namespace VacanJef.Core.Entities;

// DAT-SALUD: vacunas, alergias y medicamentos (RN-002)
public class RegistroSalud
{
    public int Id { get; set; }
    public int MascotaId { get; set; }
    public TipoRegistroSalud Tipo { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public DateOnly? Fecha { get; set; }

    public Mascota? Mascota { get; set; }
}
