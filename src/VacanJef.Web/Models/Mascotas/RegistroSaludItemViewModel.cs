using VacanJef.Core.Enums;

namespace VacanJef.Web.Models.Mascotas;

// Una fila del formulario de salud (vacuna, alergia o medicamento).
// Las validaciones se hacen en RegistroMascotaViewModel.Validate para
// que los mensajes no salgan repetidos.
public class RegistroSaludItemViewModel
{
    public TipoRegistroSalud Tipo { get; set; } = TipoRegistroSalud.Vacuna;
    public string? Descripcion { get; set; }
    public DateOnly? Fecha { get; set; }

    // Las filas que el dueño dejó en blanco se ignoran
    public bool EstaVacio => string.IsNullOrWhiteSpace(Descripcion) && Fecha is null;
}
