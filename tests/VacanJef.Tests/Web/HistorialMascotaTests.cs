using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using VacanJef.Core.Constantes;
using VacanJef.Core.Entities;
using VacanJef.Core.Enums;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Controllers;
using VacanJef.Web.Servicios;

namespace VacanJef.Tests.Web;

// HU-013 / RF-003, RF-009 / RN-006: el dueño consulta la salud y las observaciones de su mascota
public class HistorialMascotaTests
{
    private const int DuenoId = 1;
    private const int CuidadorId = 2;
    private const int OtroDuenoId = 3;
    private const int MascotaId = 10;
    private const int MascotaAjena = 20;
    private const int EstadiaAnterior = 100;
    private const int EstadiaActual = 101;
    private const int ReservaCancelada = 102;

    private static async Task<VacanJefDbContext> CrearContextoAsync()
    {
        var contexto = new VacanJefDbContext(new DbContextOptionsBuilder<VacanJefDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        contexto.Users.AddRange(
            new Usuario { Id = DuenoId, UserName = "ana@ejemplo.com", Nombre = "Ana Pérez" },
            new Usuario { Id = CuidadorId, UserName = "cuidador@ejemplo.com", Nombre = "Laura Cuidadora" },
            new Usuario { Id = OtroDuenoId, UserName = "pedro@ejemplo.com", Nombre = "Pedro Gómez" });

        contexto.Mascotas.AddRange(
            new Mascota { Id = MascotaId, DuenoId = DuenoId, Nombre = "Toby", Raza = "Criollo", Edad = 3, PesoKg = 12.5m },
            new Mascota { Id = MascotaAjena, DuenoId = OtroDuenoId, Nombre = "Luna", Raza = "Beagle", Edad = 2, PesoKg = 9m });

        contexto.RegistrosSalud.Add(new RegistroSalud
        {
            MascotaId = MascotaId, Tipo = TipoRegistroSalud.Vacuna, Descripcion = "Rabia", Fecha = new DateOnly(2026, 3, 10),
        });

        contexto.Reservas.AddRange(
            NuevaReserva(EstadiaAnterior, EstadoReserva.Finalizada, new DateOnly(2026, 10, 5)),
            NuevaReserva(EstadiaActual, EstadoReserva.Activa, new DateOnly(2026, 11, 24)),
            NuevaReserva(ReservaCancelada, EstadoReserva.Cancelada, new DateOnly(2026, 12, 20)));

        await contexto.SaveChangesAsync();
        return contexto;
    }

    private static Reserva NuevaReserva(int id, EstadoReserva estado, DateOnly ingreso) => new()
    {
        Id = id,
        MascotaId = MascotaId,
        DuenoId = DuenoId,
        FechaIngreso = ingreso,
        FechaSalida = ingreso.AddDays(3),
        Servicio = TipoServicio.Basico,
        CostoTotal = 150000m,
        Estado = estado,
    };

    private static Observacion NuevaObservacion(int reservaId, string detalle, DateTime creadoEn, int? corrigeA = null) => new()
    {
        ReservaId = reservaId,
        AutorId = CuidadorId,
        Tipo = TipoObservacion.Salud,
        Detalle = detalle,
        CreadoEn = creadoEn,
        CorrigeAId = corrigeA,
    };

    [Fact]
    public async Task Historial_MuestraSaludYObservacionesDeLaMasRecienteALaMasAntigua()
    {
        // CA-HU-013-01
        await using var contexto = await CrearContextoAsync();
        contexto.Observaciones.AddRange(
            NuevaObservacion(EstadiaActual, "Mañana", new DateTime(2026, 11, 24, 13, 0, 0, DateTimeKind.Utc)),
            NuevaObservacion(EstadiaActual, "Tarde", new DateTime(2026, 11, 24, 20, 0, 0, DateTimeKind.Utc)));
        await contexto.SaveChangesAsync();

        var historial = await new ServicioHistorialMascota(contexto).HistorialAsync(MascotaId, DuenoId);

        Assert.NotNull(historial);
        Assert.Equal("Toby", historial.Nombre);
        Assert.Single(historial.Salud);
        var actual = historial.Estadias.Single(e => e.ReservaId == EstadiaActual);
        Assert.Equal(new[] { "Tarde", "Mañana" }, actual.Observaciones.Select(o => o.Detalle));
        Assert.All(actual.Observaciones, o => Assert.Equal("Laura Cuidadora", o.Autor));
    }

    [Fact]
    public async Task Historial_OrdenaLasEstadiasDeLaMasRecienteALaMasAntiguaYOmiteLasCanceladas()
    {
        await using var contexto = await CrearContextoAsync();

        var historial = await new ServicioHistorialMascota(contexto).HistorialAsync(MascotaId, DuenoId);

        Assert.Equal(new[] { EstadiaActual, EstadiaAnterior }, historial!.Estadias.Select(e => e.ReservaId));
    }

    [Fact]
    public async Task Historial_ConservaLaObservacionOriginalYSuCorreccion()
    {
        // CA-HU-013-02 / RN-006
        await using var contexto = await CrearContextoAsync();
        var original = NuevaObservacion(EstadiaAnterior, "Vomitó una vez.", new DateTime(2026, 10, 6, 14, 0, 0, DateTimeKind.Utc));
        contexto.Observaciones.Add(original);
        await contexto.SaveChangesAsync();
        contexto.Observaciones.Add(NuevaObservacion(EstadiaAnterior, "Corrección: fue otra mascota.",
            new DateTime(2026, 10, 6, 15, 0, 0, DateTimeKind.Utc), original.Id));
        await contexto.SaveChangesAsync();

        var historial = await new ServicioHistorialMascota(contexto).HistorialAsync(MascotaId, DuenoId);

        var observaciones = historial!.Estadias.Single(e => e.ReservaId == EstadiaAnterior).Observaciones;
        Assert.Equal(2, observaciones.Count);
        Assert.Contains(observaciones, o => o.Id == original.Id && o.Detalle == "Vomitó una vez.");
        Assert.Contains(observaciones, o => o.CorrigeAId == original.Id);
    }

    [Fact]
    public async Task Historial_SinObservaciones_LoIndica()
    {
        // CA-HU-013-03
        await using var contexto = await CrearContextoAsync();

        var historial = await new ServicioHistorialMascota(contexto).HistorialAsync(MascotaId, DuenoId);

        Assert.NotNull(historial);
        Assert.False(historial.TieneObservaciones);
    }

    [Fact]
    public async Task Historial_DeLaMascotaDeOtroDueno_NoSeEncuentra()
    {
        // CA-HU-013-04
        await using var contexto = await CrearContextoAsync();

        var historial = await new ServicioHistorialMascota(contexto).HistorialAsync(MascotaAjena, DuenoId);

        Assert.Null(historial);
    }

    [Fact]
    public async Task Historial_DeUnaMascotaInexistente_NoSeEncuentra()
    {
        await using var contexto = await CrearContextoAsync();

        Assert.Null(await new ServicioHistorialMascota(contexto).HistorialAsync(9999, DuenoId));
    }

    [Fact]
    public void Controlador_SoloParaDuenos()
    {
        var roles = typeof(HistorialController).GetCustomAttribute<AuthorizeAttribute>()!.Roles!.Split(',');

        Assert.Equal(new[] { Roles.Dueno }, roles);
    }
}
