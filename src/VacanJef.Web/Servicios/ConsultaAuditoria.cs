using Microsoft.EntityFrameworkCore;
using VacanJef.Core.Entities;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Seguridad;

namespace VacanJef.Web.Servicios;

public record RegistroAuditoria(
    int Id, DateTime Fecha, string Autor, string Accion, string Descripcion, string Recurso, string Resultado);

public record SolicitudSupresion(int UsuarioId, string Nombre, string Correo, DateTime Fecha, DateTime FechaLimite);

public record FiltroAuditoria(string? Accion = null, DateOnly? Desde = null, DateOnly? Hasta = null, string? Texto = null);

// HU-017: el administrador consulta la auditoría y atiende las solicitudes de supresión de datos (TT-004).
// Los registros de auditoría nunca se modifican: atender una solicitud agrega un registro nuevo.
public class ConsultaAuditoria(VacanJefDbContext contexto)
{
    public const string AccionSupresionAtendida = "SupresionAtendida";
    public const int MaximoRegistros = 200;

    // Acciones conocidas con su descripción en español (las de otras historias se nombran por su texto)
    private static readonly IReadOnlyDictionary<string, string> Descripciones = new Dictionary<string, string>
    {
        [PoliticaDatos.AccionAutorizacion] = "Autorizó el tratamiento de datos",
        [PoliticaDatos.AccionActualizacion] = "Actualizó sus datos personales",
        [PoliticaDatos.AccionSolicitudSupresion] = "Solicitó la supresión de sus datos",
        [AccionSupresionAtendida] = "Atendió una solicitud de supresión",
        [GestionUsuarios.AccionCrear] = "Creó una cuenta de personal",
        [GestionUsuarios.AccionCambiarRol] = "Cambió el rol de un usuario",
        [GestionUsuarios.AccionActivar] = "Activó una cuenta",
        [GestionUsuarios.AccionDesactivar] = "Desactivó una cuenta",
        [GestionUsuarios.AccionDesbloquear] = "Desbloqueó una cuenta",
        ["CambioContrasena"] = "Cambió su contraseña",
        ["RestablecerContrasena"] = "Restableció la contraseña de un usuario",
        [ServicioEstadias.AccionConfirmarIngreso] = "Confirmó el ingreso de una mascota",
        [ServicioEstadias.AccionAsignarCamara] = "Asignó una cámara a una estadía",
        [ServicioEstadias.AccionConfirmarSalida] = "Confirmó la salida de una mascota",
        [ServicioCamaras.AccionCrear] = "Registró una cámara",
        [ServicioCamaras.AccionActivar] = "Activó una cámara",
        [ServicioCamaras.AccionDesactivar] = "Desactivó una cámara",
    };

    public static IEnumerable<KeyValuePair<string, string>> AccionesConocidas => Descripciones.OrderBy(d => d.Value);

    public static string Describir(string accion) => Descripciones.GetValueOrDefault(accion, accion);

    // Plazo de la Ley 1581 (art. 15) en días hábiles. No descuenta festivos: es una fecha de referencia.
    public static DateTime SumarDiasHabiles(DateTime desde, int dias)
    {
        var fecha = desde;
        while (dias > 0)
        {
            fecha = fecha.AddDays(1);
            if (fecha.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                dias--;
        }
        return fecha;
    }

    public async Task<IReadOnlyList<RegistroAuditoria>> ListarAsync(FiltroAuditoria filtro)
    {
        var consulta = from a in contexto.Auditorias.AsNoTracking()
                       join u in contexto.Users.AsNoTracking() on a.UsuarioId equals u.Id into autores
                       from u in autores.DefaultIfEmpty()
                       select new { a, Nombre = u == null ? null : u.Nombre, Correo = u == null ? null : u.Email };

        if (!string.IsNullOrWhiteSpace(filtro.Accion))
            consulta = consulta.Where(x => x.a.Accion == filtro.Accion);

        if (filtro.Desde is DateOnly desde)
        {
            var inicio = desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
            consulta = consulta.Where(x => x.a.CreadoEn >= inicio);
        }

        if (filtro.Hasta is DateOnly hasta)
        {
            var fin = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
            consulta = consulta.Where(x => x.a.CreadoEn < fin);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();
            consulta = consulta.Where(x =>
                x.a.Recurso.Contains(texto)
                || (x.Nombre != null && x.Nombre.Contains(texto))
                || (x.Correo != null && x.Correo.Contains(texto)));
        }

        var filas = await consulta
            .OrderByDescending(x => x.a.CreadoEn)
            .ThenByDescending(x => x.a.Id)
            .Take(MaximoRegistros)
            .ToListAsync();

        return filas
            .Select(x => new RegistroAuditoria(
                x.a.Id,
                x.a.CreadoEn,
                x.Nombre is null ? "—" : $"{x.Nombre} ({x.Correo})",
                x.a.Accion,
                Describir(x.a.Accion),
                x.a.Recurso,
                x.a.Resultado))
            .ToList();
    }

    // Una solicitud está pendiente si no hay un registro de atención posterior para ese titular
    public async Task<IReadOnlyList<SolicitudSupresion>> SolicitudesPendientesAsync()
    {
        var solicitudes = await contexto.Auditorias.AsNoTracking()
            .Where(a => a.Accion == PoliticaDatos.AccionSolicitudSupresion && a.UsuarioId != null)
            .GroupBy(a => a.UsuarioId!.Value)
            .Select(g => new { UsuarioId = g.Key, Fecha = g.Max(a => a.CreadoEn) })
            .ToListAsync();

        var atenciones = await contexto.Auditorias.AsNoTracking()
            .Where(a => a.Accion == AccionSupresionAtendida)
            .Select(a => new { a.Recurso, a.CreadoEn })
            .ToListAsync();

        var pendientes = solicitudes
            .Where(s => !atenciones.Any(at => at.Recurso == RecursoUsuario(s.UsuarioId) && at.CreadoEn >= s.Fecha))
            .ToList();

        var ids = pendientes.Select(p => p.UsuarioId).ToList();
        var usuarios = await contexto.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        return pendientes
            .OrderBy(p => p.Fecha)
            .Select(p => new SolicitudSupresion(
                p.UsuarioId,
                usuarios.TryGetValue(p.UsuarioId, out var u) ? u.Nombre : "Usuario eliminado",
                usuarios.TryGetValue(p.UsuarioId, out var c) ? c.Email ?? string.Empty : string.Empty,
                p.Fecha,
                SumarDiasHabiles(p.Fecha, PoliticaDatos.DiasHabilesReclamo)))
            .ToList();
    }

    public async Task<ResultadoGestion> MarcarAtendidaAsync(int titularId, int administradorId)
    {
        if (!(await SolicitudesPendientesAsync()).Any(s => s.UsuarioId == titularId))
            return ResultadoGestion.Error("Ese usuario no tiene solicitudes de supresión pendientes.");

        contexto.Auditorias.Add(new Auditoria
        {
            UsuarioId = administradorId,
            Accion = AccionSupresionAtendida,
            Recurso = RecursoUsuario(titularId),
            Resultado = "permitido",
        });
        await contexto.SaveChangesAsync();

        return ResultadoGestion.Ok("La solicitud quedó marcada como atendida.");
    }

    public static string RecursoUsuario(int usuarioId) => $"Usuario #{usuarioId}";
}
