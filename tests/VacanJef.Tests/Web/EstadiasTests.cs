using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VacanJef.Core.Constantes;
using VacanJef.Core.Entities;
using VacanJef.Core.Enums;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Controllers;
using VacanJef.Web.Navegacion;
using VacanJef.Web.Servicios;

namespace VacanJef.Tests.Web;

// HU-014 / RF-008: confirmar ingreso y salida y asignar la cámara
public class EstadiasTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 1);

    private const int DuenoId = 1;
    private const int CuidadorId = 2;
    private const int Toby = 1;
    private const int Luna = 2;
    private const int Patio = 1;      // cámara activa
    private const int Jardin = 2;     // cámara activa
    private const int Bodega = 3;     // cámara desactivada

    // ---------- Panel ----------

    [Fact]
    public async Task Llegadas_MuestraConfirmadasDeHoyYNoLasFuturas()
    {
        // CA-HU-014-01
        using var contexto = await ContextoAsync();
        var hoy = await ReservaAsync(contexto, Toby, Hoy, Hoy.AddDays(2));
        await ReservaAsync(contexto, Luna, Hoy.AddDays(1), Hoy.AddDays(3));

        var llegadas = await new ServicioEstadias(contexto).LlegadasAsync(Hoy);

        var unica = Assert.Single(llegadas);
        Assert.Equal(hoy, unica.ReservaId);
        Assert.Equal("Toby", unica.Mascota);
        Assert.Equal("Ana", unica.Dueno);
    }

    [Fact]
    public async Task EnCurso_MuestraSoloLasActivas()
    {
        using var contexto = await ContextoAsync();
        await ReservaAsync(contexto, Toby, Hoy, Hoy.AddDays(2), EstadoReserva.Activa, Patio);
        await ReservaAsync(contexto, Luna, Hoy, Hoy.AddDays(2));

        var enCurso = await new ServicioEstadias(contexto).EnCursoAsync();

        var unica = Assert.Single(enCurso);
        Assert.Equal("Patio", unica.Camara);
    }

    [Fact]
    public async Task CamarasDisponibles_ExcluyeOcupadasYDesactivadas()
    {
        using var contexto = await ContextoAsync();
        await ReservaAsync(contexto, Toby, Hoy, Hoy.AddDays(2), EstadoReserva.Activa, Patio);

        var disponibles = await new ServicioEstadias(contexto).CamarasDisponiblesAsync();

        Assert.Equal(Jardin, Assert.Single(disponibles).Id);
    }

    // ---------- Ingreso ----------

    [Fact]
    public async Task ConfirmarIngreso_PasaAActivaConHoraYCamara()
    {
        // CA-HU-014-02 y CA-HU-014-03
        using var contexto = await ContextoAsync();
        var id = await ReservaAsync(contexto, Toby, Hoy, Hoy.AddDays(2));

        var resultado = await new ServicioEstadias(contexto).ConfirmarIngresoAsync(id, CuidadorId, Patio, Hoy);

        Assert.True(resultado.Exitoso, resultado.Mensaje);
        var reserva = await contexto.Reservas.AsNoTracking().SingleAsync();
        Assert.Equal(EstadoReserva.Activa, reserva.Estado);
        Assert.NotNull(reserva.IngresoReal);
        Assert.Equal(Patio, reserva.CamaraId);
    }

    [Fact]
    public async Task ConfirmarIngreso_SinCamara_TambienSePermite()
    {
        using var contexto = await ContextoAsync();
        var id = await ReservaAsync(contexto, Toby, Hoy, Hoy.AddDays(2));

        var resultado = await new ServicioEstadias(contexto).ConfirmarIngresoAsync(id, CuidadorId, null, Hoy);

        Assert.True(resultado.Exitoso, resultado.Mensaje);
        Assert.Null((await contexto.Reservas.AsNoTracking().SingleAsync()).CamaraId);
    }

    [Fact]
    public async Task ConfirmarIngreso_AntesDeLaFecha_SeRechaza()
    {
        // CA-HU-014-02
        using var contexto = await ContextoAsync();
        var id = await ReservaAsync(contexto, Toby, Hoy.AddDays(1), Hoy.AddDays(3));

        var resultado = await new ServicioEstadias(contexto).ConfirmarIngresoAsync(id, CuidadorId, null, Hoy);

        Assert.False(resultado.Exitoso);
        Assert.Equal(EstadoReserva.Confirmada, (await contexto.Reservas.AsNoTracking().SingleAsync()).Estado);
    }

    [Fact]
    public async Task ConfirmarIngreso_ReservaCancelada_SeRechaza()
    {
        using var contexto = await ContextoAsync();
        var id = await ReservaAsync(contexto, Toby, Hoy, Hoy.AddDays(2), EstadoReserva.Cancelada);

        var resultado = await new ServicioEstadias(contexto).ConfirmarIngresoAsync(id, CuidadorId, null, Hoy);

        Assert.False(resultado.Exitoso);
    }

    [Fact]
    public async Task ConfirmarIngreso_ConCamaraOcupada_SeRechazaYNoCambiaNada()
    {
        // CA-HU-014-03
        using var contexto = await ContextoAsync();
        await ReservaAsync(contexto, Luna, Hoy, Hoy.AddDays(2), EstadoReserva.Activa, Patio);
        var id = await ReservaAsync(contexto, Toby, Hoy, Hoy.AddDays(2));

        var resultado = await new ServicioEstadias(contexto).ConfirmarIngresoAsync(id, CuidadorId, Patio, Hoy);

        Assert.False(resultado.Exitoso);
        var reserva = await contexto.Reservas.AsNoTracking().SingleAsync(r => r.Id == id);
        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
        Assert.Null(reserva.CamaraId);
        Assert.Empty(contexto.Auditorias);
    }

    [Fact]
    public async Task ConfirmarIngreso_ConCamaraDesactivada_SeRechaza()
    {
        using var contexto = await ContextoAsync();
        var id = await ReservaAsync(contexto, Toby, Hoy, Hoy.AddDays(2));

        var resultado = await new ServicioEstadias(contexto).ConfirmarIngresoAsync(id, CuidadorId, Bodega, Hoy);

        Assert.False(resultado.Exitoso);
    }

    [Fact]
    public async Task ConfirmarIngreso_QuedaEnAuditoria()
    {
        // CA-HU-014-05
        using var contexto = await ContextoAsync();
        var id = await ReservaAsync(contexto, Toby, Hoy, Hoy.AddDays(2));

        await new ServicioEstadias(contexto).ConfirmarIngresoAsync(id, CuidadorId, Patio, Hoy);

        var registros = await contexto.Auditorias.AsNoTracking().ToListAsync();
        Assert.Equal(2, registros.Count);
        Assert.All(registros, r => Assert.Equal(CuidadorId, r.UsuarioId));
        Assert.Contains(registros, r => r.Accion == ServicioEstadias.AccionConfirmarIngreso && r.Recurso == $"Reserva #{id}");
        Assert.Contains(registros, r => r.Accion == ServicioEstadias.AccionAsignarCamara);
    }

    [Fact]
    public async Task ReservaInexistente_NoSeEncuentra()
    {
        using var contexto = await ContextoAsync();

        var resultado = await new ServicioEstadias(contexto).ConfirmarIngresoAsync(999, CuidadorId, null, Hoy);

        Assert.True(resultado.NoEncontrada);
    }

    // ---------- Cámara y salida ----------

    [Fact]
    public async Task AsignarCamara_AEstadiaEnCurso()
    {
        using var contexto = await ContextoAsync();
        var id = await ReservaAsync(contexto, Toby, Hoy, Hoy.AddDays(2), EstadoReserva.Activa);

        var resultado = await new ServicioEstadias(contexto).AsignarCamaraAsync(id, CuidadorId, Jardin);

        Assert.True(resultado.Exitoso, resultado.Mensaje);
        Assert.Equal(Jardin, (await contexto.Reservas.AsNoTracking().SingleAsync()).CamaraId);
    }

    [Fact]
    public async Task AsignarCamara_AReservaNoIniciada_SeRechaza()
    {
        using var contexto = await ContextoAsync();
        var id = await ReservaAsync(contexto, Toby, Hoy, Hoy.AddDays(2));

        var resultado = await new ServicioEstadias(contexto).AsignarCamaraAsync(id, CuidadorId, Jardin);

        Assert.False(resultado.Exitoso);
    }

    [Fact]
    public async Task ConfirmarSalida_FinalizaYLiberaLaCamara()
    {
        // CA-HU-014-04
        using var contexto = await ContextoAsync();
        var id = await ReservaAsync(contexto, Toby, Hoy, Hoy.AddDays(2), EstadoReserva.Activa, Patio);
        var servicio = new ServicioEstadias(contexto);

        var resultado = await servicio.ConfirmarSalidaAsync(id, CuidadorId);

        Assert.True(resultado.Exitoso, resultado.Mensaje);
        var reserva = await contexto.Reservas.AsNoTracking().SingleAsync();
        Assert.Equal(EstadoReserva.Finalizada, reserva.Estado);
        Assert.NotNull(reserva.SalidaReal);
        Assert.Contains(await servicio.CamarasDisponiblesAsync(), c => c.Id == Patio);
        Assert.Contains(contexto.Auditorias, a => a.Accion == ServicioEstadias.AccionConfirmarSalida);
    }

    [Fact]
    public async Task ConfirmarSalida_DeReservaNoActiva_SeRechaza()
    {
        using var contexto = await ContextoAsync();
        var id = await ReservaAsync(contexto, Toby, Hoy, Hoy.AddDays(2));

        var resultado = await new ServicioEstadias(contexto).ConfirmarSalidaAsync(id, CuidadorId);

        Assert.False(resultado.Exitoso);
    }

    // ---------- Cámaras (administrador) ----------

    [Fact]
    public async Task CrearCamara_ValidaYQuedaEnAuditoria()
    {
        using var contexto = await ContextoAsync();
        var servicio = new ServicioCamaras(contexto);

        var resultado = await servicio.CrearAsync("Sala", "rtsp://localhost:8554/camara4", CuidadorId);

        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Contains(contexto.Auditorias, a => a.Accion == ServicioCamaras.AccionCrear);
    }

    [Theory]
    [InlineData("Sala", "ftp://camara")]
    [InlineData("Sala", "")]
    [InlineData("", "rtsp://localhost:8554/x")]
    [InlineData("Patio", "rtsp://localhost:8554/x")] // nombre repetido
    public async Task CrearCamara_DatosInvalidos_SeRechaza(string nombre, string url)
    {
        using var contexto = await ContextoAsync();

        var resultado = await new ServicioCamaras(contexto).CrearAsync(nombre, url, CuidadorId);

        Assert.False(resultado.Exito);
        Assert.Equal(3, await contexto.Camaras.CountAsync());
    }

    [Fact]
    public async Task DesactivarCamaraEnUso_SeRechaza()
    {
        using var contexto = await ContextoAsync();
        await ReservaAsync(contexto, Toby, Hoy, Hoy.AddDays(2), EstadoReserva.Activa, Patio);

        var resultado = await new ServicioCamaras(contexto).CambiarEstadoAsync(Patio, activa: false, CuidadorId);

        Assert.False(resultado.Exito);
    }

    [Fact]
    public async Task ListarCamaras_MuestraQuienLaUsa()
    {
        using var contexto = await ContextoAsync();
        await ReservaAsync(contexto, Toby, Hoy, Hoy.AddDays(2), EstadoReserva.Activa, Patio);

        var camaras = await new ServicioCamaras(contexto).ListarAsync();

        Assert.Equal("Toby", camaras.Single(c => c.Id == Patio).MascotaActual);
        Assert.Null(camaras.Single(c => c.Id == Jardin).MascotaActual);
    }

    // ---------- Permisos, menú y auditoría ----------

    [Fact]
    public void Estadias_SoloCuidadorYAdministrador()
    {
        // CA-HU-014-05: un dueño no puede
        var atributo = typeof(EstadiasController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(atributo);
        Assert.Equal($"{Roles.Cuidador},{Roles.Administrador}", atributo.Roles);
    }

    [Fact]
    public void Camaras_SoloAdministrador()
    {
        var atributo = typeof(CamarasController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(atributo);
        Assert.Equal(Roles.Administrador, atributo.Roles);
    }

    [Fact]
    public void MenuDelAdministrador_IncluyeEstadias()
    {
        var administrador = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, Roles.Administrador)], "Prueba"));

        Assert.Contains(MenuNavegacion.Para(administrador), e => e.Ruta == "/Estadias");
    }

    [Theory]
    [InlineData(ServicioEstadias.AccionConfirmarIngreso)]
    [InlineData(ServicioEstadias.AccionAsignarCamara)]
    [InlineData(ServicioEstadias.AccionConfirmarSalida)]
    [InlineData(ServicioCamaras.AccionCrear)]
    public void AccionesNuevas_TienenDescripcionEnLaAuditoria(string accion)
    {
        Assert.NotEqual(accion, ConsultaAuditoria.Describir(accion));
    }

    [Fact]
    public async Task Controlador_ConfirmarIngreso_VuelveAlPanelConMensaje()
    {
        using var contexto = await ContextoAsync();
        var id = await ReservaAsync(contexto, Toby, ServicioReservas.HoyEnColombia(), ServicioReservas.HoyEnColombia().AddDays(2));
        var controlador = CrearControlador(contexto);

        var resultado = await controlador.ConfirmarIngreso(id, Jardin);

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.NotNull(controlador.TempData["Mensaje"]);
        Assert.Equal(EstadoReserva.Activa, (await contexto.Reservas.AsNoTracking().SingleAsync()).Estado);
    }

    // ---------- Ayudas ----------

    private static async Task<VacanJefDbContext> ContextoAsync()
    {
        var contexto = new VacanJefDbContext(new DbContextOptionsBuilder<VacanJefDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        contexto.Users.AddRange(
            new Usuario { Id = DuenoId, UserName = "ana@ejemplo.com", Email = "ana@ejemplo.com", Nombre = "Ana" },
            new Usuario { Id = CuidadorId, UserName = "carla@ejemplo.com", Email = "carla@ejemplo.com", Nombre = "Carla" });
        contexto.Mascotas.AddRange(
            new Mascota { Id = Toby, DuenoId = DuenoId, Nombre = "Toby", Raza = "Criollo", Edad = 3, PesoKg = 12.5m },
            new Mascota { Id = Luna, DuenoId = DuenoId, Nombre = "Luna", Raza = "Pug", Edad = 2, PesoKg = 8m });
        contexto.Camaras.AddRange(
            new Camara { Id = Patio, Nombre = "Patio", UrlStream = "rtsp://localhost:8554/camara1" },
            new Camara { Id = Jardin, Nombre = "Jardín", UrlStream = "rtsp://localhost:8554/camara2" },
            new Camara { Id = Bodega, Nombre = "Bodega", UrlStream = "rtsp://localhost:8554/camara3", Activa = false });
        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();
        return contexto;
    }

    private static async Task<int> ReservaAsync(VacanJefDbContext contexto, int mascotaId, DateOnly ingreso, DateOnly salida,
        EstadoReserva estado = EstadoReserva.Confirmada, int? camaraId = null)
    {
        var reserva = new Reserva
        {
            MascotaId = mascotaId,
            DuenoId = DuenoId,
            FechaIngreso = ingreso,
            FechaSalida = salida,
            Servicio = TipoServicio.Basico,
            CostoTotal = 100000m,
            Estado = estado,
            CamaraId = camaraId,
            IngresoReal = estado == EstadoReserva.Activa ? DateTime.UtcNow : null,
        };
        contexto.Reservas.Add(reserva);
        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();
        return reserva.Id;
    }

    private static EstadiasController CrearControlador(VacanJefDbContext contexto)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, CuidadorId.ToString()),
                new Claim(ClaimTypes.Role, Roles.Cuidador),
            ], "Prueba")),
        };

        return new EstadiasController(contexto, NullLogger<EstadiasController>.Instance)
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
