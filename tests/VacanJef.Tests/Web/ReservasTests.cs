using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using VacanJef.Core.Constantes;
using VacanJef.Core.Entities;
using VacanJef.Core.Enums;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Controllers;
using VacanJef.Web.Models.Reservas;
using VacanJef.Web.Servicios;

namespace VacanJef.Tests.Web;

// HU-005 / CU-002: reserva de estadía (RN-001, RN-003, RN-007)
public class ReservasTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 1);

    private const int Toby = 1;      // mascota activa del dueño 1
    private const int Luna = 2;      // mascota inactiva del dueño 1
    private const int Max = 3;       // mascota del dueño 2

    // ---------- Reglas (servicio) ----------

    [Theory]
    [InlineData(0, 1)]   // entra y sale el mismo día: se cobra un día
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    public void Dias_SeCobraPorNoche(int noches, int esperado)
    {
        Assert.Equal(esperado, ServicioReservas.Dias(Hoy, Hoy.AddDays(noches)));
    }

    [Fact]
    public void Cotizar_MultiplicaDiasPorLaTarifaDelServicio()
    {
        var servicio = new ServicioReservas(CrearContexto(), new OpcionesReservas { TarifaPremium = 80000m });

        var cotizacion = servicio.Cotizar(TipoServicio.Premium, Hoy, Hoy.AddDays(3));

        Assert.Equal(3, cotizacion.Dias);
        Assert.Equal(80000m, cotizacion.TarifaDiaria);
        Assert.Equal(240000m, cotizacion.CostoTotal);
    }

    [Fact]
    public void ValidarFechas_SalidaAntesDelIngreso_TieneError()
    {
        // CA-HU-005-03
        var errores = Servicio(CrearContexto()).ValidarFechas(Hoy.AddDays(5), Hoy.AddDays(4), Hoy);

        Assert.Contains(errores, e => e.Campo == "FechaSalida");
    }

    [Fact]
    public void ValidarFechas_IngresoEnElPasado_TieneError()
    {
        var errores = Servicio(CrearContexto()).ValidarFechas(Hoy.AddDays(-1), Hoy.AddDays(2), Hoy);

        Assert.Contains(errores, e => e.Campo == "FechaIngreso");
    }

    [Fact]
    public void ValidarFechas_EstadiaDemasiadoLarga_TieneError()
    {
        var servicio = new ServicioReservas(CrearContexto(), new OpcionesReservas { DuracionMaximaDias = 10 });

        var errores = servicio.ValidarFechas(Hoy, Hoy.AddDays(11), Hoy);

        Assert.Contains(errores, e => e.Campo == "FechaSalida");
    }

    [Fact]
    public async Task Crear_MascotaPropia_QuedaConfirmadaConCostoCalculado()
    {
        // CA-HU-005-01
        using var contexto = await ContextoConMascotasAsync();

        var resultado = await Servicio(contexto).CrearAsync(1, Toby, Hoy.AddDays(10), Hoy.AddDays(13), TipoServicio.Basico, Hoy);

        Assert.True(resultado.Exitoso);
        var reserva = await contexto.Reservas.SingleAsync();
        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
        Assert.Equal(1, reserva.DuenoId);
        Assert.Equal(3 * new OpcionesReservas().TarifaBasico, reserva.CostoTotal);
    }

    [Fact]
    public async Task Crear_MascotaAjena_NoGuarda()
    {
        // RN-001
        using var contexto = await ContextoConMascotasAsync();

        var resultado = await Servicio(contexto).CrearAsync(1, Max, Hoy.AddDays(10), Hoy.AddDays(12), TipoServicio.Basico, Hoy);

        Assert.Contains(resultado.Errores, e => e.Campo == "MascotaId");
        Assert.Empty(contexto.Reservas);
    }

    [Fact]
    public async Task Crear_MascotaInactiva_NoGuarda()
    {
        using var contexto = await ContextoConMascotasAsync();

        var resultado = await Servicio(contexto).CrearAsync(1, Luna, Hoy.AddDays(10), Hoy.AddDays(12), TipoServicio.Basico, Hoy);

        Assert.False(resultado.Exitoso);
        Assert.Empty(contexto.Reservas);
    }

    [Fact]
    public async Task Crear_SinCupo_NoGuardaYSugiereFechas()
    {
        // CA-HU-005-02 / RN-003: capacidad de 2 y ya hay 2 mascotas del 10 al 15
        using var contexto = await ContextoConMascotasAsync();
        contexto.Reservas.AddRange(
            ReservaDeOtro(90, Hoy.AddDays(10), Hoy.AddDays(15)),
            ReservaDeOtro(91, Hoy.AddDays(10), Hoy.AddDays(15)));
        await contexto.SaveChangesAsync();

        var resultado = await Servicio(contexto, capacidad: 2)
            .CrearAsync(1, Toby, Hoy.AddDays(12), Hoy.AddDays(14), TipoServicio.Basico, Hoy);

        Assert.False(resultado.Exitoso);
        Assert.Equal(2, await contexto.Reservas.CountAsync());
        Assert.NotEmpty(resultado.Alternativas);
        Assert.All(resultado.Alternativas, a => Assert.True(a.Ingreso >= Hoy.AddDays(15)));
        Assert.All(resultado.Alternativas, a => Assert.Equal(2, a.Salida.DayNumber - a.Ingreso.DayNumber));
    }

    [Fact]
    public async Task Crear_LasReservasCanceladasNoOcupanCupo()
    {
        using var contexto = await ContextoConMascotasAsync();
        var cancelada = ReservaDeOtro(90, Hoy.AddDays(10), Hoy.AddDays(15));
        cancelada.Estado = EstadoReserva.Cancelada;
        contexto.Reservas.Add(cancelada);
        await contexto.SaveChangesAsync();

        var resultado = await Servicio(contexto, capacidad: 1)
            .CrearAsync(1, Toby, Hoy.AddDays(10), Hoy.AddDays(12), TipoServicio.Basico, Hoy);

        Assert.True(resultado.Exitoso);
    }

    [Fact]
    public async Task Crear_ElDiaDeSalidaDeOtraMascotaQuedaLibre()
    {
        // Una sale el 12 y otra entra el 12: con capacidad 1 sí cabe (se cuenta por noches)
        using var contexto = await ContextoConMascotasAsync();
        contexto.Reservas.Add(ReservaDeOtro(90, Hoy.AddDays(10), Hoy.AddDays(12)));
        await contexto.SaveChangesAsync();

        var resultado = await Servicio(contexto, capacidad: 1)
            .CrearAsync(1, Toby, Hoy.AddDays(12), Hoy.AddDays(14), TipoServicio.Basico, Hoy);

        Assert.True(resultado.Exitoso);
    }

    [Fact]
    public async Task Crear_MismaMascotaConFechasCruzadas_NoGuarda()
    {
        using var contexto = await ContextoConMascotasAsync();
        var servicio = Servicio(contexto);
        await servicio.CrearAsync(1, Toby, Hoy.AddDays(10), Hoy.AddDays(14), TipoServicio.Basico, Hoy);

        var resultado = await servicio.CrearAsync(1, Toby, Hoy.AddDays(12), Hoy.AddDays(16), TipoServicio.Spa, Hoy);

        Assert.False(resultado.Exitoso);
        Assert.Equal(1, await contexto.Reservas.CountAsync());
    }

    // ---------- Controlador ----------

    [Fact]
    public void ControladorSoloParaDuenos()
    {
        // CA-HU-005-04: un visitante no registrado no puede reservar
        var atributo = typeof(ReservasController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(atributo);
        Assert.Equal(Roles.Dueno, atributo.Roles);
    }

    [Fact]
    public void FormularioNoPermiteElegirDuenoNiCosto()
    {
        // El dueño sale de la sesión y el costo lo calcula el sistema (RN-007)
        Assert.Null(typeof(NuevaReservaViewModel).GetProperty("DuenoId"));
        Assert.Null(typeof(NuevaReservaViewModel).GetProperty("CostoTotal"));
    }

    [Fact]
    public async Task Index_SoloMuestraReservasDelDueno()
    {
        using var contexto = await ContextoConMascotasAsync();
        contexto.Reservas.AddRange(
            new Reserva { MascotaId = Toby, DuenoId = 1, FechaIngreso = Hoy, FechaSalida = Hoy.AddDays(2), CostoTotal = 100000m },
            new Reserva { MascotaId = Max, DuenoId = 2, FechaIngreso = Hoy, FechaSalida = Hoy.AddDays(2), CostoTotal = 100000m });
        await contexto.SaveChangesAsync();

        var vista = Assert.IsType<ViewResult>(await CrearControlador(contexto, duenoId: 1).Index());

        var lista = Assert.IsAssignableFrom<IEnumerable<ReservaResumenViewModel>>(vista.Model);
        var unica = Assert.Single(lista);
        Assert.Equal("Toby", unica.Mascota);
    }

    [Fact]
    public async Task Nueva_Cotizar_MuestraElCostoSinGuardar()
    {
        using var contexto = await ContextoConMascotasAsync();
        var controlador = CrearControlador(contexto, duenoId: 1);

        var resultado = await controlador.Nueva(ModeloValido(), ReservasController.AccionCotizar);

        var vista = Assert.IsType<ViewResult>(resultado);
        var modelo = Assert.IsType<NuevaReservaViewModel>(vista.Model);
        Assert.NotNull(modelo.Cotizacion);
        Assert.Equal(2 * new OpcionesReservas().TarifaBasico, modelo.Cotizacion.CostoTotal);
        Assert.Empty(contexto.Reservas);
    }

    [Fact]
    public async Task Nueva_Confirmar_GuardaYVuelveAlListado()
    {
        using var contexto = await ContextoConMascotasAsync();
        var controlador = CrearControlador(contexto, duenoId: 1);

        var resultado = await controlador.Nueva(ModeloValido(), ReservasController.AccionConfirmar);

        Assert.IsType<RedirectToActionResult>(resultado);
        var reserva = await contexto.Reservas.SingleAsync();
        Assert.Equal(1, reserva.DuenoId);
        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
    }

    [Fact]
    public async Task Nueva_ConMascotaAjena_NoGuardaYMuestraError()
    {
        using var contexto = await ContextoConMascotasAsync();
        var controlador = CrearControlador(contexto, duenoId: 1);
        var modelo = ModeloValido();
        modelo.MascotaId = Max;

        var resultado = await controlador.Nueva(modelo, ReservasController.AccionConfirmar);

        Assert.IsType<ViewResult>(resultado);
        Assert.True(controlador.ModelState.ContainsKey(nameof(NuevaReservaViewModel.MascotaId)));
        Assert.Empty(contexto.Reservas);
    }

    // ---------- Ayudas ----------

    private static NuevaReservaViewModel ModeloValido()
    {
        var ingreso = ServicioReservas.HoyEnColombia().AddDays(10);
        return new NuevaReservaViewModel
        {
            MascotaId = Toby,
            FechaIngreso = ingreso,
            FechaSalida = ingreso.AddDays(2),
            Servicio = TipoServicio.Basico,
        };
    }

    private static Reserva ReservaDeOtro(int mascotaId, DateOnly ingreso, DateOnly salida) => new()
    {
        MascotaId = mascotaId,
        DuenoId = 99,
        FechaIngreso = ingreso,
        FechaSalida = salida,
        Servicio = TipoServicio.Basico,
        CostoTotal = 0m,
        Estado = EstadoReserva.Confirmada,
    };

    private static ServicioReservas Servicio(VacanJefDbContext contexto, int capacidad = 10) =>
        new(contexto, new OpcionesReservas { CapacidadDiaria = capacidad });

    private static VacanJefDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<VacanJefDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<VacanJefDbContext> ContextoConMascotasAsync()
    {
        var contexto = CrearContexto();
        contexto.Mascotas.AddRange(
            new Mascota { Id = Toby, DuenoId = 1, Nombre = "Toby", Raza = "Criollo", Edad = 3, PesoKg = 12.5m },
            new Mascota { Id = Luna, DuenoId = 1, Nombre = "Luna", Raza = "Pug", Edad = 2, PesoKg = 8m, Activo = false },
            new Mascota { Id = Max, DuenoId = 2, Nombre = "Max", Raza = "Beagle", Edad = 5, PesoKg = 14m });
        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();
        return contexto;
    }

    private static ReservasController CrearControlador(VacanJefDbContext contexto, int duenoId)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, duenoId.ToString()),
                new Claim(ClaimTypes.Role, Roles.Dueno),
            ], "Prueba")),
        };

        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Reservas:CapacidadDiaria"] = "10" })
            .Build();

        return new ReservasController(contexto, configuracion, NullLogger<ReservasController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new TempDataSinPersistencia()),
        };
    }

    private sealed class TempDataSinPersistencia : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
