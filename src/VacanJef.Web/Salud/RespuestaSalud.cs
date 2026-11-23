using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace VacanJef.Web.Salud;

// TT-008 (Manual Técnico): respuesta de /salud.
// Solo informa el estado; nunca muestra mensajes de error ni datos de conexión.
public static class RespuestaSalud
{
    public const string Ruta = "/salud";
    public const string ChequeoBaseDeDatos = "base-de-datos";

    public static Task EscribirAsync(HttpContext contexto, HealthReport reporte)
    {
        contexto.Response.ContentType = "application/json; charset=utf-8";

        var respuesta = new
        {
            estado = Traducir(reporte.Status),
            duracionMs = Math.Round(reporte.TotalDuration.TotalMilliseconds),
            componentes = reporte.Entries.ToDictionary(e => e.Key, e => Traducir(e.Value.Status)),
        };

        return contexto.Response.WriteAsync(JsonSerializer.Serialize(respuesta));
    }

    public static string Traducir(HealthStatus estado) => estado switch
    {
        HealthStatus.Healthy => "Saludable",
        HealthStatus.Degraded => "Degradado",
        _ => "No disponible",
    };
}
