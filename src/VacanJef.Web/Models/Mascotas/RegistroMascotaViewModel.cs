using System.ComponentModel.DataAnnotations;
using System.Globalization;
using VacanJef.Core.Entities;
using VacanJef.Core.Enums;

namespace VacanJef.Web.Models.Mascotas;

// HU-003: registro de mascota con información de salud (RN-002).
// No tiene DuenoId a propósito: el dueño siempre es el usuario de la sesión.
public class RegistroMascotaViewModel : IValidatableObject
{
    public const decimal PesoMinimoKg = 0.1m;
    public const decimal PesoMaximoKg = 120m;
    public const int DescripcionMaxima = 255;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(80, ErrorMessage = "El nombre admite máximo 80 caracteres.")]
    [Display(Name = "Nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "La raza es obligatoria.")]
    [StringLength(80, ErrorMessage = "La raza admite máximo 80 caracteres.")]
    [Display(Name = "Raza")]
    public string Raza { get; set; } = string.Empty;

    [Required(ErrorMessage = "La edad es obligatoria.")]
    [Range(0, 30, ErrorMessage = "La edad debe estar entre 0 y 30 años.")]
    [Display(Name = "Edad (años)")]
    public int? Edad { get; set; }

    // Texto para aceptar "12.5" y "12,5" sin depender de la cultura del servidor
    [Required(ErrorMessage = "El peso es obligatorio.")]
    [Display(Name = "Peso (kg)")]
    public string Peso { get; set; } = string.Empty;

    [Required(ErrorMessage = "Seleccione el sexo.")]
    [Display(Name = "Sexo")]
    public SexoMascota? Sexo { get; set; }

    public List<RegistroSaludItemViewModel> RegistrosSalud { get; set; } = new();

    public static bool IntentarLeerPeso(string? texto, out decimal peso) =>
        decimal.TryParse((texto ?? string.Empty).Trim().Replace(',', '.'),
            NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out peso);

    public IEnumerable<ValidationResult> Validate(ValidationContext contexto)
    {
        if (!IntentarLeerPeso(Peso, out var peso))
            yield return new ValidationResult("Ingrese el peso en números, por ejemplo 12.5.", [nameof(Peso)]);
        else if (peso < PesoMinimoKg || peso > PesoMaximoKg)
            yield return new ValidationResult(
                $"El peso debe estar entre {PesoMinimoKg.ToString(CultureInfo.InvariantCulture)} y {PesoMaximoKg} kg.",
                [nameof(Peso)]);

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var tieneVacuna = false;

        for (var i = 0; i < RegistrosSalud.Count; i++)
        {
            var registro = RegistrosSalud[i];
            if (registro.EstaVacio) continue;

            var prefijo = $"{nameof(RegistrosSalud)}[{i}].";

            if (string.IsNullOrWhiteSpace(registro.Descripcion))
                yield return new ValidationResult("Escriba la descripción.", [prefijo + nameof(registro.Descripcion)]);
            else if (registro.Descripcion.Trim().Length > DescripcionMaxima)
                yield return new ValidationResult($"Máximo {DescripcionMaxima} caracteres.", [prefijo + nameof(registro.Descripcion)]);

            if (registro.Tipo == TipoRegistroSalud.Vacuna && registro.Fecha is null)
                yield return new ValidationResult("Indique la fecha de aplicación.", [prefijo + nameof(registro.Fecha)]);

            if (registro.Fecha > hoy)
                yield return new ValidationResult("La fecha no puede ser futura.", [prefijo + nameof(registro.Fecha)]);

            if (registro.Tipo == TipoRegistroSalud.Vacuna && !string.IsNullOrWhiteSpace(registro.Descripcion))
                tieneVacuna = true;
        }

        // RN-002: la mascota debe llegar con su registro de vacunas
        if (!tieneVacuna)
            yield return new ValidationResult("Registre al menos una vacuna con su fecha de aplicación.", [nameof(RegistrosSalud)]);
    }

    public Mascota CrearMascota(int duenoId)
    {
        IntentarLeerPeso(Peso, out var peso);

        var mascota = new Mascota
        {
            DuenoId = duenoId,
            Nombre = Nombre.Trim(),
            Raza = Raza.Trim(),
            Edad = (byte)Edad!.Value,
            PesoKg = decimal.Round(peso, 2),
            Sexo = Sexo!.Value,
        };

        foreach (var registro in RegistrosSalud.Where(r => !r.EstaVacio))
        {
            mascota.RegistrosSalud.Add(new RegistroSalud
            {
                Tipo = registro.Tipo,
                Descripcion = registro.Descripcion!.Trim(),
                Fecha = registro.Fecha,
            });
        }

        return mascota;
    }
}
