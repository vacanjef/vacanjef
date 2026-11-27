using System.ComponentModel.DataAnnotations;

namespace VacanJef.Web.Models.Camaras;

// HU-014: registro de una cámara (solo administrador)
public class NuevaCamaraViewModel
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(80, ErrorMessage = "El nombre admite máximo 80 caracteres.")]
    [Display(Name = "Nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "La dirección del video es obligatoria.")]
    [StringLength(255, ErrorMessage = "La dirección admite máximo 255 caracteres.")]
    [Display(Name = "Dirección del video (RTSP)")]
    public string UrlStream { get; set; } = string.Empty;
}
