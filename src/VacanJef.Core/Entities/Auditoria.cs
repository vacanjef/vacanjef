namespace VacanJef.Core.Entities;

// DAT-AUDITORIA: accesos a cámaras y cambios de reservas
public class Auditoria
{
    public int Id { get; set; }
    public int? UsuarioId { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string Recurso { get; set; } = string.Empty;
    public string Resultado { get; set; } = string.Empty; // permitido / denegado
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public Usuario? Usuario { get; set; }
}
