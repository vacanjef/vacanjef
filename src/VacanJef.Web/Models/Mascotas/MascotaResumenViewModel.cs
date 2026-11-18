using VacanJef.Core.Enums;

namespace VacanJef.Web.Models.Mascotas;

// Fila del listado "Mis mascotas"
public record MascotaResumenViewModel(
    int Id,
    string Nombre,
    string Raza,
    byte Edad,
    decimal PesoKg,
    SexoMascota Sexo,
    int Vacunas,
    int Alergias,
    int Medicamentos,
    bool TieneFoto);
