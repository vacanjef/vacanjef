using Microsoft.EntityFrameworkCore;
using VacanJef.Core.Entities;
using VacanJef.Core.Enums;
using VacanJef.Infrastructure.Data;

namespace VacanJef.Web.Servicios;

// MascotaActual: la mascota cuya estadía en curso usa la cámara (null si está libre)
public record CamaraResumen(int Id, string Nombre, bool Activa, string? MascotaActual);

// HU-014: registro mínimo de cámaras para poder asignarlas a las estadías.
// La dirección del video (UrlStream) es un dato sensible: se guarda, pero nunca se muestra
// ni se envía al navegador (el monitoreo en vivo de HU-007 usará un token temporal, RN-004).
public class ServicioCamaras(VacanJefDbContext contexto)
{
    public const string AccionCrear = "CrearCamara";
    public const string AccionActivar = "ActivarCamara";
    public const string AccionDesactivar = "DesactivarCamara";

    public const int NombreMaximo = 80;
    public const int UrlMaxima = 255;

    public static readonly IReadOnlyList<string> EsquemasPermitidos = ["rtsp://", "http://", "https://"];

    public static string RecursoCamara(int camaraId) => $"Camara #{camaraId}";

    public Task<List<CamaraResumen>> ListarAsync() =>
        contexto.Camaras
            .AsNoTracking()
            .OrderBy(c => c.Nombre)
            .Select(c => new CamaraResumen(
                c.Id,
                c.Nombre,
                c.Activa,
                c.Reservas
                    .Where(r => r.Estado == EstadoReserva.Activa)
                    .Select(r => r.Mascota!.Nombre)
                    .FirstOrDefault()))
            .ToListAsync();

    public async Task<ResultadoGestion> CrearAsync(string? nombre, string? urlStream, int administradorId)
    {
        var nombreLimpio = (nombre ?? string.Empty).Trim();
        var url = (urlStream ?? string.Empty).Trim();

        if (nombreLimpio.Length == 0 || nombreLimpio.Length > NombreMaximo)
            return ResultadoGestion.Error($"El nombre es obligatorio y admite máximo {NombreMaximo} caracteres.");

        if (url.Length == 0 || url.Length > UrlMaxima)
            return ResultadoGestion.Error($"La dirección del video es obligatoria y admite máximo {UrlMaxima} caracteres.");

        if (!EsquemasPermitidos.Any(e => url.StartsWith(e, StringComparison.OrdinalIgnoreCase)))
            return ResultadoGestion.Error("La dirección del video debe empezar por rtsp://, http:// o https://.");

        if (await contexto.Camaras.AnyAsync(c => c.Nombre == nombreLimpio))
            return ResultadoGestion.Error("Ya existe una cámara con ese nombre.");

        var camara = new Camara { Nombre = nombreLimpio, UrlStream = url, Activa = true };
        contexto.Camaras.Add(camara);
        await contexto.SaveChangesAsync();

        await AuditarAsync(administradorId, AccionCrear, camara.Id);
        return ResultadoGestion.Ok($"Se registró la cámara {camara.Nombre}.");
    }

    public async Task<ResultadoGestion> CambiarEstadoAsync(int camaraId, bool activa, int administradorId)
    {
        var camara = await contexto.Camaras.FirstOrDefaultAsync(c => c.Id == camaraId);
        if (camara is null)
            return ResultadoGestion.Error("La cámara no existe.");

        if (!activa && await contexto.Reservas.AnyAsync(r => r.CamaraId == camaraId && r.Estado == EstadoReserva.Activa))
            return ResultadoGestion.Error("No se puede desactivar: la cámara está asignada a una estadía en curso. Cámbiela primero en Estadías.");

        camara.Activa = activa;
        await contexto.SaveChangesAsync();

        await AuditarAsync(administradorId, activa ? AccionActivar : AccionDesactivar, camara.Id);
        return ResultadoGestion.Ok(activa ? $"Se activó la cámara {camara.Nombre}." : $"Se desactivó la cámara {camara.Nombre}.");
    }

    private async Task AuditarAsync(int administradorId, string accion, int camaraId)
    {
        contexto.Auditorias.Add(new Auditoria
        {
            UsuarioId = administradorId,
            Accion = accion,
            Recurso = RecursoCamara(camaraId),
            Resultado = "permitido",
            CreadoEn = DateTime.UtcNow,
        });
        await contexto.SaveChangesAsync();
    }
}
