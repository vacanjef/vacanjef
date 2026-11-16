using System.ComponentModel.DataAnnotations;

namespace VacanJef.Web.Models.Cuenta;

// HU-001 / RF-001. No tiene campo de rol a propósito:
// quien se registra desde el sitio público siempre queda como Dueño.
public class RegistroDuenoViewModel
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre admite máximo 100 caracteres.")]
    [Display(Name = "Nombre completo")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
    [StringLength(150, ErrorMessage = "El correo admite máximo 150 caracteres.")]
    [Display(Name = "Correo electrónico")]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [RegularExpression(@"^\+?[0-9 \-]{7,20}$", ErrorMessage = "Ingrese un teléfono válido (solo números, espacios, guiones y +).")]
    [Display(Name = "Teléfono")]
    public string Telefono { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Contrasena { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme la contraseña.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Contrasena), ErrorMessage = "Las contraseñas no coinciden.")]
    [Display(Name = "Confirmar contraseña")]
    public string ConfirmarContrasena { get; set; } = string.Empty;

    // RNF-007: autorización de tratamiento de datos (Ley 1581 de 2012)
    [Display(Name = "Autorizo el tratamiento de mis datos personales según la Ley 1581 de 2012")]
    public bool AceptaTratamientoDatos { get; set; }
}
