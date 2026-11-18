using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VacanJef.Core.Entities;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Models.Cuenta;
using VacanJef.Web.Seguridad;

namespace VacanJef.Tests.Web;

// HU-002 / RF-002 / RN-005: inicio de sesión con bloqueo temporal
public class IniciarSesionTests
{
    private const string ContrasenaValida = "Vacanjef#2026";

    // Identity real con base en memoria y la MISMA configuración de Program.cs
    private static ServiceProvider CrearServicios()
    {
        var servicios = new ServiceCollection();
        var nombreBase = Guid.NewGuid().ToString();

        servicios.AddLogging();
        servicios.AddDbContext<VacanJefDbContext>(o => o.UseInMemoryDatabase(nombreBase));
        servicios.AddIdentityCore<Usuario>(OpcionesIdentity.Configurar)
            .AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<VacanJefDbContext>()
            .AddErrorDescriber<ErroresIdentityEnEspanol>();

        return servicios.BuildServiceProvider();
    }

    private static async Task<Usuario> CrearUsuarioAsync(UserManager<Usuario> gestor)
    {
        var usuario = new Usuario
        {
            UserName = "ana.perez@ejemplo.com",
            Email = "ana.perez@ejemplo.com",
            Nombre = "Ana Pérez",
            PhoneNumber = "+57 300 123 4567",
        };

        var resultado = await gestor.CreateAsync(usuario, ContrasenaValida);
        Assert.True(resultado.Succeeded);
        return usuario;
    }

    // Lo mismo que hace SignInManager con lockoutOnFailure: true cuando la contraseña falla
    private static async Task FallarIntentosAsync(UserManager<Usuario> gestor, Usuario usuario, int veces)
    {
        for (var i = 0; i < veces; i++)
        {
            Assert.False(await gestor.CheckPasswordAsync(usuario, "ClaveIncorrecta#1"));
            await gestor.AccessFailedAsync(usuario);
        }
    }

    private static List<ValidationResult> Validar(object modelo)
    {
        var errores = new List<ValidationResult>();
        Validator.TryValidateObject(modelo, new ValidationContext(modelo), errores, validateAllProperties: true);
        return errores;
    }

    [Fact]
    public void DatosValidos_NoTienenErrores()
    {
        var modelo = new IniciarSesionViewModel { Correo = "ana.perez@ejemplo.com", Contrasena = ContrasenaValida };

        Assert.Empty(Validar(modelo));
    }

    [Fact]
    public void CamposVacios_TienenError()
    {
        var errores = Validar(new IniciarSesionViewModel());

        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(IniciarSesionViewModel.Correo)));
        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(IniciarSesionViewModel.Contrasena)));
    }

    [Fact]
    public void CorreoInvalido_TieneError()
    {
        var modelo = new IniciarSesionViewModel { Correo = "ana-sin-arroba", Contrasena = ContrasenaValida };

        Assert.Contains(Validar(modelo), e => e.MemberNames.Contains(nameof(modelo.Correo)));
    }

    [Fact]
    public async Task CuatroIntentosFallidos_NoBloquean()
    {
        using var proveedor = CrearServicios();
        using var alcance = proveedor.CreateScope();
        var gestor = alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var usuario = await CrearUsuarioAsync(gestor);

        await FallarIntentosAsync(gestor, usuario, OpcionesIdentity.MaximoIntentosFallidos - 1);

        Assert.False(await gestor.IsLockedOutAsync(usuario));
    }

    [Fact]
    public async Task CincoIntentosFallidos_BloqueanLaCuenta()
    {
        using var proveedor = CrearServicios();
        using var alcance = proveedor.CreateScope();
        var gestor = alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var usuario = await CrearUsuarioAsync(gestor);

        await FallarIntentosAsync(gestor, usuario, OpcionesIdentity.MaximoIntentosFallidos);

        Assert.True(await gestor.IsLockedOutAsync(usuario));
    }

    [Fact]
    public async Task Bloqueo_DuraQuinceMinutos()
    {
        using var proveedor = CrearServicios();
        using var alcance = proveedor.CreateScope();
        var gestor = alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var usuario = await CrearUsuarioAsync(gestor);

        await FallarIntentosAsync(gestor, usuario, OpcionesIdentity.MaximoIntentosFallidos);

        var finBloqueo = await gestor.GetLockoutEndDateAsync(usuario);
        Assert.NotNull(finBloqueo);
        var minutos = (finBloqueo.Value - DateTimeOffset.UtcNow).TotalMinutes;
        Assert.InRange(minutos, 14, 15);
    }

    [Fact]
    public async Task InicioCorrecto_ReiniciaElConteoDeFallos()
    {
        using var proveedor = CrearServicios();
        using var alcance = proveedor.CreateScope();
        var gestor = alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var usuario = await CrearUsuarioAsync(gestor);

        await FallarIntentosAsync(gestor, usuario, 3);
        Assert.True(await gestor.CheckPasswordAsync(usuario, ContrasenaValida));
        await gestor.ResetAccessFailedCountAsync(usuario); // lo hace SignInManager al entrar bien

        Assert.Equal(0, await gestor.GetAccessFailedCountAsync(usuario));
        Assert.False(await gestor.IsLockedOutAsync(usuario));
    }

    [Fact]
    public async Task ContrasenaDebil_SeRechazaAlCrearUsuario()
    {
        // RNF-003 con la configuración compartida
        using var proveedor = CrearServicios();
        using var alcance = proveedor.CreateScope();
        var gestor = alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();

        var resultado = await gestor.CreateAsync(
            new Usuario { UserName = "debil@ejemplo.com", Email = "debil@ejemplo.com", Nombre = "Prueba" },
            "clave123");

        Assert.False(resultado.Succeeded);
    }
}
