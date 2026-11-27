using System.Text;

namespace VacanJef.Web.Servicios;

// HU-007: VacanJef reenvía la oferta WebRTC del navegador al endpoint WHEP de MediaMTX
// (POST {base}/{ruta}/whep con Content-Type application/sdp) y devuelve la respuesta.
// El video viaja directo entre MediaMTX y el navegador; VacanJef solo hace la negociación.
public class ClienteMediaMtx(string urlBase, HttpClient? http = null)
{
    public const string SeccionConfiguracion = "Monitoreo:MediaMtxWebRtc";
    public const string UrlPorDefecto = "http://localhost:8889";

    private static readonly HttpClient Compartido = new() { Timeout = TimeSpan.FromSeconds(8) };

    private readonly HttpClient cliente = http ?? Compartido;

    public static string UrlDesde(IConfiguration configuracion) =>
        (configuracion[SeccionConfiguracion] ?? UrlPorDefecto).TrimEnd('/');

    // Devuelve la respuesta SDP, o null si MediaMTX no está disponible o la cámara no transmite
    public async Task<string?> NegociarAsync(string ruta, string oferta, CancellationToken cancelacion = default)
    {
        try
        {
            using var contenido = new StringContent(oferta, Encoding.UTF8);
            contenido.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/sdp");

            using var respuesta = await cliente.PostAsync($"{urlBase.TrimEnd('/')}/{ruta}/whep", contenido, cancelacion);
            if (!respuesta.IsSuccessStatusCode)
                return null;

            var sdp = await respuesta.Content.ReadAsStringAsync(cancelacion);
            return sdp.StartsWith("v=0", StringComparison.Ordinal) ? sdp : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }
}
