using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VacanJef.Core.Constantes;
using VacanJef.Core.Entities;
using VacanJef.Core.Enums;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Controllers;
using VacanJef.Web.Models.Notificaciones;
using VacanJef.Web.Servicios;

namespace VacanJef.Tests.Web;

// HU-009 / RF-010: notificaciones de eventos (CA-HU-009-01 a CA-HU-009-05)
public class NotificacionesTests
{
    private const int DuenoId = 1;
    private const int OtroDuenoId = 2;
    private const int CuidadorId = 3;
    private const int AdministradorId = 4;
    private const int AdministradorInactivoId = 5;
    private const int RolAdministradorId = 50;
    private const int MascotaId = 10;
    private const int ReservaId = 100;

    private sealed class EmisorFalso : IEmisorNotificaciones
    {
        public List<(int UsuarioId, NotificacionItemViewModel? Notificacion, int NoLeidas)> Enviados { get; } = [];
        public bool Fallar { get; set; }

        public Task EnviarAsync(int usuarioId, NotificacionItemViewModel? notificacion, int noLeidas)
        {
            if (Fallar)
                throw new InvalidOperationException("Sin conexión");

            Enviados.Add((usuarioId, notificacion, noLeidas));
            return Task.CompletedTask;
        }
    }

    private static async Task<VacanJefDbContext> CrearContextoAsync()
    {
        var contexto = new VacanJefDbContext(new DbContextOptionsBuilder<VacanJefDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        contexto.Users.AddRange(
            new Usuario { Id = DuenoId, UserName = "ana@ejemplo.com", Nombre = "Ana Pérez" },
            new Usuario { Id = OtroDuenoId, UserName = "luis@ejemplo.com", Nombre = "Luis Gómez" },
            new Usuario { Id = CuidadorId, UserName = "cuidador@ejemplo.com", Nombre = "Sofía Cuidadora" },
            new Usuario { Id = AdministradorId, UserName = "admin@ejemplo.com", Nombre = "Administrador" },
            new Usuario { Id = AdministradorInactivoId, UserName = "admin2@ejemplo.com", Nombre = "Admin inactivo", Activo = false });

        contexto.Roles.Add(new IdentityRole<int> { Id = RolAdministradorId, Name = Roles.Administrador, NormalizedName = "ADMINISTRADOR" });
        contexto.UserRoles.AddRange(
            new IdentityUserRole<int> { UserId = AdministradorId, RoleId = RolAdministradorId },
            new IdentityUserRole<int> { UserId = AdministradorInactivoId, RoleId = RolAdministradorId });

        contexto.Mascotas.Add(new Mascota
        {
            Id = MascotaId, DuenoId = DuenoId, Nombre = "Toby", Raza = "Criollo", Edad = 3, PesoKg = 12.5m,
        });

        contexto.Reservas.Add(new Reserva
        {
            Id = ReservaId,
            MascotaId = MascotaId,
            DuenoId = DuenoId,
            FechaIngreso = new DateOnly(2026, 12, 1),
            FechaSalida = new DateOnly(2026, 12, 4),
            Servicio = TipoServicio.Premium,
            CostoTotal = 240000m,
            Estado = EstadoReserva.Activa,
        });

        await contexto.SaveChangesAsync();
        return contexto;
    }

    private static ServicioNotificaciones Servicio(VacanJefDbContext contexto, EmisorFalso emisor) =>
        new(contexto, emisor, NullLogger<ServicioNotificaciones>.Instance);

    private static async Task<int> AgregarObservacionAsync(VacanJefDbContext contexto, int? corrigeA = null)
    {
        var observacion = new Observacion
        {
            ReservaId = ReservaId,
            AutorId = CuidadorId,
            Tipo = TipoObservacion.Alimentacion,
            Detalle = "Comió toda la ración.",
            CorrigeAId = corrigeA,
        };
        contexto.Observaciones.Add(observacion);
        await contexto.SaveChangesAsync();
        return observacion.Id;
    }

    private static NotificacionesController Controlador(VacanJefDbContext contexto, EmisorFalso emisor, int usuarioId)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) }, "Prueba")),
        };
        return new NotificacionesController(Servicio(contexto, emisor))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
    }

    // CA-HU-009-01
    [Fact]
    public async Task ReservaConfirmada_NotificaAlDuenoConFechasYCosto()
    {
        await using var contexto = await CrearContextoAsync();

        await Servicio(contexto, new EmisorFalso()).NotificarReservaConfirmadaAsync(ReservaId);

        var aviso = Assert.Single(contexto.Notificaciones.ToList(), n => n.UsuarioId == DuenoId);
        Assert.Equal(EventosNotificacion.ReservaConfirmada, aviso.Evento);
        Assert.Contains("Toby", aviso.Mensaje);
        Assert.Contains("01/12/2026", aviso.Mensaje);
        Assert.Contains("04/12/2026", aviso.Mensaje);
        Assert.Contains("$240.000", aviso.Mensaje);
        Assert.Equal("/Reservas", aviso.Enlace);
        Assert.Equal(CanalNotificacion.Plataforma, aviso.Canal);
        Assert.Equal(EstadoNotificacion.Enviada, aviso.Estado);
        Assert.Null(aviso.LeidaEn);
    }

    // CA-HU-009-01 (CU-002 paso 6)
    [Fact]
    public async Task ReservaConfirmada_NotificaSoloALosAdministradoresActivos()
    {
        await using var contexto = await CrearContextoAsync();

        await Servicio(contexto, new EmisorFalso()).NotificarReservaConfirmadaAsync(ReservaId);

        var aviso = Assert.Single(contexto.Notificaciones.ToList(), n => n.Evento == EventosNotificacion.ReservaNueva);
        Assert.Equal(AdministradorId, aviso.UsuarioId);
        Assert.Contains("Ana Pérez", aviso.Mensaje);
        Assert.DoesNotContain(contexto.Notificaciones.ToList(), n => n.UsuarioId == AdministradorInactivoId);
        Assert.DoesNotContain(contexto.Notificaciones.ToList(), n => n.UsuarioId == CuidadorId);
    }

    // CA-HU-009-02
    [Fact]
    public async Task Observacion_NotificaAlDuenoConEnlaceAlHistorialYSinElDetalle()
    {
        await using var contexto = await CrearContextoAsync();
        var observacionId = await AgregarObservacionAsync(contexto);

        await Servicio(contexto, new EmisorFalso()).NotificarObservacionAsync(observacionId);

        var aviso = Assert.Single(contexto.Notificaciones);
        Assert.Equal(DuenoId, aviso.UsuarioId);
        Assert.Equal(EventosNotificacion.ObservacionRegistrada, aviso.Evento);
        Assert.Contains("alimentación", aviso.Mensaje);
        Assert.Contains("Toby", aviso.Mensaje);
        Assert.DoesNotContain("Comió toda la ración", aviso.Mensaje);
        Assert.Equal($"/Mascotas/{MascotaId}/Historial", aviso.Enlace);
    }

    // CA-HU-009-02 (RN-006)
    [Fact]
    public async Task ObservacionCorregida_UsaElEventoDeCorreccion()
    {
        await using var contexto = await CrearContextoAsync();
        var original = await AgregarObservacionAsync(contexto);
        var correccion = await AgregarObservacionAsync(contexto, original);

        await Servicio(contexto, new EmisorFalso()).NotificarObservacionAsync(correccion);

        var aviso = Assert.Single(contexto.Notificaciones);
        Assert.Equal(EventosNotificacion.ObservacionCorregida, aviso.Evento);
        Assert.Equal("Observación corregida", aviso.Titulo);
    }

    // CA-HU-009-03
    [Fact]
    public async Task Notificar_EnviaEnTiempoRealConElContadorDeNoLeidas()
    {
        await using var contexto = await CrearContextoAsync();
        var emisor = new EmisorFalso();
        var servicio = Servicio(contexto, emisor);

        await servicio.NotificarAsync(DuenoId, "EVT-PRUEBA", "Uno", "Primer aviso");
        await servicio.NotificarAsync(DuenoId, "EVT-PRUEBA", "Dos", "Segundo aviso");

        Assert.Equal(2, emisor.Enviados.Count);
        Assert.All(emisor.Enviados, e => Assert.Equal(DuenoId, e.UsuarioId));
        Assert.Equal("Dos", emisor.Enviados[1].Notificacion!.Titulo);
        Assert.Equal(2, emisor.Enviados[1].NoLeidas);
    }

    // CA-HU-009-03: si el tiempo real falla, la notificación igual queda guardada
    [Fact]
    public async Task Notificar_SiFallaElTiempoReal_LaNotificacionSeGuarda()
    {
        await using var contexto = await CrearContextoAsync();
        var emisor = new EmisorFalso { Fallar = true };

        await Servicio(contexto, emisor).NotificarAsync(DuenoId, "EVT-PRUEBA", "Uno", "Aviso");

        Assert.Single(contexto.Notificaciones);
    }

    // CA-HU-009-04 y -05
    [Fact]
    public async Task Listar_SoloLasDelUsuario_DeLaMasRecienteALaMasAntigua()
    {
        await using var contexto = await CrearContextoAsync();
        var servicio = Servicio(contexto, new EmisorFalso());
        await servicio.NotificarAsync(DuenoId, "EVT-PRUEBA", "Antigua", "a");
        await servicio.NotificarAsync(OtroDuenoId, "EVT-PRUEBA", "Ajena", "b");
        await servicio.NotificarAsync(DuenoId, "EVT-PRUEBA", "Reciente", "c");

        var lista = await servicio.ListarAsync(DuenoId);

        Assert.Equal(new[] { "Reciente", "Antigua" }, lista.Select(n => n.Titulo));
    }

    // CA-HU-009-05
    [Fact]
    public async Task MarcarTodas_DejaElContadorEnCero_SinTocarLasDeOtros()
    {
        await using var contexto = await CrearContextoAsync();
        var servicio = Servicio(contexto, new EmisorFalso());
        await servicio.NotificarAsync(DuenoId, "EVT-PRUEBA", "Uno", "a");
        await servicio.NotificarAsync(DuenoId, "EVT-PRUEBA", "Dos", "b");
        await servicio.NotificarAsync(OtroDuenoId, "EVT-PRUEBA", "Ajena", "c");

        var marcadas = await servicio.MarcarTodasAsync(DuenoId);

        Assert.Equal(2, marcadas);
        Assert.Equal(0, await servicio.ContarNoLeidasAsync(DuenoId));
        Assert.Equal(1, await servicio.ContarNoLeidasAsync(OtroDuenoId));
    }

    // CA-HU-009-04: abrir la notificación de otro usuario responde 404 y no la marca
    [Fact]
    public async Task Abrir_NotificacionDeOtroUsuario_NoEncontrada()
    {
        await using var contexto = await CrearContextoAsync();
        var emisor = new EmisorFalso();
        var ajena = await Servicio(contexto, emisor).NotificarAsync(OtroDuenoId, "EVT-PRUEBA", "Ajena", "a", "/Reservas");

        var resultado = await Controlador(contexto, emisor, DuenoId).Abrir(ajena.Id);

        Assert.IsType<NotFoundResult>(resultado);
        Assert.Null((await contexto.Notificaciones.SingleAsync()).LeidaEn);
    }

    // CA-HU-009-05: abrir marca como leída y lleva a la página relacionada
    [Fact]
    public async Task Abrir_NotificacionPropia_LaMarcaYRedirigeAlEnlace()
    {
        await using var contexto = await CrearContextoAsync();
        var emisor = new EmisorFalso();
        var propia = await Servicio(contexto, emisor).NotificarAsync(DuenoId, "EVT-PRUEBA", "Propia", "a", "/Reservas");

        var resultado = await Controlador(contexto, emisor, DuenoId).Abrir(propia.Id);

        var redireccion = Assert.IsType<LocalRedirectResult>(resultado);
        Assert.Equal("/Reservas", redireccion.Url);
        Assert.NotNull((await contexto.Notificaciones.SingleAsync()).LeidaEn);
    }

    // Seguridad: solo se redirige a rutas del mismo sitio
    [Theory]
    [InlineData("/Reservas", true)]
    [InlineData("/Mascotas/10/Historial", true)]
    [InlineData("//sitio-malicioso.com", false)]
    [InlineData("/\\sitio-malicioso.com", false)]
    [InlineData("https://sitio-malicioso.com", false)]
    [InlineData(null, false)]
    public void EsRutaLocal_SoloAceptaRutasDelSitio(string? ruta, bool esperado)
    {
        Assert.Equal(esperado, ServicioNotificaciones.EsRutaLocal(ruta));
    }

    // CA-HU-009-04: un visitante es enviado a iniciar sesión
    [Fact]
    public void Controlador_RequiereIniciarSesion()
    {
        Assert.NotNull(typeof(NotificacionesController).GetCustomAttribute<AuthorizeAttribute>());
    }
}
