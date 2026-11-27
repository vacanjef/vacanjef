using System.Net;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using VacanJef.Core.Constantes;
using VacanJef.Core.Entities;
using VacanJef.Core.Enums;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Controllers;
using VacanJef.Web.Models.Monitoreo;
using VacanJef.Web.Seguridad;
using VacanJef.Web.Servicios;

namespace VacanJef.Tests.Web;

// HU-007 / RF-007 / RN-004: monitoreo en vivo
public class MonitoreoTests
{
    private const int Ana = 1;        // dueña de Toby
    private const int Luis = 2;       // otro dueño
    private const int Carla = 3;      // cuidadora
    private const int Toby = 1;
    private const int Max = 2;
    private const int Patio = 1;

    // ---------- Autorización (RN-004) ----------

    [Fact]
    public async Task Dueno_ConEstadiaActivaYCamara_Puede()
    {
        // CA-HU-007-01 / ESC-HU-007-01
        using var contexto = await ContextoAsync();
        var id = await ReservaAsync(contexto, Toby, Ana, EstadoReserva.Activa, Patio);

        var autorizacion = await new ServicioMonitoreo(contexto).AutorizarAsync(id, Ana, esPersonal: false);

        Assert.True(autorizacion.Permitido);
        Assert.Equal("camara1", autorizacion.Ruta);
        Assert.Equal("Toby", autorizacion.Mascota);
    }

    [Fact]
    public async Task OtroDueno_NoPuede()
    {
        // ESC-HU-007-03
        using var contexto = await ContextoAsync();
        var id = await ReservaAsync(contexto, Toby, Ana, EstadoReserva.Activa, Patio);

        var autorizacion = await new ServicioMonitoreo(contexto).AutorizarAsync(id, Luis, esPersonal: false);

        Assert.False(autorizacion.Permitido);
        Assert.Null(autorizacion.Ruta);
    }

    [Theory]
    [InlineData(EstadoReserva.Confirmada)]
    [InlineData(EstadoReserva.Finalizada)]
    [InlineData(EstadoReserva.Cancelada)]
    public async Task ReservaSinEstadiaActiva_NoPuede(EstadoReserva estado)
    {
        using var contexto = await ContextoAsync();
        var id = await ReservaAsync(contexto, Toby, Ana, estado, Patio);

        var autorizacion = await new ServicioMonitoreo(contexto).AutorizarAsync(id, Ana, esPersonal: false);

        Assert.False(autorizacion.Permitido);
    }

    [Fact]
    public async Task SinCamara_PermiteConRespaldo()
    {
        // CA-HU-007-02: entra a la página, pero sin transmisión (mensaje y última foto)
        using var contexto = await ContextoAsync();
        var id = await ReservaAsync(contexto, Toby, Ana, EstadoReserva.Activa, camaraId: null);

        var autorizacion = await new ServicioMonitoreo(contexto).AutorizarAsync(id, Ana, esPersonal: false);

        Assert.True(autorizacion.Permitido);
        Assert.Null(autorizacion.Ruta);
    }

    [Fact]
    public async Task Personal_VeCualquierEstadiaActiva()
    {
        using var contexto = await ContextoAsync();
        var id = await ReservaAsync(contexto, Toby, Ana, EstadoReserva.Activa, Patio);

        var autorizacion = await new ServicioMonitoreo(contexto).AutorizarAsync(id, Carla, esPersonal: true);

        Assert.True(autorizacion.Permitido);
    }

    [Fact]
    public async Task EstadiasVisibles_ElDuenoSoloVeLasSuyasActivas()
    {
        using var contexto = await ContextoAsync();
        await ReservaAsync(contexto, Toby, Ana, EstadoReserva.Activa, Patio);
        await ReservaAsync(contexto, Max, Luis, EstadoReserva.Activa, camaraId: null);
        await ReservaAsync(contexto, Toby, Ana, EstadoReserva.Finalizada, camaraId: null);
        var servicio = new ServicioMonitoreo(contexto);

        var deAna = await servicio.EstadiasVisiblesAsync(Ana, esPersonal: false);
        var delPersonal = await servicio.EstadiasVisiblesAsync(Carla, esPersonal: true);

        Assert.Equal("Patio", Assert.Single(deAna).Camara);
        Assert.Equal(2, delPersonal.Count);
    }

    [Theory]
    [InlineData("rtsp://localhost:8554/camara1", "camara1")]
    [InlineData("rtsp://usuario:clave@192.168.0.50:554/stream1", "stream1")]
    [InlineData("rtsp://localhost:8554/patio/camara2", "patio/camara2")]
    [InlineData("rtsp://localhost:8554/", null)]
    [InlineData("no es una url", null)]
    [InlineData("rtsp://localhost:8554/camara%201", null)]
    [InlineData(null, null)]
    public void RutaMediaMtx_TomaSoloElPathDeLaCamara(string? url, string? esperado)
    {
        Assert.Equal(esperado, ServicioMonitoreo.RutaMediaMtx(url));
    }

    // ---------- Token temporal ----------

    [Fact]
    public void Token_SoloSirveParaSuUsuario()
    {
        var tokens = new TokenMonitoreo(new EphemeralDataProtectionProvider());
        var token = tokens.Crear(reservaId: 7, usuarioId: Ana);

        Assert.True(tokens.Validar(token, Ana, out var reservaId));
        Assert.Equal(7, reservaId);
        Assert.False(tokens.Validar(token, Luis, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("token-inventado")]
    public void Token_InvalidoOAlterado_SeRechaza(string? token)
    {
        var tokens = new TokenMonitoreo(new EphemeralDataProtectionProvider());

        Assert.False(tokens.Validar(token, Ana, out _));
    }

    [Fact]
    public void Token_DeOtraInstalacion_SeRechaza()
    {
        var token = new TokenMonitoreo(new EphemeralDataProtectionProvider()).Crear(7, Ana);

        Assert.False(new TokenMonitoreo(new EphemeralDataProtectionProvider()).Validar(token, Ana, out _));
    }

    // ---------- Controlador ----------

    [Fact]
    public void Controlador_SoloUsuariosConRol()
    {
        var atributo = typeof(MonitoreoController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(atributo);
        Assert.Contains(Roles.Dueno, atributo.Roles);
    }

    [Fact]
    public async Task Ver_Propia_MuestraLaPaginaYAuditaPermitido()
    {
        using var contexto = await ContextoAsync();
        var id = await ReservaAsync(contexto, Toby, Ana, EstadoReserva.Activa, Patio);

        var resultado = await Controlador(contexto, Ana, Roles.Dueno).Ver(id);

        var vista = Assert.IsType<ViewResult>(resultado);
        var modelo = Assert.IsType<MonitoreoVerViewModel>(vista.Model);
        Assert.StartsWith("/Monitoreo/Senal?token=", modelo.UrlSenal);
        Assert.DoesNotContain("rtsp", modelo.UrlSenal); // la dirección de la cámara nunca va al navegador
        var registro = await contexto.Auditorias.AsNoTracking().SingleAsync();
        Assert.Equal(ServicioMonitoreo.AccionAccesoCamara, registro.Accion);
        Assert.Equal("permitido", registro.Resultado);
        Assert.Equal($"Camara #{Patio}", registro.Recurso);
    }

    [Fact]
    public async Task Ver_Ajena_SeDeniegaYQuedaRegistrada()
    {
        // ESC-HU-007-03
        using var contexto = await ContextoAsync();
        var id = await ReservaAsync(contexto, Toby, Ana, EstadoReserva.Activa, Patio);

        var resultado = await Controlador(contexto, Luis, Roles.Dueno).Ver(id);

        Assert.IsType<ForbidResult>(resultado);
        var registro = await contexto.Auditorias.AsNoTracking().SingleAsync();
        Assert.Equal("denegado", registro.Resultado);
        Assert.Equal(Luis, registro.UsuarioId);
    }

    [Fact]
    public async Task Ver_SinCamara_MuestraRespaldoSinToken()
    {
        using var contexto = await ContextoAsync();
        var id = await ReservaAsync(contexto, Toby, Ana, EstadoReserva.Activa, camaraId: null);

        var vista = Assert.IsType<ViewResult>(await Controlador(contexto, Ana, Roles.Dueno).Ver(id));

        Assert.Null(Assert.IsType<MonitoreoVerViewModel>(vista.Model).UrlSenal);
    }

    [Fact]
    public async Task Senal_ConTokenInvalido_Responde403()
    {
        using var contexto = await ContextoAsync();

        var resultado = await Controlador(contexto, Ana, Roles.Dueno).Senal("token-inventado");

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(resultado).StatusCode);
    }

    // ---------- Cliente de MediaMTX ----------

    [Fact]
    public async Task ClienteMediaMtx_EnviaLaOfertaAlEndpointWhep()
    {
        var manejador = new ManejadorFalso(HttpStatusCode.Created, "v=0\r\nrespuesta");
        var cliente = new ClienteMediaMtx("http://localhost:8889/", new HttpClient(manejador));

        var respuesta = await cliente.NegociarAsync("camara1", "v=0\r\noferta");

        Assert.Equal("v=0\r\nrespuesta", respuesta);
        Assert.Equal("http://localhost:8889/camara1/whep", manejador.Url);
        Assert.Equal("application/sdp", manejador.TipoContenido);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]          // la cámara no está publicando
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task ClienteMediaMtx_SinTransmision_DevuelveNull(HttpStatusCode estado)
    {
        var cliente = new ClienteMediaMtx("http://localhost:8889", new HttpClient(new ManejadorFalso(estado, "error")));

        Assert.Null(await cliente.NegociarAsync("camara1", "v=0\r\noferta"));
    }

    [Fact]
    public async Task ClienteMediaMtx_ServidorApagado_DevuelveNull()
    {
        var cliente = new ClienteMediaMtx("http://localhost:8889", new HttpClient(new ManejadorQueFalla()));

        Assert.Null(await cliente.NegociarAsync("camara1", "v=0\r\noferta"));
    }

    // ---------- Ayudas ----------

    private static async Task<VacanJefDbContext> ContextoAsync()
    {
        var contexto = new VacanJefDbContext(new DbContextOptionsBuilder<VacanJefDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        contexto.Users.AddRange(
            new Usuario { Id = Ana, UserName = "ana@ejemplo.com", Email = "ana@ejemplo.com", Nombre = "Ana" },
            new Usuario { Id = Luis, UserName = "luis@ejemplo.com", Email = "luis@ejemplo.com", Nombre = "Luis" },
            new Usuario { Id = Carla, UserName = "carla@ejemplo.com", Email = "carla@ejemplo.com", Nombre = "Carla" });
        contexto.Mascotas.AddRange(
            new Mascota { Id = Toby, DuenoId = Ana, Nombre = "Toby", Raza = "Criollo", Edad = 3, PesoKg = 12m, FotoUrl = "toby.jpg" },
            new Mascota { Id = Max, DuenoId = Luis, Nombre = "Max", Raza = "Beagle", Edad = 5, PesoKg = 14m });
        contexto.Camaras.Add(new Camara { Id = Patio, Nombre = "Patio", UrlStream = "rtsp://localhost:8554/camara1" });
        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();
        return contexto;
    }

    private static async Task<int> ReservaAsync(VacanJefDbContext contexto, int mascotaId, int duenoId,
        EstadoReserva estado, int? camaraId)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var reserva = new Reserva
        {
            MascotaId = mascotaId,
            DuenoId = duenoId,
            FechaIngreso = hoy,
            FechaSalida = hoy.AddDays(2),
            Servicio = TipoServicio.Basico,
            CostoTotal = 100000m,
            Estado = estado,
            CamaraId = camaraId,
        };
        contexto.Reservas.Add(reserva);
        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();
        return reserva.Id;
    }

    private static MonitoreoController Controlador(VacanJefDbContext contexto, int usuarioId, string rol)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()),
                new Claim(ClaimTypes.Role, rol),
            ], "Prueba")),
        };

        return new MonitoreoController(
            contexto,
            new ConfigurationBuilder().Build(),
            new EphemeralDataProtectionProvider(),
            NullLogger<MonitoreoController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
    }

    private sealed class ManejadorFalso(HttpStatusCode estado, string cuerpo) : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public string? TipoContenido { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud, CancellationToken cancelacion)
        {
            Url = solicitud.RequestUri?.ToString();
            TipoContenido = solicitud.Content?.Headers.ContentType?.MediaType;
            return Task.FromResult(new HttpResponseMessage(estado) { Content = new StringContent(cuerpo) });
        }
    }

    private sealed class ManejadorQueFalla : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud, CancellationToken cancelacion) =>
            throw new HttpRequestException("Conexión rechazada");
    }
}
