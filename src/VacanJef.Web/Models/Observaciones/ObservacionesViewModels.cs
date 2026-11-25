using System.ComponentModel.DataAnnotations;
using VacanJef.Core.Enums;

namespace VacanJef.Web.Models.Observaciones;

// HU-008: fila del listado de estadías activas que ve el cuidador
public record EstadiaActivaViewModel(
    int ReservaId,
    string Mascota,
    string Raza,
    string Dueno,
    DateOnly FechaIngreso,
    DateOnly FechaSalida,
    int Observaciones,
    DateTime? UltimaObservacion);

public record ObservacionItemViewModel(
    int Id,
    TipoObservacion Tipo,
    string Detalle,
    DateTime CreadoEn,
    string Autor,
    int? CorrigeAId);

public record RegistroSaludResumen(TipoRegistroSalud Tipo, string Descripcion, DateOnly? Fecha);

// Página de una estadía: salud de la mascota, historial de observaciones y formulario
public class EstadiaDetalleViewModel
{
    public int ReservaId { get; set; }
    public int MascotaId { get; set; }
    public string Mascota { get; set; } = string.Empty;
    public string Raza { get; set; } = string.Empty;
    public string Dueno { get; set; } = string.Empty;
    public DateOnly FechaIngreso { get; set; }
    public DateOnly FechaSalida { get; set; }
    public EstadoReserva Estado { get; set; }
    public List<RegistroSaludResumen> Salud { get; set; } = [];
    public List<ObservacionItemViewModel> Observaciones { get; set; } = [];
    public NuevaObservacionViewModel Nueva { get; set; } = new();
    public ObservacionItemViewModel? Corrigiendo { get; set; }

    // CA-HU-008-02: solo se registran observaciones durante una estadía activa
    public bool PermiteRegistrar => Estado == EstadoReserva.Activa;
}

// No tiene ReservaId ni AutorId: la estadía sale de la ruta y el autor, de la sesión
public class NuevaObservacionViewModel
{
    [Required(ErrorMessage = "Seleccione el tipo de observación.")]
    [Display(Name = "Tipo")]
    public TipoObservacion? Tipo { get; set; }

    // CA-HU-008-04
    [Required(ErrorMessage = "Escriba el detalle de la observación.")]
    [StringLength(1000, ErrorMessage = "El detalle admite máximo 1000 caracteres.")]
    [Display(Name = "Detalle")]
    public string Detalle { get; set; } = string.Empty;

    // RN-006: si viene, la observación nueva corrige a esta (la original no se toca)
    public int? CorrigeAId { get; set; }
}
