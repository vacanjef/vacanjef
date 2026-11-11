using Microsoft.EntityFrameworkCore;
using VacanJef.Core.Entities;
using VacanJef.Core.Enums;
using VacanJef.Infrastructure.Data;

namespace VacanJef.Tests.Infrastructure;

// RN-006: las observaciones de salud no se modifican ni se eliminan
public class ObservacionesInmodificablesTests
{
    private static VacanJefDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<VacanJefDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Observacion NuevaObservacion() => new()
    {
        ReservaId = 1,
        AutorId = 1,
        Tipo = TipoObservacion.Salud,
        Detalle = "Comió bien y está activo",
    };

    [Fact]
    public async Task RegistrarObservacion_SeGuarda()
    {
        using var db = CrearContexto();
        db.Observaciones.Add(NuevaObservacion());
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.Observaciones.CountAsync());
    }

    [Fact]
    public async Task ModificarObservacion_SeRechaza()
    {
        using var db = CrearContexto();
        var observacion = NuevaObservacion();
        db.Observaciones.Add(observacion);
        await db.SaveChangesAsync();

        observacion.Detalle = "Texto cambiado";

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task EliminarObservacion_SeRechaza()
    {
        using var db = CrearContexto();
        var observacion = NuevaObservacion();
        db.Observaciones.Add(observacion);
        await db.SaveChangesAsync();

        db.Observaciones.Remove(observacion);

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task CorregirConNuevaObservacion_SePermite()
    {
        using var db = CrearContexto();
        var original = NuevaObservacion();
        db.Observaciones.Add(original);
        await db.SaveChangesAsync();

        db.Observaciones.Add(new Observacion
        {
            ReservaId = 1,
            AutorId = 1,
            Tipo = TipoObservacion.Salud,
            Detalle = "Corrección: comió la mitad de la ración",
            CorrigeAId = original.Id,
        });
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.Observaciones.CountAsync());
    }
}
