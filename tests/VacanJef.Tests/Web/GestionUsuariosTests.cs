using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VacanJef.Core.Constantes;
using VacanJef.Core.Entities;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Seguridad;
using VacanJef.Web.Servicios;

namespace VacanJef.Tests.Web;

// HU-015: gestión de usuarios por el administrador
public class GestionUsuariosTests
{
    private const string Contrasena = "Vacanjef#2026";

    // Identity real con base en memoria, roles creados y la misma configuración de Program.cs
    private static async Task<ServiceProvider> CrearServiciosAsync()
    {
        var servicios = new ServiceCollection();
        var nombreBase = Guid.NewGuid().ToString();

        servicios.AddLogging();
        servicios.AddDbContext<VacanJefDbContext>(o => o.UseInMemoryDatabase(nombreBase));
        servicios.AddIdentityCore<Usuario>(OpcionesIdentity.Configurar)
            .AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<VacanJefDbContext>()
            .AddErrorDescriber<ErroresIdentityEnEspanol>();
        servicios.AddScoped<GestionUsuarios>();

        var proveedor = servicios.BuildServiceProvider();
        await SemillaRoles.CrearAsync(proveedor);
        return proveedor;
    }

    private static async Task<Usuario> CrearUsuarioAsync(UserManager<Usuario> gestor, string correo, string rol, string nombre = "Prueba")
    {
        var usuario = new Usuario { UserName = correo, Email = correo, Nombre = nombre, PhoneNumber = "3001234567" };
        Assert.True((await gestor.CreateAsync(usuario, Contrasena)).Succeeded);
        Assert.True((await gestor.AddToRoleAsync(usuario, rol)).Succeeded);
        return usuario;
    }

    private sealed class Escenario(ServiceProvider proveedor) : IDisposable
    {
        private readonly IServiceScope alcance = proveedor.CreateScope();
        public UserManager<Usuario> Gestor => alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        public GestionUsuarios Gestion => alcance.ServiceProvider.GetRequiredService<GestionUsuarios>();
        public VacanJefDbContext Contexto => alcance.ServiceProvider.GetRequiredService<VacanJefDbContext>();

        public void Dispose()
        {
            alcance.Dispose();
            proveedor.Dispose();
        }
    }

    private static async Task<(Escenario Escenario, Usuario Admin)> PrepararAsync()
    {
        var escenario = new Escenario(await CrearServiciosAsync());
        var admin = await CrearUsuarioAsync(escenario.Gestor, "admin@ejemplo.com", Roles.Administrador, "Admin");
        return (escenario, admin);
    }

    [Fact]
    public async Task CrearCuidador_AsignaRolYQuedaActivo()
    {
        var (escenario, admin) = await PrepararAsync();
        using var _ = escenario;

        var resultado = await escenario.Gestion.CrearAsync(
            "Carla Ruiz", "carla@ejemplo.com", "3001112233", Contrasena, Roles.Cuidador, admin.Id);

        Assert.True(resultado.Exito, resultado.Mensaje);
        var cuidador = await escenario.Gestor.FindByEmailAsync("carla@ejemplo.com");
        Assert.NotNull(cuidador);
        Assert.True(cuidador.Activo);
        Assert.True(await escenario.Gestor.IsInRoleAsync(cuidador, Roles.Cuidador));
    }

    [Fact]
    public async Task CrearCuentaDeDueno_SeRechaza()
    {
        // Los dueños se registran solos (HU-001)
        var (escenario, admin) = await PrepararAsync();
        using var _ = escenario;

        var resultado = await escenario.Gestion.CrearAsync(
            "Pedro", "pedro@ejemplo.com", "3001112233", Contrasena, Roles.Dueno, admin.Id);

        Assert.False(resultado.Exito);
        Assert.Null(await escenario.Gestor.FindByEmailAsync("pedro@ejemplo.com"));
    }

    [Fact]
    public async Task CrearConCorreoRepetido_SeRechazaEnEspanol()
    {
        var (escenario, admin) = await PrepararAsync();
        using var _ = escenario;

        var resultado = await escenario.Gestion.CrearAsync(
            "Otro admin", "admin@ejemplo.com", "3001112233", Contrasena, Roles.Administrador, admin.Id);

        Assert.False(resultado.Exito);
        Assert.Contains("Ya existe una cuenta", resultado.Mensaje);
    }

    [Fact]
    public async Task Crear_QuedaEnAuditoriaConElAdministrador()
    {
        var (escenario, admin) = await PrepararAsync();
        using var _ = escenario;

        await escenario.Gestion.CrearAsync(
            "Carla Ruiz", "carla@ejemplo.com", "3001112233", Contrasena, Roles.Cuidador, admin.Id);

        var registro = await escenario.Contexto.Auditorias.SingleAsync();
        Assert.Equal(admin.Id, registro.UsuarioId);
        Assert.Equal(GestionUsuarios.AccionCrear, registro.Accion);
        Assert.StartsWith("Usuario #", registro.Recurso);
    }

    [Fact]
    public async Task CambiarRol_ReemplazaElRolAnterior()
    {
        var (escenario, admin) = await PrepararAsync();
        using var _ = escenario;
        var usuario = await CrearUsuarioAsync(escenario.Gestor, "ana@ejemplo.com", Roles.Dueno);

        var resultado = await escenario.Gestion.CambiarRolAsync(usuario.Id, Roles.Cuidador, admin.Id);

        Assert.True(resultado.Exito, resultado.Mensaje);
        var roles = await escenario.Gestor.GetRolesAsync(usuario);
        Assert.Equal(new[] { Roles.Cuidador }, roles);
    }

    [Fact]
    public async Task CambiarSuPropioRol_SeRechaza()
    {
        var (escenario, admin) = await PrepararAsync();
        using var _ = escenario;

        var resultado = await escenario.Gestion.CambiarRolAsync(admin.Id, Roles.Dueno, admin.Id);

        Assert.False(resultado.Exito);
        Assert.True(await escenario.Gestor.IsInRoleAsync(admin, Roles.Administrador));
    }

    [Fact]
    public async Task CambiarARolInexistente_SeRechaza()
    {
        var (escenario, admin) = await PrepararAsync();
        using var _ = escenario;
        var usuario = await CrearUsuarioAsync(escenario.Gestor, "ana@ejemplo.com", Roles.Dueno);

        var resultado = await escenario.Gestion.CambiarRolAsync(usuario.Id, "SuperUsuario", admin.Id);

        Assert.False(resultado.Exito);
    }

    [Fact]
    public async Task Desactivar_MarcaInactivoYCierraSusSesiones()
    {
        var (escenario, admin) = await PrepararAsync();
        using var _ = escenario;
        var usuario = await CrearUsuarioAsync(escenario.Gestor, "ana@ejemplo.com", Roles.Dueno);
        var selloAnterior = usuario.SecurityStamp;

        var resultado = await escenario.Gestion.CambiarEstadoAsync(usuario.Id, activo: false, admin.Id);

        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.False(usuario.Activo);
        Assert.NotEqual(selloAnterior, usuario.SecurityStamp);
    }

    [Fact]
    public async Task Activar_DevuelveElAcceso()
    {
        var (escenario, admin) = await PrepararAsync();
        using var _ = escenario;
        var usuario = await CrearUsuarioAsync(escenario.Gestor, "ana@ejemplo.com", Roles.Dueno);
        await escenario.Gestion.CambiarEstadoAsync(usuario.Id, activo: false, admin.Id);

        var resultado = await escenario.Gestion.CambiarEstadoAsync(usuario.Id, activo: true, admin.Id);

        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.True(usuario.Activo);
    }

    [Fact]
    public async Task DesactivarseASiMismo_SeRechaza()
    {
        var (escenario, admin) = await PrepararAsync();
        using var _ = escenario;

        var resultado = await escenario.Gestion.CambiarEstadoAsync(admin.Id, activo: false, admin.Id);

        Assert.False(resultado.Exito);
        Assert.True(admin.Activo);
    }

    [Fact]
    public async Task Desbloquear_QuitaElBloqueoYReiniciaLosIntentos()
    {
        var (escenario, admin) = await PrepararAsync();
        using var _ = escenario;
        var usuario = await CrearUsuarioAsync(escenario.Gestor, "ana@ejemplo.com", Roles.Dueno);
        for (var i = 0; i < OpcionesIdentity.MaximoIntentosFallidos; i++)
            await escenario.Gestor.AccessFailedAsync(usuario);
        Assert.True(await escenario.Gestor.IsLockedOutAsync(usuario));

        var resultado = await escenario.Gestion.DesbloquearAsync(usuario.Id, admin.Id);

        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.False(await escenario.Gestor.IsLockedOutAsync(usuario));
        Assert.Equal(0, await escenario.Gestor.GetAccessFailedCountAsync(usuario));
    }

    [Fact]
    public async Task Listar_MuestraRolEstadoYBloqueo()
    {
        var (escenario, admin) = await PrepararAsync();
        using var _ = escenario;
        var usuario = await CrearUsuarioAsync(escenario.Gestor, "ana@ejemplo.com", Roles.Dueno, "Ana");
        for (var i = 0; i < OpcionesIdentity.MaximoIntentosFallidos; i++)
            await escenario.Gestor.AccessFailedAsync(usuario);

        var lista = await escenario.Gestion.ListarAsync();

        var ana = Assert.Single(lista, u => u.Correo == "ana@ejemplo.com");
        Assert.Equal(Roles.Dueno, ana.Rol);
        Assert.True(ana.Activo);
        Assert.True(ana.Bloqueado);
        Assert.Equal(Roles.Administrador, Assert.Single(lista, u => u.Id == admin.Id).Rol);
    }

    [Fact]
    public async Task Listar_FiltraPorRolYPorTexto()
    {
        var (escenario, _) = await PrepararAsync();
        using var __ = escenario;
        await CrearUsuarioAsync(escenario.Gestor, "ana@ejemplo.com", Roles.Dueno, "Ana");
        await CrearUsuarioAsync(escenario.Gestor, "carla@ejemplo.com", Roles.Cuidador, "Carla");

        var cuidadores = await escenario.Gestion.ListarAsync(rol: Roles.Cuidador);
        var buscados = await escenario.Gestion.ListarAsync(busqueda: "ana@");

        Assert.Equal("Carla", Assert.Single(cuidadores).Nombre);
        Assert.Equal("Ana", Assert.Single(buscados).Nombre);
    }

    [Fact]
    public async Task SemillaAdministrador_CreaElPrimeroSoloUnaVez()
    {
        using var proveedor = await CrearServiciosAsync();
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SemillaAdministrador.VariableCorreo] = "jefe@ejemplo.com",
                [SemillaAdministrador.VariableContrasena] = Contrasena,
            })
            .Build();

        Assert.True(await SemillaAdministrador.CrearAsync(proveedor, configuracion));
        Assert.False(await SemillaAdministrador.CrearAsync(proveedor, configuracion));

        using var alcance = proveedor.CreateScope();
        var gestor = alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        Assert.Single(await gestor.GetUsersInRoleAsync(Roles.Administrador));
    }

    [Fact]
    public async Task SemillaAdministrador_SinVariables_NoHaceNada()
    {
        using var proveedor = await CrearServiciosAsync();
        var configuracion = new ConfigurationBuilder().Build();

        Assert.False(await SemillaAdministrador.CrearAsync(proveedor, configuracion));
    }
}
