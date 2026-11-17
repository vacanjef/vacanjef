using System.Security.Claims;
using VacanJef.Core.Constantes;
using VacanJef.Web.Navegacion;

namespace VacanJef.Tests.Web;

// TT-003: el menú principal depende del rol del usuario
public class MenuNavegacionTests
{
    private static ClaimsPrincipal UsuarioConRoles(params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, "prueba@ejemplo.com") };
        claims.AddRange(roles.Select(rol => new Claim(ClaimTypes.Role, rol)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Prueba"));
    }

    private static IEnumerable<string> Rutas(ClaimsPrincipal usuario) =>
        MenuNavegacion.Para(usuario).Select(e => e.Ruta);

    [Fact]
    public void Anonimo_NoTieneMenu()
    {
        Assert.Empty(MenuNavegacion.Para(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Fact]
    public void UsuarioSinRol_NoTieneMenu()
    {
        Assert.Empty(MenuNavegacion.Para(UsuarioConRoles()));
    }

    [Fact]
    public void Dueno_VeMascotasYNoVeAdministracion()
    {
        var rutas = Rutas(UsuarioConRoles(Roles.Dueno)).ToList();

        Assert.Contains("/Mascotas", rutas);
        Assert.Contains("/Notificaciones", rutas);
        Assert.DoesNotContain("/Usuarios", rutas);
        Assert.DoesNotContain("/Auditoria", rutas);
    }

    [Fact]
    public void Cuidador_VeObservacionesYNoVeAdministracion()
    {
        var rutas = Rutas(UsuarioConRoles(Roles.Cuidador)).ToList();

        Assert.Contains("/Observaciones", rutas);
        Assert.DoesNotContain("/Usuarios", rutas);
    }

    [Fact]
    public void Administrador_VeUsuariosYAuditoria()
    {
        var rutas = Rutas(UsuarioConRoles(Roles.Administrador)).ToList();

        Assert.Contains("/Usuarios", rutas);
        Assert.Contains("/Auditoria", rutas);
    }

    [Fact]
    public void VariosRoles_GanaElDeMayorNivel()
    {
        var rutas = Rutas(UsuarioConRoles(Roles.Dueno, Roles.Administrador)).ToList();

        Assert.Contains("/Usuarios", rutas);
        Assert.DoesNotContain("/Mascotas", rutas);
    }

    [Theory]
    [InlineData("/Mascotas", true)]
    [InlineData("/mascotas", true)]
    [InlineData("/Mascotas/Crear", true)]
    [InlineData("/MascotasPerdidas", false)]
    [InlineData("/", false)]
    [InlineData(null, false)]
    public void EstaActivo_ReconoceRutaYSubrutas(string? rutaActual, bool esperado)
    {
        var elemento = new ElementoMenu("Mis mascotas", "/Mascotas");

        Assert.Equal(esperado, MenuNavegacion.EstaActivo(elemento, rutaActual));
    }
}
