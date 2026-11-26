using Microsoft.EntityFrameworkCore;
using VacanJef.Core.Entities;
using VacanJef.Core.Enums;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Models.GestionReservas;

namespace VacanJef.Web.Servicios;

// HU-006 / RF-008: el administrador consulta todas las reservas, ve la ocupación y cancela.
// El ingreso, la salida y la cámara son de HU-014.
public class ServicioGestionReservas(VacanJefDbContext contexto)
{
    public const int MaximoListado = 200;
    public const int DiasOcupacion = 14;
    public const int MaximoTexto = 100;
    public const string AccionCancelar = "ReservaCancelada";

    // CA-HU-006-01
    public async Task<(List<ReservaAdminItem> Reservas, bool HayMas)> ListarAsync(FiltroReservas filtro)
    {
        var consulta = contexto.Reservas.AsNoTracking();

        if (filtro.Estado is EstadoReserva estado)
            consulta = consulta.Where(r => r.Estado == estado);

        // Estadías que se cruzan con el rango pedido
        if (filtro.Desde is DateOnly desde)
            consulta = consulta.Where(r => r.FechaSalida >= desde);
        if (filtro.Hasta is DateOnly hasta)
            consulta = consulta.Where(r => r.FechaIngreso <= hasta);

        var texto = filtro.Texto?.Trim();
        if (!string.IsNullOrEmpty(texto))
        {
            if (texto.Length > MaximoTexto)
                texto = texto[..MaximoTexto];
            consulta = consulta.Where(r =>
                r.Mascota!.Nombre.Contains(texto) ||
                r.Dueno!.Nombre.Contains(texto) ||
                r.Dueno.Email!.Contains(texto));
        }

        var filas = await consulta
            .OrderByDescending(r => r.FechaIngreso).ThenByDescending(r => r.Id)
            .Take(MaximoListado + 1)
            .Select(r => new ReservaAdminItem(
                r.Id,
                r.Mascota!.Nombre,
                r.Dueno!.Nombre,
                r.Dueno.Email ?? string.Empty,
                r.FechaIngreso,
                r.FechaSalida,
                r.Servicio,
                r.CostoTotal,
                r.Estado))
            .ToListAsync();

        var hayMas = filas.Count > MaximoListado;
        return (hayMas ? filas.Take(MaximoListado).ToList() : filas, hayMas);
    }

    // CA-HU-006-04: misma regla de cupo que HU-005 (ServicioReservas.OcupaCupo y DiasOcupados)
    public async Task<List<OcupacionDia>> OcupacionAsync(DateOnly hoy, int capacidad, int dias = DiasOcupacion)
    {
        var fin = hoy.AddDays(dias);

        var reservas = await contexto.Reservas
            .AsNoTracking()
            .Where(r => r.FechaIngreso < fin && r.FechaSalida >= hoy)
            .Select(r => new { r.FechaIngreso, r.FechaSalida, r.Estado })
            .ToListAsync();

        var conteo = new Dictionary<DateOnly, int>();
        foreach (var r in reservas.Where(r => ServicioReservas.OcupaCupo(r.Estado)))
        {
            foreach (var dia in ServicioReservas.DiasOcupados(r.FechaIngreso, r.FechaSalida))
                conteo[dia] = conteo.GetValueOrDefault(dia) + 1;
        }

        return Enumerable.Range(0, dias)
            .Select(i => hoy.AddDays(i))
            .Select(dia => new OcupacionDia(dia, conteo.GetValueOrDefault(dia), capacidad))
            .ToList();
    }

    public Task<ReservaAdminItem?> ObtenerAsync(int reservaId) =>
        contexto.Reservas
            .AsNoTracking()
            .Where(r => r.Id == reservaId)
            .Select(r => new ReservaAdminItem(
                r.Id, r.Mascota!.Nombre, r.Dueno!.Nombre, r.Dueno.Email ?? string.Empty,
                r.FechaIngreso, r.FechaSalida, r.Servicio, r.CostoTotal, r.Estado))
            .FirstOrDefaultAsync();

    // CA-HU-006-02/03: solo Confirmada pasa a Cancelada; queda en la auditoría
    public async Task<ResultadoCancelacion> CancelarAsync(int reservaId, int administradorId)
    {
        var reserva = await contexto.Reservas
            .Include(r => r.Mascota)
            .FirstOrDefaultAsync(r => r.Id == reservaId);

        if (reserva is null)
            return ResultadoCancelacion.NoEncontrada();

        if (reserva.Estado != EstadoReserva.Confirmada)
            return ResultadoCancelacion.Fallo(
                $"No se puede cancelar una reserva en estado {reserva.Estado}. Solo se cancelan reservas Confirmadas.");

        reserva.Estado = EstadoReserva.Cancelada;
        contexto.Auditorias.Add(new Auditoria
        {
            UsuarioId = administradorId,
            Accion = AccionCancelar,
            Recurso = $"Reserva #{reserva.Id}",
            Resultado = "permitido",
            CreadoEn = DateTime.UtcNow,
        });
        await contexto.SaveChangesAsync();

        return ResultadoCancelacion.Exito(reserva.DuenoId, reserva.Mascota?.Nombre ?? "su mascota", reserva.FechaIngreso, reserva.FechaSalida);
    }
}

public record ResultadoCancelacion(
    bool Exitoso, bool ReservaNoEncontrada, string Mensaje,
    int DuenoId, string Mascota, DateOnly FechaIngreso, DateOnly FechaSalida)
{
    public static ResultadoCancelacion Exito(int duenoId, string mascota, DateOnly ingreso, DateOnly salida) =>
        new(true, false, string.Empty, duenoId, mascota, ingreso, salida);

    public static ResultadoCancelacion Fallo(string mensaje) =>
        new(false, false, mensaje, 0, string.Empty, default, default);

    public static ResultadoCancelacion NoEncontrada() =>
        new(false, true, "La reserva no existe.", 0, string.Empty, default, default);
}
