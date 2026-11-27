using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VacanJef.Core.Entities;
using VacanJef.Infrastructure.Data;

namespace VacanJef.Tests.Integracion;

// TT-012: levanta la aplicación completa (Program.cs real, middleware, filtros, Identity, SignalR)
// en memoria, en ambiente Production, con una base EF InMemory propia. Nunca usa VACANJEF_DB.
public partial class FabricaVacanJef : WebApplicationFactory<Program>
{
    public const string Contrasena = "Prueba#2026x";

    private readonly string nombreBase = "Integracion-" + Guid.NewGuid().ToString("N");
    private readonly string carpetaTemporal =
        Path.Combine(Path.GetTempPath(), "vacanjef-integracion", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        // UseSetting se aplica antes de que Program.cs lea la configuración,
        // y reemplaza las variables de entorno del PC (VACANJEF_DB, administrador, demo).
        builder.UseSetting("ConnectionStrings:VacanJef", "Server=no-se-usa;Database=Integracion");
        builder.UseSetting("Registros:Ruta", Path.Combine(carpetaTemporal, "registros"));
        builder.UseSetting("Fotos:Ruta", Path.Combine(carpetaTemporal, "fotos"));
        builder.UseSetting("VACANJEF_ADMIN_CORREO", string.Empty);
        builder.UseSetting("VACANJEF_ADMIN_CONTRASENA", string.Empty);
        builder.UseSetting("VACANJEF_DEMO", "false");

        builder.ConfigureServices(servicios =>
        {
            // Quita SQL Server (AddInfrastructure) y deja EF InMemory
            servicios.RemoveAll<DbContextOptions<VacanJefDbContext>>();
            servicios.RemoveAll<IDbContextOptionsConfiguration<VacanJefDbContext>>();
            servicios.AddDbContext<VacanJefDbContext>(opciones => opciones.UseInMemoryDatabase(nombreBase));
        });
    }

    // Cliente HTTPS que no sigue redirecciones (para revisarlas) y guarda las cookies
    public HttpClient CrearCliente() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost"),
    });

    public async Task<string> CrearUsuarioAsync(string rol)
    {
        using var alcance = Services.CreateScope();
        var gestor = alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var correo = $"prueba-{Guid.NewGuid():N}@ejemplo.com";
        var usuario = new Usuario { UserName = correo, Email = correo, Nombre = "Persona de prueba", EmailConfirmed = true };

        var creacion = await gestor.CreateAsync(usuario, Contrasena);
        Assert.True(creacion.Succeeded, string.Join(" ", creacion.Errors.Select(e => e.Description)));
        await gestor.AddToRoleAsync(usuario, rol);
        return correo;
    }

    public static async Task<HttpResponseMessage> IniciarSesionAsync(
        HttpClient cliente, string correo, string? returnUrl = null)
    {
        var formulario = await cliente.GetStringAsync("/Cuenta/IniciarSesion");
        var datos = new Dictionary<string, string>
        {
            ["Correo"] = correo,
            ["Contrasena"] = Contrasena,
            ["__RequestVerificationToken"] = TokenAntifalsificacion(formulario),
        };
        if (returnUrl is not null)
        {
            datos["ReturnUrl"] = returnUrl;
        }

        return await cliente.PostAsync("/Cuenta/IniciarSesion", new FormUrlEncodedContent(datos));
    }

    public static string TokenAntifalsificacion(string html)
    {
        var token = ExpresionToken().Match(html).Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token), "El formulario no tiene el token antifalsificación.");
        return token;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            if (Directory.Exists(carpetaTemporal))
            {
                Directory.Delete(carpetaTemporal, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Serilog puede tener el archivo abierto un momento más: se queda en la carpeta temporal
        }
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*?value=\"([^\"]+)\"")]
    private static partial Regex ExpresionToken();
}
