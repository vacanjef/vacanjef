using System.Data;
using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VacanJef.Core.Entities;
using VacanJef.Core.Enums;
using VacanJef.Infrastructure.Data;

namespace VacanJef.Web.Servicios;

public record EstadiaPanel(
    int ReservaId,
    string Mascota,
    string Raza,
    string Dueno,
    DateOnly FechaIngreso,
    DateOnly FechaSalida,
    TipoServicio Servicio,
    EstadoReserva Estado,
    int? CamaraId,
    string? Camara,
    DateTime? IngresoReal);

public record CamaraDisponible(int Id, string Nombre);

public record ResultadoEstadia(bool Exitoso, bool NoEncontrada, string Mensaje)
{
    public static ResultadoEstadia Ok(string mensaje) => new(true, false, mensaje);
    public static ResultadoEstadia Error(string mensaje) => new(false, false, mensaje);
    public static ResultadoEstadia NoExiste() => new(false, true, "La reserva no existe.");
}

// HU-014 / RF-008: el cuidador o el administrador confirman el ingreso y la salida de cada mascota
// y le asignan una cámara. Confirmar el ingreso deja la reserva Activa (así se pueden registrar
// observaciones, HU-008); confirmar la salida la deja Finalizada y libera la cámara.
// Una cámara está ocupada mientras esté asignada a una reserva Activa.
// Cada acción queda en Auditorias (CA-HU-014-05).
public class ServicioEstadias(VacanJefDbContext contexto)
{
    public const string AccionConfirmarIngreso = "ConfirmarIngreso";
    public const string AccionAsignarCamara = "AsignarCamara";
    public const string AccionConfirmarSalida = "ConfirmarSalida";

    public static string RecursoReserva(int reservaId) => $"Reserva #{reservaId}";

    // CA-HU-014-01: llegadas de hoy (y las atrasadas cuya salida aún no pasa)
    public Task<List<EstadiaPanel>> LlegadasAsync(DateOnly hoy) =>
        ListarAsync(r => r.Estado == EstadoReserva.Confirmada && r.FechaIngreso <= hoy && r.FechaSalida >= hoy);

    // CA-HU-014-01: estadías en curso
    public Task<List<EstadiaPanel>> EnCursoAsync() =>
        ListarAsync(r => r.Estado == EstadoReserva.Activa);

    // Cámaras activas que no están asignadas a ninguna estadía en curso
    public Task<List<CamaraDisponible>> CamarasDisponiblesAsync() =>
        contexto.Camaras
            .AsNoTracking()
            .Where(c => c.Activa && !c.Reservas.Any(r => r.Estado == EstadoReserva.Activa))
            .OrderBy(c => c.Nombre)
            .Select(c => new CamaraDisponible(c.Id, c.Nombre))
            .ToListAsync();

    // CA-HU-014-02 y CA-HU-014-03
    public Task<ResultadoEstadia> ConfirmarIngresoAsync(int reservaId, int usuarioId, int? camaraId, DateOnly hoy) =>
        EnTransaccionAsync(async () =>
        {
            var reserva = await contexto.Reservas
                .Include(r => r.Mascota)
                .FirstOrDefaultAsync(r => r.Id == reservaId);

            if (reserva is null)
                return ResultadoEstadia.NoExiste();

            var mascota = reserva.Mascota?.Nombre ?? "la mascota";

            if (reserva.Estado != EstadoReserva.Confirmada)
                return ResultadoEstadia.Error("Solo se confirma el ingreso de una reserva en estado Confirmada.");

            if (reserva.FechaIngreso > hoy)
                return ResultadoEstadia.Error(
                    $"El ingreso de {mascota} está programado para el {Fecha(reserva.FechaIngreso)}; todavía no se puede confirmar.");

            if (reserva.FechaSalida < hoy)
                return ResultadoEstadia.Error("La fecha de salida de esta reserva ya pasó; no se puede confirmar el ingreso.");

            if (camaraId is int camara && !await CamaraDisponibleAsync(camara, reservaId))
                return ResultadoEstadia.Error("La cámara elegida no está disponible: está desactivada o la usa otra estadía.");

            reserva.Estado = EstadoReserva.Activa;
            reserva.IngresoReal = DateTime.UtcNow;
            reserva.CamaraId = camaraId;

            Auditar(usuarioId, AccionConfirmarIngreso, RecursoReserva(reservaId));
            if (camaraId is int asignada)
                Auditar(usuarioId, AccionAsignarCamara, RecursoCamara(reservaId, asignada));

            await contexto.SaveChangesAsync();

            return ResultadoEstadia.Ok(camaraId is null
                ? $"Se confirmó el ingreso de {mascota}. Asígnele una cámara para que el dueño pueda verla."
                : $"Se confirmó el ingreso de {mascota} y se asignó la cámara.");
        });

    // CA-HU-014-03: asignar o cambiar la cámara de una estadía en curso
    public Task<ResultadoEstadia> AsignarCamaraAsync(int reservaId, int usuarioId, int camaraId) =>
        EnTransaccionAsync(async () =>
        {
            var reserva = await contexto.Reservas
                .Include(r => r.Mascota)
                .FirstOrDefaultAsync(r => r.Id == reservaId);

            if (reserva is null)
                return ResultadoEstadia.NoExiste();

            if (reserva.Estado != EstadoReserva.Activa)
                return ResultadoEstadia.Error("Solo se asigna una cámara a una estadía en curso (después de confirmar el ingreso).");

            if (reserva.CamaraId == camaraId)
                return ResultadoEstadia.Ok("Esa cámara ya estaba asignada a esta estadía.");

            if (!await CamaraDisponibleAsync(camaraId, reservaId))
                return ResultadoEstadia.Error("La cámara elegida no está disponible: está desactivada o la usa otra estadía.");

            reserva.CamaraId = camaraId;
            Auditar(usuarioId, AccionAsignarCamara, RecursoCamara(reservaId, camaraId));
            await contexto.SaveChangesAsync();

            return ResultadoEstadia.Ok($"Se asignó la cámara a la estadía de {reserva.Mascota?.Nombre ?? "la mascota"}.");
        });

    // CA-HU-014-04: la reserva queda Finalizada. La cámara se conserva en la reserva para el historial,
    // pero queda libre porque solo las reservas Activas ocupan cámara.
    public Task<ResultadoEstadia> ConfirmarSalidaAsync(int reservaId, int usuarioId) =>
        EnTransaccionAsync(async () =>
        {
            var reserva = await contexto.Reservas
                .Include(r => r.Mascota)
                .FirstOrDefaultAsync(r => r.Id == reservaId);

            if (reserva is null)
                return ResultadoEstadia.NoExiste();

            if (reserva.Estado != EstadoReserva.Activa)
                return ResultadoEstadia.Error("Solo se confirma la salida de una estadía en curso.");

            reserva.Estado = EstadoReserva.Finalizada;
            reserva.SalidaReal = DateTime.UtcNow;

            Auditar(usuarioId, AccionConfirmarSalida, RecursoReserva(reservaId));
            await contexto.SaveChangesAsync();

            return ResultadoEstadia.Ok(
                $"Se confirmó la salida de {reserva.Mascota?.Nombre ?? "la mascota"}." +
                (reserva.CamaraId is null ? string.Empty : " La cámara quedó libre."));
        });

    private Task<List<EstadiaPanel>> ListarAsync(Expression<Func<Reserva, bool>> filtro) =>
        contexto.Reservas
            .AsNoTracking()
            .Where(filtro)
            .OrderBy(r => r.FechaIngreso)
            .ThenBy(r => r.Mascota!.Nombre)
            .Select(r => new EstadiaPanel(
                r.Id,
                r.Mascota!.Nombre,
                r.Mascota.Raza,
                r.Dueno!.Nombre,
                r.FechaIngreso,
                r.FechaSalida,
                r.Servicio,
                r.Estado,
                r.CamaraId,
                r.Camara != null ? r.Camara.Nombre : null,
                r.IngresoReal))
            .ToListAsync();

    private Task<bool> CamaraDisponibleAsync(int camaraId, int reservaId) =>
        contexto.Camaras.AnyAsync(c =>
            c.Id == camaraId
            && c.Activa
            && !c.Reservas.Any(r => r.Estado == EstadoReserva.Activa && r.Id != reservaId));

    // Con SQL Server, aislamiento Serializable: dos cuidadores no pueden tomar la misma cámara
    // al mismo tiempo. Va dentro de CreateExecutionStrategy por los reintentos de Azure SQL.
    private async Task<ResultadoEstadia> EnTransaccionAsync(Func<Task<ResultadoEstadia>> operacion)
    {
        var estrategia = contexto.Database.CreateExecutionStrategy();

        return await estrategia.ExecuteAsync(async () =>
        {
            contexto.ChangeTracker.Clear();

            await using IDbContextTransaction? transaccion = contexto.Database.IsRelational()
                ? await contexto.Database.BeginTransactionAsync(IsolationLevel.Serializable)
                : null;

            var resultado = await operacion();

            if (resultado.Exitoso && transaccion is not null)
                await transaccion.CommitAsync();

            return resultado;
        });
    }

    private void Auditar(int usuarioId, string accion, string recurso) =>
        contexto.Auditorias.Add(new Auditoria
        {
            UsuarioId = usuarioId,
            Accion = accion,
            Recurso = recurso,
            Resultado = "permitido",
            CreadoEn = DateTime.UtcNow,
        });

    private static string RecursoCamara(int reservaId, int camaraId) => $"{RecursoReserva(reservaId)} / Camara #{camaraId}";

    private static string Fecha(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
