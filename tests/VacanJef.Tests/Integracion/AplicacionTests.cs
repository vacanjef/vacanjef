using System.Net;
using VacanJef.Core.Constantes;

namespace VacanJef.Tests.Integracion;

// TT-012: pruebas de integración de la aplicación completa por HTTP.
// Verifican lo que las pruebas unitarias no ven: el orden del middleware, las cabeceras,
// la autorización real de cada ruta, la cookie de sesión, CSRF y el hub de SignalR.
public class AplicacionTests(FabricaVacanJef fabrica) : IClassFixture<FabricaVacanJef>
{
    [Fact]
    public async Task PaginaPrincipal_RespondeConCabecerasDeSeguridad()
    {
        using var cliente = fabrica.CrearCliente();

        var respuesta = await cliente.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var csp = Assert.Single(respuesta.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("script-src 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.DoesNotContain("unsafe-inline", csp);
        Assert.Equal("DENY", Assert.Single(respuesta.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("nosniff", Assert.Single(respuesta.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("strict-origin-when-cross-origin", Assert.Single(respuesta.Headers.GetValues("Referrer-Policy")));
    }

    [Fact]
    public async Task Salud_RespondeSaludableEnJson()
    {
        using var cliente = fabrica.CrearCliente();

        var respuesta = await cliente.GetAsync("/salud");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/json", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Contains("\"estado\":\"Saludable\"", await respuesta.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/Mascotas")]
    [InlineData("/Reservas")]
    [InlineData("/Observaciones")]
    [InlineData("/Notificaciones")]
    [InlineData("/Usuarios")]
    [InlineData("/Auditoria")]
    public async Task RutaProtegida_SinSesion_RedirigeAlInicioDeSesion(string ruta)
    {
        using var cliente = fabrica.CrearCliente();

        var respuesta = await cliente.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.StartsWith("https://localhost/Cuenta/IniciarSesion", respuesta.Headers.Location?.ToString());
    }

    [Fact]
    public async Task GuiaEstilos_EnProduccion_NoExiste()
    {
        using var cliente = fabrica.CrearCliente();

        var respuesta = await cliente.GetAsync("/Home/GuiaEstilos");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task IniciarSesion_SinTokenAntifalsificacion_SeRechaza()
    {
        using var cliente = fabrica.CrearCliente();
        var datos = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Correo"] = "alguien@ejemplo.com",
            ["Contrasena"] = FabricaVacanJef.Contrasena,
        });

        var respuesta = await cliente.PostAsync("/Cuenta/IniciarSesion", datos);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task IniciarSesion_Correcto_CreaCookieSeguraYAbreNotificaciones()
    {
        var correo = await fabrica.CrearUsuarioAsync(Roles.Dueno);
        using var cliente = fabrica.CrearCliente();

        var respuesta = await FabricaVacanJef.IniciarSesionAsync(cliente, correo);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/", respuesta.Headers.Location?.OriginalString);
        var cookie = respuesta.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith("VacanJef.Sesion=", StringComparison.Ordinal));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);

        var notificaciones = await cliente.GetAsync("/Notificaciones");
        Assert.Equal(HttpStatusCode.OK, notificaciones.StatusCode);
    }

    [Fact]
    public async Task IniciarSesion_ConReturnUrlExterna_NoSaleDelSitio()
    {
        var correo = await fabrica.CrearUsuarioAsync(Roles.Dueno);
        using var cliente = fabrica.CrearCliente();

        var respuesta = await FabricaVacanJef.IniciarSesionAsync(cliente, correo, "https://sitio-malicioso.example/robar");

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/", respuesta.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task IniciarSesion_ContrasenaIncorrecta_MensajeGenericoSinCookie()
    {
        var correo = await fabrica.CrearUsuarioAsync(Roles.Dueno);
        using var cliente = fabrica.CrearCliente();
        var formulario = await cliente.GetStringAsync("/Cuenta/IniciarSesion");

        var respuesta = await cliente.PostAsync("/Cuenta/IniciarSesion", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Correo"] = correo,
                ["Contrasena"] = "Equivocada#2026",
                ["__RequestVerificationToken"] = FabricaVacanJef.TokenAntifalsificacion(formulario),
            }));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        // Razor codifica la ñ como &#xF1;: se decodifica antes de comparar
        Assert.Contains("Correo o contraseña incorrectos.",
            WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync()));
        Assert.False(respuesta.Headers.TryGetValues("Set-Cookie", out var cookies)
                     && cookies.Any(c => c.StartsWith("VacanJef.Sesion=", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Dueno_EnRutaDeAdministrador_VaAAccesoDenegado()
    {
        var correo = await fabrica.CrearUsuarioAsync(Roles.Dueno);
        using var cliente = fabrica.CrearCliente();
        await FabricaVacanJef.IniciarSesionAsync(cliente, correo);

        var respuesta = await cliente.GetAsync("/Usuarios");

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.StartsWith("https://localhost/Cuenta/AccesoDenegado", respuesta.Headers.Location?.ToString());
    }

    [Fact]
    public async Task HubNotificaciones_SinSesion_NoPermiteConectarse()
    {
        using var cliente = fabrica.CrearCliente();

        var respuesta = await cliente.PostAsync("/hubs/notificaciones/negotiate?negotiateVersion=1", null);

        Assert.NotEqual(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect,
            $"Se esperaba 401 o redirección al inicio de sesión y llegó {(int)respuesta.StatusCode}.");
    }

    [Fact]
    public async Task HubNotificaciones_ConSesion_Negocia()
    {
        var correo = await fabrica.CrearUsuarioAsync(Roles.Dueno);
        using var cliente = fabrica.CrearCliente();
        await FabricaVacanJef.IniciarSesionAsync(cliente, correo);

        var respuesta = await cliente.PostAsync("/hubs/notificaciones/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("connectionToken", await respuesta.Content.ReadAsStringAsync());
    }
}
