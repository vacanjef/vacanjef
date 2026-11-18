using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace VacanJef.Web.Models.Mascotas;

// HU-004. Id, Nombre y TieneFoto son solo para mostrar:
// el controlador usa el id de la ruta y vuelve a consultar la mascota.
public class CambiarFotoViewModel
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool TieneFoto { get; set; }

    [Display(Name = "Nueva foto")]
    public IFormFile? Foto { get; set; }
}
