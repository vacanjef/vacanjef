namespace VacanJef.Web.Seguridad;

// Cabeceras HTTP de seguridad (RNF-006): evitan XSS, clickjacking y fugas de información
public class CabecerasSeguridadMiddleware(RequestDelegate siguiente)
{
    public Task InvokeAsync(HttpContext contexto)
    {
        var cabeceras = contexto.Response.Headers;
        cabeceras["X-Content-Type-Options"] = "nosniff";
        cabeceras["X-Frame-Options"] = "DENY";
        cabeceras["Referrer-Policy"] = "strict-origin-when-cross-origin";
        cabeceras["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        cabeceras["Content-Security-Policy"] =
            "default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; " +
            "connect-src 'self' ws: wss:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

        return siguiente(contexto);
    }
}
