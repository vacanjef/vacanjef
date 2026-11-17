using System.ComponentModel.DataAnnotations;

namespace VacanJef.Web.Models.Cuenta;

// HU-002 / RF-002: inicio de sesión
public class IniciarSesionViewModel
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
    [StringLength(150, ErrorMessage = "El correo admite máximo 150 caracteres.")]
    [Display(Name = "Correo electrónico")]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(100, ErrorMessage = "La contraseña admite máximo 100 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Contrasena { get; set; } = string.Empty;

    [Display(Name = "Mantener la sesión iniciada en este equipo")]
    public bool Recordarme { get; set; }

    // Página a la que se vuelve después de iniciar sesión (solo rutas locales)
    public string? ReturnUrl { get; set; }
}
