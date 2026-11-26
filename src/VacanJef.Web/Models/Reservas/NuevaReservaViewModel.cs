using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using VacanJef.Core.Enums;
using VacanJef.Web.Servicios;

namespace VacanJef.Web.Models.Reservas;

// HU-005: formulario de nueva reserva.
// No tiene DuenoId a propósito: el dueño siempre es el usuario de la sesión.
// El costo tampoco viene del formulario: lo calcula el sistema (RN-007).
public class NuevaReservaViewModel
{
    [Required(ErrorMessage = "Seleccione la mascota.")]
    [Display(Name = "Mascota")]
    public int? MascotaId { get; set; }

    [Required(ErrorMessage = "Indique la fecha de ingreso.")]
    [Display(Name = "Fecha de ingreso")]
    public DateOnly? FechaIngreso { get; set; }

    [Required(ErrorMessage = "Indique la fecha de salida.")]
    [Display(Name = "Fecha de salida")]
    public DateOnly? FechaSalida { get; set; }

    [Required(ErrorMessage = "Seleccione el servicio.")]
    [Display(Name = "Servicio")]
    public TipoServicio? Servicio { get; set; }

    // Solo para mostrar: los llena el controlador
    [BindNever, ValidateNever]
    public List<SelectListItem> Mascotas { get; set; } = new();

    [BindNever, ValidateNever]
    public Dictionary<TipoServicio, decimal> Tarifas { get; set; } = new();

    [BindNever, ValidateNever]
    public int CapacidadDiaria { get; set; }

    [BindNever, ValidateNever]
    public DateOnly Hoy { get; set; }

    [BindNever, ValidateNever]
    public Cotizacion? Cotizacion { get; set; }

    [BindNever, ValidateNever]
    public List<RangoFechas> Alternativas { get; set; } = new();
}
