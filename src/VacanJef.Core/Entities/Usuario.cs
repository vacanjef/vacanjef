using Microsoft.AspNetCore.Identity;

namespace VacanJef.Core.Entities;

// DAT-USUARIO. Identity ya maneja correo, teléfono, hash de contraseña,
// intentos fallidos y bloqueo temporal (RN-005).
public class Usuario : IdentityUser<int>
{
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public ICollection<Mascota> Mascotas { get; set; } = new List<Mascota>();
    public ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
}
