using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using VacanJef.Core.Entities;
using VacanJef.Core.Enums;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Seguridad;

namespace VacanJef.Tests.Web;

// TT-009: datos ficticios de demostración (solo Desarrollo, sin duplicar)
public class SemillaDemostracionTests
{
    private static readonly UsuariosDemostracion Usuarios = new(CuidadorId: 1, Dueno1Id: 2, Dueno2Id: 3, Dueno3Id: 4);
    private static readonly DateOnly Hoy = new(2026, 11, 25);

    private sealed class Ambiente(string nombre) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nombre;
        public string ApplicationName { get; set; } = "VacanJef.Web";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration Configuracion(string? activar, string? contrasena) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SemillaDemostracion.VariableActivar] = activar,
                [SemillaDemostracion.VariableContrasena] = contrasena,
            })
            .Build();

    private static async Task<VacanJefDbContext> CrearContextoAsync()
    {
        var contexto = new VacanJefDbContext(new DbContextOptionsBuilder<VacanJefDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        contexto.Users.AddRange(
            new Usuario { Id = Usuarios.CuidadorId, UserName = "cuidador.demo@ejemplo.com", Nombre = "Sofía Ramírez (demo)" },
            new Usuario { Id = Usuarios.Dueno1Id, UserName = "carolina.demo@ejemplo.com", Nombre = "Carolina Gómez (demo)" },
            new Usuario { Id = Usuarios.Dueno2Id, UserName = "andres.demo@ejemplo.com", Nombre = "Andrés Restrepo (demo)" },
            new Usuario { Id = Usuarios.Dueno3Id, UserName = "valentina.demo@ejemplo.com", Nombre = "Valentina Ospina (demo)" });
        await contexto.SaveChangesAsync();
        return contexto;
    }

    [Fact]
    public void DebeCargar_EnDesarrolloConVariableYContrasena()
    {
        Assert.True(SemillaDemostracion.DebeCargar(Configuracion("true", "Demo*2026"), new Ambiente(Environments.Development)));
        Assert.True(SemillaDemostracion.DebeCargar(Configuracion("TRUE", "Demo*2026"), new Ambiente(Environments.Development)));
    }

    [Fact]
    public void NoDebeCargar_EnProduccionAunqueEsteActivada()
    {
        Assert.False(SemillaDemostracion.DebeCargar(Configuracion("true", "Demo*2026"), new Ambiente(Environments.Production)));
    }

    [Theory]
    [InlineData(null, "Demo*2026")]
    [InlineData("false", "Demo*2026")]
    [InlineData("true", null)]
    [InlineData("true", "")]
    public void NoDebeCargar_SinVariableOSinContrasena(string? activar, string? contrasena)
    {
        Assert.False(SemillaDemostracion.DebeCargar(Configuracion(activar, contrasena), new Ambiente(Environments.Development)));
    }

    [Fact]
    public async Task CargarDatos_CreaMascotasConSaludYReservasEnTodosLosEstados()
    {
        await using var contexto = await CrearContextoAsync();

        Assert.True(await SemillaDemostracion.CargarDatosAsync(contexto, Usuarios, Hoy));

        Assert.Equal(5, await contexto.Mascotas.CountAsync());
        Assert.True(await contexto.RegistrosSalud.CountAsync() >= 5);
        var estados = await contexto.Reservas.Select(r => r.Estado).Distinct().ToListAsync();
        Assert.Contains(EstadoReserva.Confirmada, estados);
        Assert.Contains(EstadoReserva.Activa, estados);
        Assert.Contains(EstadoReserva.Finalizada, estados);
        Assert.Contains(EstadoReserva.Cancelada, estados);
    }

    [Fact]
    public async Task CargarDatos_DejaUnaEstadiaActivaHoyYUnIngresoPendienteHoy()
    {
        await using var contexto = await CrearContextoAsync();
        await SemillaDemostracion.CargarDatosAsync(contexto, Usuarios, Hoy);

        Assert.True(await contexto.Reservas.AnyAsync(r =>
            r.Estado == EstadoReserva.Activa && r.FechaIngreso <= Hoy && r.FechaSalida >= Hoy && r.IngresoReal != null));
        Assert.True(await contexto.Reservas.AnyAsync(r =>
            r.Estado == EstadoReserva.Confirmada && r.FechaIngreso == Hoy && r.IngresoReal == null));
    }

    [Fact]
    public async Task CargarDatos_CalculaElCostoPorNoche()
    {
        await using var contexto = await CrearContextoAsync();
        await SemillaDemostracion.CargarDatosAsync(contexto, Usuarios, Hoy);

        // RN-007: 3 noches de Básico a 50.000
        var rocky = await contexto.Reservas.SingleAsync(r => r.Estado == EstadoReserva.Activa && r.FechaIngreso == Hoy);
        Assert.Equal(TipoServicio.Basico, rocky.Servicio);
        Assert.Equal(150000m, rocky.CostoTotal);
    }

    [Fact]
    public async Task CargarDatos_IncluyeUnaCorreccionDeLaMismaEstadia()
    {
        // RN-006
        await using var contexto = await CrearContextoAsync();
        await SemillaDemostracion.CargarDatosAsync(contexto, Usuarios, Hoy);

        var correccion = await contexto.Observaciones.SingleAsync(o => o.CorrigeAId != null);
        var original = await contexto.Observaciones.SingleAsync(o => o.Id == correccion.CorrigeAId);
        Assert.Equal(original.ReservaId, correccion.ReservaId);
        Assert.All(await contexto.Observaciones.ToListAsync(), o => Assert.Equal(Usuarios.CuidadorId, o.AutorId));
    }

    [Fact]
    public async Task CargarDatos_SoloEnEstadiasQueEmpezaron()
    {
        // HU-008: no hay observaciones en reservas Confirmadas ni Canceladas
        await using var contexto = await CrearContextoAsync();
        await SemillaDemostracion.CargarDatosAsync(contexto, Usuarios, Hoy);

        Assert.False(await contexto.Observaciones.AnyAsync(o =>
            o.Reserva!.Estado == EstadoReserva.Confirmada || o.Reserva.Estado == EstadoReserva.Cancelada));
    }

    [Fact]
    public async Task CargarDatos_DosVeces_NoDuplica()
    {
        await using var contexto = await CrearContextoAsync();

        Assert.True(await SemillaDemostracion.CargarDatosAsync(contexto, Usuarios, Hoy));
        Assert.False(await SemillaDemostracion.CargarDatosAsync(contexto, Usuarios, Hoy));

        Assert.Equal(5, await contexto.Mascotas.CountAsync());
        Assert.Equal(6, await contexto.Reservas.CountAsync());
        Assert.Equal(7, await contexto.Observaciones.CountAsync());
    }

    [Fact]
    public void Cuentas_SonFicticias()
    {
        // Ley 1581: cuatro cuentas, todas con correo de ejemplo
        Assert.Equal(4, SemillaDemostracion.Correos.Count);
        Assert.All(SemillaDemostracion.Correos, correo => Assert.EndsWith("@ejemplo.com", correo));
    }
}
