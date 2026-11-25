using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using VacanJef.Core.Constantes;
using VacanJef.Core.Entities;
using VacanJef.Core.Enums;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Controllers;
using VacanJef.Web.Models.Observaciones;
using VacanJef.Web.Servicios;

namespace VacanJef.Tests.Web;

// HU-008 / RF-009 / RN-006: registrar observaciones durante una estadía activa
public class ObservacionesTests
{
    private const int DuenoId = 1;
    private const int CuidadorId = 2;
    private const int MascotaId = 10;
    private const int ReservaActiva = 100;
    private const int ReservaConfirmada = 101;
    private const int ReservaFinalizada = 102;

    private static async Task<VacanJefDbContext> CrearContextoAsync()
    {
        var contexto = new VacanJefDbContext(new DbContextOptionsBuilder<VacanJefDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        contexto.Users.AddRange(
            new Usuario { Id = DuenoId, UserName = "ana@ejemplo.com", Nombre = "Ana Pérez" },
            new Usuario { Id = CuidadorId, UserName = "cuidador@ejemplo.com", Nombre = "Laura Cuidadora" });

        contexto.Mascotas.Add(new Mascota
        {
            Id = MascotaId, DuenoId = DuenoId, Nombre = "Toby", Raza = "Criollo", Edad = 3, PesoKg = 12.5m,
        });

        contexto.RegistrosSalud.Add(new RegistroSalud
        {
            MascotaId = MascotaId, Tipo = TipoRegistroSalud.Vacuna, Descripcion = "Rabia", Fecha = new DateOnly(2026, 3, 10),
        });

        contexto.Reservas.AddRange(
            NuevaReserva(ReservaActiva, EstadoReserva.Activa),
            NuevaReserva(ReservaConfirmada, EstadoReserva.Confirmada),
            NuevaReserva(ReservaFinalizada, EstadoReserva.Finalizada));

        await contexto.SaveChangesAsync();
        return contexto;
    }

    private static Reserva NuevaReserva(int id, EstadoReserva estado) => new()
    {
        Id = id,
        MascotaId = MascotaId,
        DuenoId = DuenoId,
        FechaIngreso = new DateOnly(2026, 11, 24),
        FechaSalida = new DateOnly(2026, 11, 27),
        Servicio = TipoServicio.Basico,
        CostoTotal = 150000m,
        Estado = estado,
    };

    [Fact]
    public async Task Registrar_EnEstadiaActiva_GuardaConAutorYFecha()
    {
        await using var contexto = await CrearContextoAsync();
        var antes = DateTime.UtcNow;

        var resultado = await new ServicioObservaciones(contexto)
            .RegistrarAsync(ReservaActiva, CuidadorId, TipoObservacion.Alimentacion, "  Comió toda la ración.  ", null);

        Assert.True(resultado.Exitoso);
        var guardada = Assert.Single(contexto.Observaciones);
        Assert.Equal(CuidadorId, guardada.AutorId);
        Assert.Equal(TipoObservacion.Alimentacion, guardada.Tipo);
        Assert.Equal("Comió toda la ración.", guardada.Detalle);
        Assert.True(guardada.CreadoEn >= antes);
    }

    [Fact]
    public async Task Registrar_SinDetalle_NoGuarda()
    {
        await using var contexto = await CrearContextoAsync();

        var resultado = await new ServicioObservaciones(contexto)
            .RegistrarAsync(ReservaActiva, CuidadorId, TipoObservacion.Salud, "   ", null);

        Assert.False(resultado.Exitoso);
        Assert.Equal(nameof(NuevaObservacionViewModel.Detalle), resultado.Campo);
        Assert.Empty(contexto.Observaciones);
    }

    [Fact]
    public async Task Registrar_DetalleDeMasDe1000Caracteres_NoGuarda()
    {
        await using var contexto = await CrearContextoAsync();

        var resultado = await new ServicioObservaciones(contexto)
            .RegistrarAsync(ReservaActiva, CuidadorId, TipoObservacion.Actividad, new string('a', 1001), null);

        Assert.False(resultado.Exitoso);
        Assert.Contains("1000", resultado.Mensaje);
        Assert.Empty(contexto.Observaciones);
    }

    [Theory]
    [InlineData(ReservaConfirmada)]
    [InlineData(ReservaFinalizada)]
    public async Task Registrar_EnReservaNoActiva_NoGuarda(int reservaId)
    {
        await using var contexto = await CrearContextoAsync();

        var resultado = await new ServicioObservaciones(contexto)
            .RegistrarAsync(reservaId, CuidadorId, TipoObservacion.Salud, "Está tranquilo.", null);

        Assert.False(resultado.Exitoso);
        Assert.Contains("estadía activa", resultado.Mensaje);
        Assert.Empty(contexto.Observaciones);
    }

    [Fact]
    public async Task Registrar_EnReservaInexistente_NoSeEncuentra()
    {
        await using var contexto = await CrearContextoAsync();

        var resultado = await new ServicioObservaciones(contexto)
            .RegistrarAsync(9999, CuidadorId, TipoObservacion.Salud, "Está tranquilo.", null);

        Assert.True(resultado.ReservaNoEncontrada);
    }

    [Fact]
    public async Task Corregir_CreaUnaObservacionNuevaYConservaLaOriginal()
    {
        await using var contexto = await CrearContextoAsync();
        var servicio = new ServicioObservaciones(contexto);
        var original = (await servicio.RegistrarAsync(ReservaActiva, CuidadorId, TipoObservacion.Salud, "Vomitó una vez.", null)).Observacion!;

        var correccion = await servicio.RegistrarAsync(
            ReservaActiva, CuidadorId, TipoObservacion.Salud, "Corrección: no vomitó, fue otra mascota.", original.Id);

        Assert.True(correccion.Exitoso);
        Assert.Equal(original.Id, correccion.Observacion!.CorrigeAId);
        Assert.Equal(2, await contexto.Observaciones.CountAsync());
        Assert.Equal("Vomitó una vez.", (await contexto.Observaciones.AsNoTracking().SingleAsync(o => o.Id == original.Id)).Detalle);
    }

    [Fact]
    public async Task Corregir_UnaObservacionQueNoEsDeLaEstadia_NoGuarda()
    {
        await using var contexto = await CrearContextoAsync();

        var resultado = await new ServicioObservaciones(contexto)
            .RegistrarAsync(ReservaActiva, CuidadorId, TipoObservacion.Salud, "Corrección.", 9999);

        Assert.False(resultado.Exitoso);
        Assert.Empty(contexto.Observaciones);
    }

    [Fact]
    public async Task EstadiasActivas_SoloMuestraLasActivas()
    {
        await using var contexto = await CrearContextoAsync();

        var estadias = await new ServicioObservaciones(contexto).EstadiasActivasAsync();

        var unica = Assert.Single(estadias);
        Assert.Equal(ReservaActiva, unica.ReservaId);
        Assert.Equal("Toby", unica.Mascota);
        Assert.Equal("Ana Pérez", unica.Dueno);
        Assert.Equal(0, unica.Observaciones);
        Assert.Null(unica.UltimaObservacion);
    }

    [Fact]
    public async Task Estadia_MuestraSaludYObservacionesDeLaMasRecienteALaMasAntigua()
    {
        await using var contexto = await CrearContextoAsync();
        contexto.Observaciones.AddRange(
            new Observacion { ReservaId = ReservaActiva, AutorId = CuidadorId, Tipo = TipoObservacion.Actividad, Detalle = "Primera", CreadoEn = new DateTime(2026, 11, 24, 13, 0, 0, DateTimeKind.Utc) },
            new Observacion { ReservaId = ReservaActiva, AutorId = CuidadorId, Tipo = TipoObservacion.Salud, Detalle = "Segunda", CreadoEn = new DateTime(2026, 11, 24, 18, 0, 0, DateTimeKind.Utc) });
        await contexto.SaveChangesAsync();

        var estadia = await new ServicioObservaciones(contexto).EstadiaAsync(ReservaActiva, incluirNoActivas: false);

        Assert.NotNull(estadia);
        Assert.True(estadia.PermiteRegistrar);
        Assert.Single(estadia.Salud);
        Assert.Equal(new[] { "Segunda", "Primera" }, estadia.Observaciones.Select(o => o.Detalle));
        Assert.All(estadia.Observaciones, o => Assert.Equal("Laura Cuidadora", o.Autor));
    }

    [Fact]
    public async Task Estadia_ElCuidadorNoVeUnaEstadiaFinalizada_ElAdministradorSi()
    {
        await using var contexto = await CrearContextoAsync();
        var servicio = new ServicioObservaciones(contexto);

        Assert.Null(await servicio.EstadiaAsync(ReservaFinalizada, incluirNoActivas: false));

        var paraAdministrador = await servicio.EstadiaAsync(ReservaFinalizada, incluirNoActivas: true);
        Assert.NotNull(paraAdministrador);
        Assert.False(paraAdministrador.PermiteRegistrar);
    }

    [Fact]
    public void Controlador_SoloParaCuidadorYAdministrador()
    {
        // CA-HU-008-05: un dueño o un visitante no puede registrar observaciones
        var roles = typeof(ObservacionesController).GetCustomAttribute<AuthorizeAttribute>()!.Roles!.Split(',');

        Assert.Contains(Roles.Cuidador, roles);
        Assert.Contains(Roles.Administrador, roles);
        Assert.DoesNotContain(Roles.Dueno, roles);
    }

    [Fact]
    public void Formulario_SinDetalleNiTipo_TieneErrores()
    {
        var modelo = new NuevaObservacionViewModel { Detalle = string.Empty };
        var errores = new List<ValidationResult>();

        Validator.TryValidateObject(modelo, new ValidationContext(modelo), errores, validateAllProperties: true);

        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(NuevaObservacionViewModel.Detalle)));
        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(NuevaObservacionViewModel.Tipo)));
    }

    [Fact]
    public void Formulario_NoPermiteElegirAutorNiEstadia()
    {
        // El autor sale de la sesión y la estadía de la ruta, nunca del formulario
        Assert.Null(typeof(NuevaObservacionViewModel).GetProperty("AutorId"));
        Assert.Null(typeof(NuevaObservacionViewModel).GetProperty("ReservaId"));
    }
}
