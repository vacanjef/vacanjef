using Microsoft.EntityFrameworkCore;
using VacanJef.Core.Enums;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Models.Historial;
using VacanJef.Web.Models.Observaciones;

namespace VacanJef.Web.Servicios;

// HU-013 / RF-003, RF-009: el dueño consulta la salud de su mascota y las observaciones de sus estadías.
// Solo lectura: RN-006 se respeta mostrando la original y su corrección.
public class ServicioHistorialMascota(VacanJefDbContext contexto)
{
    // Estadías que ya empezaron. Una reserva Confirmada o Cancelada no tiene observaciones (HU-008).
    private static readonly EstadoReserva[] EstadosConEstadia =
        [EstadoReserva.Activa, EstadoReserva.Finalizada, EstadoReserva.Pagada];

    // CA-HU-013-04: devuelve null si la mascota no existe o es de otro dueño (el controlador responde 404)
    public async Task<HistorialMascotaViewModel?> HistorialAsync(int mascotaId, int duenoId)
    {
        var historial = await contexto.Mascotas
            .AsNoTracking()
            .Where(m => m.Id == mascotaId && m.DuenoId == duenoId)
            .Select(m => new HistorialMascotaViewModel
            {
                MascotaId = m.Id,
                Nombre = m.Nombre,
                Raza = m.Raza,
                Edad = m.Edad,
            })
            .FirstOrDefaultAsync();

        if (historial is null)
            return null;

        historial.Salud = await contexto.RegistrosSalud
            .AsNoTracking()
            .Where(s => s.MascotaId == mascotaId)
            .OrderBy(s => s.Tipo).ThenBy(s => s.Descripcion)
            .Select(s => new RegistroSaludResumen(s.Tipo, s.Descripcion, s.Fecha))
            .ToListAsync();

        historial.Estadias = await contexto.Reservas
            .AsNoTracking()
            .Where(r => r.MascotaId == mascotaId && r.DuenoId == duenoId && EstadosConEstadia.Contains(r.Estado))
            .OrderByDescending(r => r.FechaIngreso).ThenByDescending(r => r.Id)
            .Select(r => new EstadiaHistorialViewModel
            {
                ReservaId = r.Id,
                FechaIngreso = r.FechaIngreso,
                FechaSalida = r.FechaSalida,
                Estado = r.Estado,
            })
            .ToListAsync();

        var reservas = historial.Estadias.Select(e => e.ReservaId).ToList();

        // CA-HU-013-01: de la más reciente a la más antigua, con tipo, fecha y hora y autor
        var observaciones = await contexto.Observaciones
            .AsNoTracking()
            .Where(o => reservas.Contains(o.ReservaId))
            .OrderByDescending(o => o.CreadoEn).ThenByDescending(o => o.Id)
            .Select(o => new { o.ReservaId, Item = new ObservacionItemViewModel(o.Id, o.Tipo, o.Detalle, o.CreadoEn, o.Autor!.Nombre, o.CorrigeAId) })
            .ToListAsync();

        foreach (var estadia in historial.Estadias)
        {
            estadia.Observaciones = observaciones
                .Where(o => o.ReservaId == estadia.ReservaId)
                .Select(o => o.Item)
                .ToList();
        }

        return historial;
    }
}
