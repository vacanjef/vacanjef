using System.ComponentModel.DataAnnotations;
using VacanJef.Web.Servicios;

namespace VacanJef.Web.Models.Usuarios;

// HU-015: listado de usuarios con filtros
public class UsuariosIndexViewModel
{
    public IReadOnlyList<UsuarioResumen> Usuarios { get; init; } = [];
    public string? Busqueda { get; init; }
    public string? Rol { get; init; }
    public int AdministradorId { get; init; }
}

// HU-015: cuenta de personal creada por el administrador
public class NuevoUsuarioViewModel
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

    [Required(ErrorMessage = "Elija el rol.")]
    [Display(Name = "Rol")]
    public string Rol { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña inicial es obligatoria.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña inicial")]
    public string Contrasena { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme la contraseña.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Contrasena), ErrorMessage = "Las contraseñas no coinciden.")]
    [Display(Name = "Confirmar contraseña")]
    public string ConfirmarContrasena { get; set; } = string.Empty;
}
