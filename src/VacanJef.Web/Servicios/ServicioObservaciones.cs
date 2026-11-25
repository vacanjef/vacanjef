using Microsoft.EntityFrameworkCore;
using VacanJef.Core.Entities;
using VacanJef.Core.Enums;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Models.Observaciones;

namespace VacanJef.Web.Servicios;

// HU-008 / RF-009: observaciones de salud y comportamiento durante una estadía.
// RN-006: solo se agregan. Una corrección es una observación nueva que apunta a la original (CorrigeAId).
// La protección contra modificar o eliminar está en VacanJefDbContext.SaveChanges.
public class ServicioObservaciones(VacanJefDbContext contexto)
{
    public const int MaximoDetalle = 1000;

    public Task<List<EstadiaActivaViewModel>> EstadiasActivasAsync() =>
        contexto.Reservas
            .AsNoTracking()
            .Where(r => r.Estado == EstadoReserva.Activa)
            .OrderBy(r => r.Mascota!.Nombre)
            .Select(r => new EstadiaActivaViewModel(
                r.Id,
                r.Mascota!.Nombre,
                r.Mascota.Raza,
                r.Dueno!.Nombre,
                r.FechaIngreso,
                r.FechaSalida,
                r.Observaciones.Count,
                r.Observaciones.Max(o => (DateTime?)o.CreadoEn)))
            .ToListAsync();

    // Matriz de roles 8.2: el cuidador solo consulta estadías activas; el administrador, todas.
    public async Task<EstadiaDetalleViewModel?> EstadiaAsync(int reservaId, bool incluirNoActivas)
    {
        var estadia = await contexto.Reservas
            .AsNoTracking()
            .Where(r => r.Id == reservaId && (incluirNoActivas || r.Estado == EstadoReserva.Activa))
            .Select(r => new EstadiaDetalleViewModel
            {
                ReservaId = r.Id,
                MascotaId = r.MascotaId,
                Mascota = r.Mascota!.Nombre,
                Raza = r.Mascota.Raza,
                Dueno = r.Dueno!.Nombre,
                FechaIngreso = r.FechaIngreso,
                FechaSalida = r.FechaSalida,
                Estado = r.Estado,
            })
            .FirstOrDefaultAsync();

        if (estadia is null)
            return null;

        estadia.Salud = await contexto.RegistrosSalud
            .AsNoTracking()
            .Where(s => s.MascotaId == estadia.MascotaId)
            .OrderBy(s => s.Tipo).ThenBy(s => s.Descripcion)
            .Select(s => new RegistroSaludResumen(s.Tipo, s.Descripcion, s.Fecha))
            .ToListAsync();

        estadia.Observaciones = await contexto.Observaciones
            .AsNoTracking()
            .Where(o => o.ReservaId == reservaId)
            .OrderByDescending(o => o.CreadoEn).ThenByDescending(o => o.Id)
            .Select(o => new ObservacionItemViewModel(o.Id, o.Tipo, o.Detalle, o.CreadoEn, o.Autor!.Nombre, o.CorrigeAId))
            .ToListAsync();

        return estadia;
    }

    public async Task<ResultadoObservacion> RegistrarAsync(
        int reservaId, int autorId, TipoObservacion tipo, string? detalle, int? corrigeAId)
    {
        var texto = (detalle ?? string.Empty).Trim();

        if (texto.Length == 0)
            return ResultadoObservacion.Fallo(nameof(NuevaObservacionViewModel.Detalle), "Escriba el detalle de la observación.");

        if (texto.Length > MaximoDetalle)
            return ResultadoObservacion.Fallo(nameof(NuevaObservacionViewModel.Detalle),
                $"El detalle admite máximo {MaximoDetalle} caracteres.");

        var estado = await contexto.Reservas
            .Where(r => r.Id == reservaId)
            .Select(r => (EstadoReserva?)r.Estado)
            .FirstOrDefaultAsync();

        if (estado is null)
            return ResultadoObservacion.NoEncontrada();

        // CA-HU-008-02
        if (estado != EstadoReserva.Activa)
            return ResultadoObservacion.Fallo(string.Empty,
                "Solo se registran observaciones durante una estadía activa (después de confirmar el ingreso de la mascota).");

        // RN-006: la corrección debe apuntar a una observación de la misma estadía
        if (corrigeAId is int originalId &&
            !await contexto.Observaciones.AnyAsync(o => o.Id == originalId && o.ReservaId == reservaId))
            return ResultadoObservacion.Fallo(string.Empty, "La observación que intenta corregir no pertenece a esta estadía.");

        var observacion = new Observacion
        {
            ReservaId = reservaId,
            AutorId = autorId,
            Tipo = tipo,
            Detalle = texto,
            CorrigeAId = corrigeAId,
            CreadoEn = DateTime.UtcNow,
        };

        contexto.Observaciones.Add(observacion);
        await contexto.SaveChangesAsync();
        return ResultadoObservacion.Exito(observacion);
    }
}

public record ResultadoObservacion(bool Exitoso, bool ReservaNoEncontrada, string Campo, string Mensaje, Observacion? Observacion)
{
    public static ResultadoObservacion Exito(Observacion observacion) => new(true, false, string.Empty, string.Empty, observacion);
    public static ResultadoObservacion Fallo(string campo, string mensaje) => new(false, false, campo, mensaje, null);
    public static ResultadoObservacion NoEncontrada() => new(false, true, string.Empty, "La estadía no existe.", null);
}
