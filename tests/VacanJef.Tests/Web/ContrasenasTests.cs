using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VacanJef.Core.Constantes;
using VacanJef.Core.Entities;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Seguridad;
using VacanJef.Web.Servicios;

namespace VacanJef.Tests.Web;

// HU-016: cambio de contraseña del titular y restablecimiento por el administrador
public class ContrasenasTests
{
    private const string Inicial = "Vacanjef#2026";
    private const string Nueva = "OtraClave#2027";

    private sealed class Escenario : IDisposable
    {
        private readonly ServiceProvider proveedor;
        private readonly IServiceScope alcance;

        public Escenario()
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
            servicios.AddScoped<ServicioContrasenas>();
            proveedor = servicios.BuildServiceProvider();
            alcance = proveedor.CreateScope();
        }

        public UserManager<Usuario> Gestor => alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        public ServicioContrasenas Contrasenas => alcance.ServiceProvider.GetRequiredService<ServicioContrasenas>();
        public GestionUsuarios Gestion => alcance.ServiceProvider.GetRequiredService<GestionUsuarios>();
        public VacanJefDbContext Contexto => alcance.ServiceProvider.GetRequiredService<VacanJefDbContext>();

        public async Task<Usuario> CrearUsuarioAsync(string correo)
        {
            var usuario = new Usuario { UserName = correo, Email = correo, Nombre = "Prueba", PhoneNumber = "3001234567" };
            Assert.True((await Gestor.CreateAsync(usuario, Inicial)).Succeeded);
            return usuario;
        }

        public async Task<bool> TieneMarcaAsync(Usuario usuario) =>
            (await Gestor.GetClaimsAsync(usuario)).Any(c => c.Type == ContrasenaObligatoria.TipoClaim);

        public void Dispose()
        {
            alcance.Dispose();
            proveedor.Dispose();
        }
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims.Prepend(new Claim(ClaimTypes.Name, "ana@ejemplo.com")), "Prueba"));

    [Fact]
    public async Task Cambiar_ConDatosCorrectos_AceptaLaNueva()
    {
        using var escenario = new Escenario();
        var usuario = await escenario.CrearUsuarioAsync("ana@ejemplo.com");

        var resultado = await escenario.Contrasenas.CambiarAsync(usuario, Inicial, Nueva);

        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.True(await escenario.Gestor.CheckPasswordAsync(usuario, Nueva));
        Assert.False(await escenario.Gestor.CheckPasswordAsync(usuario, Inicial));
    }

    [Fact]
    public async Task Cambiar_ConActualIncorrecta_SeRechazaEnEspanol()
    {
        using var escenario = new Escenario();
        var usuario = await escenario.CrearUsuarioAsync("ana@ejemplo.com");

        var resultado = await escenario.Contrasenas.CambiarAsync(usuario, "Equivocada#1", Nueva);

        Assert.False(resultado.Exito);
        Assert.Equal("La contraseña actual no es correcta.", resultado.Mensaje);
    }

    [Fact]
    public async Task Cambiar_PorLaMisma_SeRechaza()
    {
        using var escenario = new Escenario();
        var usuario = await escenario.CrearUsuarioAsync("ana@ejemplo.com");

        var resultado = await escenario.Contrasenas.CambiarAsync(usuario, Inicial, Inicial);

        Assert.False(resultado.Exito);
    }

    [Fact]
    public async Task Cambiar_PorUnaDebil_SeRechaza()
    {
        using var escenario = new Escenario();
        var usuario = await escenario.CrearUsuarioAsync("ana@ejemplo.com");

        var resultado = await escenario.Contrasenas.CambiarAsync(usuario, Inicial, "clave123");

        Assert.False(resultado.Exito);
        Assert.True(await escenario.Gestor.CheckPasswordAsync(usuario, Inicial));
    }

    [Fact]
    public async Task Cambiar_QuitaLaMarcaYQuedaEnAuditoria()
    {
        using var escenario = new Escenario();
        var usuario = await escenario.CrearUsuarioAsync("ana@ejemplo.com");
        await escenario.Gestor.AddClaimAsync(usuario, ContrasenaObligatoria.CrearMarca());

        await escenario.Contrasenas.CambiarAsync(usuario, Inicial, Nueva);

        Assert.False(await escenario.TieneMarcaAsync(usuario));
        var registro = await escenario.Contexto.Auditorias.SingleAsync();
        Assert.Equal(ServicioContrasenas.AccionCambiar, registro.Accion);
        Assert.Equal(usuario.Id, registro.UsuarioId);
    }

    [Fact]
    public async Task CuentaCreadaPorElAdministrador_DebeCambiarLaContrasena()
    {
        using var escenario = new Escenario();
        await SemillaRolesEnAsync(escenario);
        var admin = await escenario.CrearUsuarioAsync("admin@ejemplo.com");

        await escenario.Gestion.CrearAsync("Carla", "carla@ejemplo.com", "3001112233", Inicial, Roles.Cuidador, admin.Id);

        var cuidador = await escenario.Gestor.FindByEmailAsync("carla@ejemplo.com");
        Assert.NotNull(cuidador);
        Assert.True(await escenario.TieneMarcaAsync(cuidador));
    }

    [Fact]
    public async Task Restablecer_AsignaTemporalMarcaYDesbloquea()
    {
        using var escenario = new Escenario();
        var admin = await escenario.CrearUsuarioAsync("admin@ejemplo.com");
        var usuario = await escenario.CrearUsuarioAsync("ana@ejemplo.com");
        for (var i = 0; i < OpcionesIdentity.MaximoIntentosFallidos; i++)
            await escenario.Gestor.AccessFailedAsync(usuario);

        var resultado = await escenario.Contrasenas.RestablecerAsync(usuario.Id, Nueva, admin.Id);

        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.True(await escenario.Gestor.CheckPasswordAsync(usuario, Nueva));
        Assert.True(await escenario.TieneMarcaAsync(usuario));
        Assert.False(await escenario.Gestor.IsLockedOutAsync(usuario));
    }

    [Fact]
    public async Task Restablecer_ConContrasenaDebil_NoDejaLaCuentaSinContrasena()
    {
        using var escenario = new Escenario();
        var admin = await escenario.CrearUsuarioAsync("admin@ejemplo.com");
        var usuario = await escenario.CrearUsuarioAsync("ana@ejemplo.com");

        var resultado = await escenario.Contrasenas.RestablecerAsync(usuario.Id, "clave123", admin.Id);

        Assert.False(resultado.Exito);
        Assert.True(await escenario.Gestor.CheckPasswordAsync(usuario, Inicial));
    }

    [Fact]
    public async Task Restablecer_LaPropia_SeRechaza()
    {
        using var escenario = new Escenario();
        var admin = await escenario.CrearUsuarioAsync("admin@ejemplo.com");

        var resultado = await escenario.Contrasenas.RestablecerAsync(admin.Id, Nueva, admin.Id);

        Assert.False(resultado.Exito);
        Assert.True(await escenario.Gestor.CheckPasswordAsync(admin, Inicial));
    }

    [Fact]
    public void ConMarca_DebeCambiar()
    {
        Assert.True(ContrasenaObligatoria.DebeCambiar(Principal(ContrasenaObligatoria.CrearMarca())));
        Assert.False(ContrasenaObligatoria.DebeCambiar(Principal()));
        Assert.False(ContrasenaObligatoria.DebeCambiar(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Theory]
    [InlineData("Contrasena", "Cambiar", true)]
    [InlineData("Cuenta", "CerrarSesion", true)]
    [InlineData("Home", "Error", true)]
    [InlineData("Mascotas", "Index", false)]
    [InlineData("Usuarios", "Index", false)]
    [InlineData("DatosPersonales", "Index", false)]
    public void MientrasDebaCambiarla_SoloPuedeIrAPaginasPermitidas(string controlador, string accion, bool permitida)
    {
        Assert.Equal(permitida, ContrasenaObligatoria.RutaPermitida(controlador, accion));
    }

    private static async Task SemillaRolesEnAsync(Escenario escenario)
    {
        var roles = escenario.Contexto.Roles;
        foreach (var rol in new[] { Roles.Dueno, Roles.Cuidador, Roles.Administrador })
            roles.Add(new IdentityRole<int>(rol) { NormalizedName = rol.ToUpperInvariant() });
        await escenario.Contexto.SaveChangesAsync();
    }
}
