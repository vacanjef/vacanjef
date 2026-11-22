using Microsoft.AspNetCore.Builder;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VacanJef.Web.Seguridad;

namespace VacanJef.Tests.Web;

// TT-003 (Azure): la app detrás del proxy de App Service conoce el esquema y la IP originales
public class ProxyTests
{
    private static async Task<HttpContext> ProcesarAsync(Action<HttpContext> preparar)
    {
        var opciones = new ForwardedHeadersOptions();
        OpcionesProxy.Configurar(opciones);

        HttpContext? recibido = null;
        var middleware = new ForwardedHeadersMiddleware(
            contexto => { recibido = contexto; return Task.CompletedTask; },
            NullLoggerFactory.Instance,
            Options.Create(opciones));

        var contexto = new DefaultHttpContext();
        contexto.Request.Scheme = "http";
        contexto.Connection.RemoteIpAddress = IPAddress.Parse("169.254.130.1"); // IP interna del proxy
        preparar(contexto);

        await middleware.Invoke(contexto);
        return recibido!;
    }

    [Fact]
    public void Configuracion_ReenviaEsquemaEIpYConfiaEnElProxyDeAzure()
    {
        var opciones = new ForwardedHeadersOptions();
        OpcionesProxy.Configurar(opciones);

        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, opciones.ForwardedHeaders);
        Assert.Empty(opciones.KnownIPNetworks);
        Assert.Empty(opciones.KnownProxies);
    }

    [Fact]
    public async Task PeticionHttpsDesdeElProxy_SeVeComoHttps()
    {
        var contexto = await ProcesarAsync(c => c.Request.Headers["X-Forwarded-Proto"] = "https");

        Assert.Equal("https", contexto.Request.Scheme);
        Assert.True(contexto.Request.IsHttps);
    }

    [Fact]
    public async Task IpDelUsuario_SeTomaDeXForwardedFor()
    {
        var contexto = await ProcesarAsync(c => c.Request.Headers["X-Forwarded-For"] = "190.85.10.20");

        Assert.Equal(IPAddress.Parse("190.85.10.20"), contexto.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task SinCabeceras_NoCambiaNada()
    {
        var contexto = await ProcesarAsync(_ => { });

        Assert.Equal("http", contexto.Request.Scheme);
        Assert.Equal(IPAddress.Parse("169.254.130.1"), contexto.Connection.RemoteIpAddress);
    }
}
