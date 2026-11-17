using System.ComponentModel.DataAnnotations;

namespace VacanJef.Web.Models.DatosPersonales;

// TT-004: derechos del titular (conocer, actualizar, rectificar y solicitar supresión)
public class MisDatosViewModel
{
    public string Correo { get; init; } = string.Empty;
    public DateTime FechaRegistro { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
    public IReadOnlyList<RegistroDatosViewModel> Historial { get; init; } = [];
    public ActualizarDatosViewModel Datos { get; init; } = new();
}

public record RegistroDatosViewModel(string Descripcion, string Version, DateTime Fecha);

public class ActualizarDatosViewModel
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre admite máximo 100 caracteres.")]
    [Display(Name = "Nombre completo")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [RegularExpression(@"^\+?[0-9 \-]{7,20}$", ErrorMessage = "Ingrese un teléfono válido (solo números, espacios, guiones y +).")]
    [Display(Name = "Teléfono")]
    public string Telefono { get; set; } = string.Empty;
}
