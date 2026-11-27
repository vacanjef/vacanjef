using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using VacanJef.Core.Entities;
using VacanJef.Core.Enums;
using VacanJef.Infrastructure.Data;

namespace VacanJef.Web.Servicios;

// Estadía que el usuario puede ver en el listado de /Monitoreo
public record EstadiaMonitoreo(int ReservaId, int MascotaId, string Mascota, string Dueno, string? Camara);

// Resultado de revisar si un usuario puede ver la cámara de una reserva (RN-004).
// Ruta: el "path" de MediaMTX (ej. camara1). Es null si no hay cámara disponible (CA-HU-007-02).
public record AutorizacionMonitoreo(
    bool Permitido,
    string Motivo,
    int ReservaId,
    int? CamaraId,
    string? Ruta,
    int MascotaId,
    string Mascota,
    bool TieneFoto)
{
    public static AutorizacionMonitoreo Denegada(int reservaId, string motivo, int? camaraId = null) =>
        new(false, motivo, reservaId, camaraId, null, 0, string.Empty, false);
}

// HU-007 / RF-007 / RN-004: monitoreo en vivo.
// El dueño solo ve la cámara de su mascota mientras la estadía está Activa; el cuidador y el
// administrador ven las estadías activas. La dirección de la cámara (UrlStream) nunca sale del
// servidor: el navegador negocia el video con VacanJef, que reenvía la señal a MediaMTX.
public class ServicioMonitoreo(VacanJefDbContext contexto)
{
    public const string AccionAccesoCamara = "AccesoCamara";

    // Solo letras, números, guion, guion bajo y barras: evita rutas raras hacia MediaMTX
    private static readonly Regex RutaValida = new("^[A-Za-z0-9_\\-]+(/[A-Za-z0-9_\\-]+)*$", RegexOptions.Compiled);

    public Task<List<EstadiaMonitoreo>> EstadiasVisiblesAsync(int usuarioId, bool esPersonal) =>
        contexto.Reservas
            .AsNoTracking()
            .Where(r => r.Estado == EstadoReserva.Activa && (esPersonal || r.DuenoId == usuarioId))
            .OrderBy(r => r.Mascota!.Nombre)
            .Select(r => new EstadiaMonitoreo(
                r.Id,
                r.MascotaId,
                r.Mascota!.Nombre,
                r.Dueno!.Nombre,
                r.Camara != null && r.Camara.Activa ? r.Camara.Nombre : null))
            .ToListAsync();

    public async Task<AutorizacionMonitoreo> AutorizarAsync(int reservaId, int usuarioId, bool esPersonal)
    {
        var reserva = await contexto.Reservas
            .AsNoTracking()
            .Where(r => r.Id == reservaId)
            .Select(r => new
            {
                r.DuenoId,
                r.Estado,
                r.CamaraId,
                UrlStream = r.Camara != null && r.Camara.Activa ? r.Camara.UrlStream : null,
                r.MascotaId,
                Mascota = r.Mascota!.Nombre,
                TieneFoto = r.Mascota.FotoUrl != null,
            })
            .FirstOrDefaultAsync();

        if (reserva is null)
            return AutorizacionMonitoreo.Denegada(reservaId, "La reserva no existe.");

        // ESC-HU-007-03: un usuario que no es el dueño no ve la cámara
        if (!esPersonal && reserva.DuenoId != usuarioId)
            return AutorizacionMonitoreo.Denegada(reservaId, "La reserva no pertenece al usuario.", reserva.CamaraId);

        // RN-004: solo durante una estadía activa
        if (reserva.Estado != EstadoReserva.Activa)
            return AutorizacionMonitoreo.Denegada(reservaId, "La reserva no tiene una estadía activa.", reserva.CamaraId);

        return new AutorizacionMonitoreo(
            true,
            "Permitido",
            reservaId,
            reserva.CamaraId,
            RutaMediaMtx(reserva.UrlStream),
            reserva.MascotaId,
            reserva.Mascota,
            reserva.TieneFoto);
    }

    // ESC-HU-007-03 y DAT-09: cada acceso a la cámara queda en Auditoría, permitido o denegado
    public async Task RegistrarAccesoAsync(int usuarioId, AutorizacionMonitoreo autorizacion)
    {
        contexto.Auditorias.Add(new Auditoria
        {
            UsuarioId = usuarioId,
            Accion = AccionAccesoCamara,
            Recurso = autorizacion.CamaraId is int camaraId
                ? $"Camara #{camaraId}"
                : $"Reserva #{autorizacion.ReservaId}",
            Resultado = autorizacion.Permitido ? "permitido" : "denegado",
            CreadoEn = DateTime.UtcNow,
        });
        await contexto.SaveChangesAsync();
    }

    // rtsp://localhost:8554/camara1 -> camara1 (el mismo nombre que usa MediaMTX para WebRTC)
    public static string? RutaMediaMtx(string? urlStream)
    {
        if (string.IsNullOrWhiteSpace(urlStream) || !Uri.TryCreate(urlStream.Trim(), UriKind.Absolute, out var uri))
            return null;

        var ruta = uri.AbsolutePath.Trim('/');
        return RutaValida.IsMatch(ruta) ? ruta : null;
    }
}
