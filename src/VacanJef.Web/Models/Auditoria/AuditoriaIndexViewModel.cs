using VacanJef.Web.Servicios;

namespace VacanJef.Web.Models.Auditoria;

// HU-017: panel de auditoría
public class AuditoriaIndexViewModel
{
    public IReadOnlyList<SolicitudSupresion> Pendientes { get; init; } = [];
    public IReadOnlyList<RegistroAuditoria> Registros { get; init; } = [];
    public FiltroAuditoria Filtro { get; init; } = new();
}
