using Microsoft.AspNetCore.HttpOverrides;

namespace VacanJef.Web.Seguridad;

// TT-003 (Azure): App Service recibe el HTTPS y reenvía la petición a la app por HTTP.
// Con estas cabeceras la app conoce el esquema original (https) y la IP real del usuario,
// así HttpsRedirection y HSTS funcionan y los registros guardan la IP correcta.
public static class OpcionesProxy
{
    public static void Configurar(ForwardedHeadersOptions opciones)
    {
        opciones.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        // El proxy de App Service no tiene una IP fija conocida: se confía en el que está delante.
        // La app no queda expuesta directamente a internet, solo a través de ese proxy.
        opciones.KnownIPNetworks.Clear();
        opciones.KnownProxies.Clear();
    }
}
