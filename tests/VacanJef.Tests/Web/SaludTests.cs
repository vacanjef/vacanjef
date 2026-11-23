using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Salud;

namespace VacanJef.Tests.Web;

// TT-008: health check en /salud
public class SaludTests
{
    private static async Task<JsonDocument> EscribirAsync(HealthReport reporte)
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();

        await RespuestaSalud.EscribirAsync(contexto, reporte);

        contexto.Response.Body.Position = 0;
        var json = await new StreamReader(contexto.Response.Body).ReadToEndAsync();
        return JsonDocument.Parse(json);
    }

    private static HealthReport Reporte(HealthStatus estado, Exception? error = null) => new(
        new Dictionary<string, HealthReportEntry>
        {
            [RespuestaSalud.ChequeoBaseDeDatos] =
                new(estado, "detalle interno", TimeSpan.FromMilliseconds(5), error, null),
        },
        TimeSpan.FromMilliseconds(5));

    [Fact]
    public async Task ChequeoBaseDeDatos_ConBaseDisponible_EsSaludable()
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddDbContext<VacanJefDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        servicios.AddHealthChecks().AddDbContextCheck<VacanJefDbContext>(RespuestaSalud.ChequeoBaseDeDatos);

        await using var proveedor = servicios.BuildServiceProvider();
        var reporte = await proveedor.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        Assert.Equal(HealthStatus.Healthy, reporte.Status);
        Assert.Contains(RespuestaSalud.ChequeoBaseDeDatos, reporte.Entries.Keys);
    }

    [Fact]
    public async Task Respuesta_Saludable_IndicaEstadoYComponentes()
    {
        using var json = await EscribirAsync(Reporte(HealthStatus.Healthy));

        Assert.Equal("Saludable", json.RootElement.GetProperty("estado").GetString());
        Assert.Equal("Saludable", json.RootElement.GetProperty("componentes")
            .GetProperty(RespuestaSalud.ChequeoBaseDeDatos).GetString());
    }

    [Fact]
    public async Task Respuesta_ConFalla_NoMuestraDetallesInternos()
    {
        var error = new InvalidOperationException("Server=sql;Password=secreta");

        using var json = await EscribirAsync(Reporte(HealthStatus.Unhealthy, error));
        var texto = json.RootElement.GetRawText();

        Assert.Equal("No disponible", json.RootElement.GetProperty("estado").GetString());
        Assert.DoesNotContain("secreta", texto);
        Assert.DoesNotContain("detalle interno", texto);
    }

    [Theory]
    [InlineData(HealthStatus.Healthy, "Saludable")]
    [InlineData(HealthStatus.Degraded, "Degradado")]
    [InlineData(HealthStatus.Unhealthy, "No disponible")]
    public void Traducir_DevuelveEstadoEnEspanol(HealthStatus estado, string esperado)
    {
        Assert.Equal(esperado, RespuestaSalud.Traducir(estado));
    }
}
