using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using VacanJef.Core.Constantes;
using VacanJef.Core.Entities;
using VacanJef.Core.Enums;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Controllers;
using VacanJef.Web.Models.GestionReservas;
using VacanJef.Web.Servicios;

namespace VacanJef.Tests.Web;

// HU-006 / RF-008: gestión de reservas del administrador
public class GestionReservasTests
{
    private const int AdminId = 1;
    private const int AnaId = 2;
    private const int PedroId = 3;
    private static readonly DateOnly Hoy = new(2026, 11, 26);

    private static async Task<VacanJefDbContext> CrearContextoAsync()
    {
        var contexto = new VacanJefDbContext(new DbContextOptionsBuilder<VacanJefDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        contexto.Users.AddRange(
            new Usuario { Id = AdminId, UserName = "admin@ejemplo.com", Email = "admin@ejemplo.com", Nombre = "Administrador" },
            new Usuario { Id = AnaId, UserName = "ana@ejemplo.com", Email = "ana@ejemplo.com", Nombre = "Ana Pérez" },
            new Usuario { Id = PedroId, UserName = "pedro@ejemplo.com", Email = "pedro@ejemplo.com", Nombre = "Pedro Gómez" });

        contexto.Mascotas.AddRange(
            new Mascota { Id = 10, DuenoId = AnaId, Nombre = "Toby", Raza = "Criollo", Edad = 3, PesoKg = 12m },
            new Mascota { Id = 20, DuenoId = PedroId, Nombre = "Luna", Raza = "Beagle", Edad = 2, PesoKg = 9m });

        contexto.Reservas.AddRange(
            NuevaReserva(100, 10, AnaId, Hoy, Hoy.AddDays(3), EstadoReserva.Confirmada),
            NuevaReserva(101, 20, PedroId, Hoy.AddDays(1), Hoy.AddDays(2), EstadoReserva.Activa),
            NuevaReserva(102, 10, AnaId, Hoy.AddDays(-10), Hoy.AddDays(-8), EstadoReserva.Finalizada),
            NuevaReserva(103, 20, PedroId, Hoy, Hoy.AddDays(5), EstadoReserva.Cancelada));

        await contexto.SaveChangesAsync();
        return contexto;
    }

    private static Reserva NuevaReserva(int id, int mascotaId, int duenoId, DateOnly ingreso, DateOnly salida, EstadoReserva estado) => new()
    {
        Id = id, MascotaId = mascotaId, DuenoId = duenoId, FechaIngreso = ingreso, FechaSalida = salida,
        Servicio = TipoServicio.Basico, CostoTotal = 50000m * Math.Max(1, salida.DayNumber - ingreso.DayNumber), Estado = estado,
    };

    [Fact]
    public async Task Listar_SinFiltros_MuestraTodasDeLaMasRecienteALaMasAntigua()
    {
        // CA-HU-006-01
        await using var contexto = await CrearContextoAsync();

        var (reservas, hayMas) = await new ServicioGestionReservas(contexto).ListarAsync(new FiltroReservas());

        Assert.Equal(4, reservas.Count);
        Assert.False(hayMas);
        Assert.Equal(101, reservas[0].Id);
        Assert.Equal(102, reservas[^1].Id);
        Assert.Equal("Ana Pérez", reservas.Single(r => r.Id == 100).Dueno);
    }

    [Fact]
    public async Task Listar_PorEstado()
    {
        await using var contexto = await CrearContextoAsync();

        var (reservas, _) = await new ServicioGestionReservas(contexto)
            .ListarAsync(new FiltroReservas { Estado = EstadoReserva.Confirmada });

        Assert.Equal(100, Assert.Single(reservas).Id);
    }

    [Fact]
    public async Task Listar_PorTextoDeMascotaODueno()
    {
        await using var contexto = await CrearContextoAsync();
        var servicio = new ServicioGestionReservas(contexto);

        var (porMascota, _) = await servicio.ListarAsync(new FiltroReservas { Texto = "Luna" });
        var (porDueno, _) = await servicio.ListarAsync(new FiltroReservas { Texto = "Ana" });

        Assert.All(porMascota, r => Assert.Equal("Luna", r.Mascota));
        Assert.Equal(2, porMascota.Count);
        Assert.All(porDueno, r => Assert.Equal("Ana Pérez", r.Dueno));
    }

    [Fact]
    public async Task Listar_PorRangoDeFechas_IncluyeLasEstadiasQueSeCruzan()
    {
        await using var contexto = await CrearContextoAsync();

        var (reservas, _) = await new ServicioGestionReservas(contexto)
            .ListarAsync(new FiltroReservas { Desde = Hoy.AddDays(4), Hasta = Hoy.AddDays(6) });

        Assert.Equal(103, Assert.Single(reservas).Id);
    }

    [Fact]
    public async Task Cancelar_UnaReservaConfirmada_QuedaCanceladaYEnLaAuditoria()
    {
        // CA-HU-006-02
        await using var contexto = await CrearContextoAsync();

        var resultado = await new ServicioGestionReservas(contexto).CancelarAsync(100, AdminId);

        Assert.True(resultado.Exitoso);
        Assert.Equal(AnaId, resultado.DuenoId);
        Assert.Equal("Toby", resultado.Mascota);
        Assert.Equal(EstadoReserva.Cancelada, (await contexto.Reservas.FindAsync(100))!.Estado);
        var auditoria = Assert.Single(contexto.Auditorias);
        Assert.Equal(AdminId, auditoria.UsuarioId);
        Assert.Equal(ServicioGestionReservas.AccionCancelar, auditoria.Accion);
        Assert.Equal("Reserva #100", auditoria.Recurso);
    }

    [Theory]
    [InlineData(101)] // Activa
    [InlineData(102)] // Finalizada
    [InlineData(103)] // Cancelada
    public async Task Cancelar_UnaReservaQueNoEstaConfirmada_NoCambiaNada(int reservaId)
    {
        // CA-HU-006-03
        await using var contexto = await CrearContextoAsync();
        var antes = (await contexto.Reservas.AsNoTracking().SingleAsync(r => r.Id == reservaId)).Estado;

        var resultado = await new ServicioGestionReservas(contexto).CancelarAsync(reservaId, AdminId);

        Assert.False(resultado.Exitoso);
        Assert.Contains("Confirmadas", resultado.Mensaje);
        Assert.Equal(antes, (await contexto.Reservas.AsNoTracking().SingleAsync(r => r.Id == reservaId)).Estado);
        Assert.Empty(contexto.Auditorias);
    }

    [Fact]
    public async Task Cancelar_UnaReservaInexistente_NoSeEncuentra()
    {
        await using var contexto = await CrearContextoAsync();

        Assert.True((await new ServicioGestionReservas(contexto).CancelarAsync(9999, AdminId)).ReservaNoEncontrada);
    }

    [Fact]
    public async Task Ocupacion_CuentaSoloLasReservasQueOcupanCupo()
    {
        // CA-HU-006-04: la Confirmada (hoy a hoy+3) y la Activa (hoy+1 a hoy+2) ocupan; la Cancelada no
        await using var contexto = await CrearContextoAsync();

        var ocupacion = await new ServicioGestionReservas(contexto).OcupacionAsync(Hoy, capacidad: 2);

        Assert.Equal(ServicioGestionReservas.DiasOcupacion, ocupacion.Count);
        Assert.Equal(Hoy, ocupacion[0].Fecha);
        Assert.Equal(1, ocupacion[0].Ocupados);
        Assert.Equal(2, ocupacion[1].Ocupados);
        Assert.True(ocupacion[1].Lleno);
        Assert.Equal(0, ocupacion[5].Ocupados);
    }

    [Fact]
    public async Task Ocupacion_LiberaElCupoAlCancelar()
    {
        await using var contexto = await CrearContextoAsync();
        var servicio = new ServicioGestionReservas(contexto);

        await servicio.CancelarAsync(100, AdminId);
        var ocupacion = await servicio.OcupacionAsync(Hoy, capacidad: 10);

        Assert.Equal(0, ocupacion[0].Ocupados);
        Assert.Equal(1, ocupacion[1].Ocupados);
    }

    [Fact]
    public void Controlador_SoloParaAdministradores()
    {
        // CA-HU-006-05
        var roles = typeof(GestionReservasController).GetCustomAttribute<AuthorizeAttribute>()!.Roles!.Split(',');

        Assert.Equal(new[] { Roles.Administrador }, roles);
    }

    [Fact]
    public void ReservaAdminItem_SoloLaConfirmadaSePuedeCancelar()
    {
        foreach (var estado in Enum.GetValues<EstadoReserva>())
        {
            var item = new ReservaAdminItem(1, "Toby", "Ana", "ana@ejemplo.com", Hoy, Hoy.AddDays(1), TipoServicio.Basico, 50000m, estado);
            Assert.Equal(estado == EstadoReserva.Confirmada, item.SePuedeCancelar);
        }
    }
}
