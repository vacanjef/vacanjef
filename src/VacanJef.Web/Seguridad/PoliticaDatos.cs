using VacanJef.Core.Entities;

namespace VacanJef.Web.Seguridad;

// TT-004: protocolo de protección de datos personales (Ley 1581 de 2012, Decreto 1074 de 2015).
// Cada autorización, actualización o solicitud del titular queda como prueba en la tabla Auditorias:
// quién, cuándo, qué hizo y bajo qué versión de la política.
public static class PoliticaDatos
{
    public const string Version = "1.0";
    public const string FechaVigencia = "16 de noviembre de 2026";

    // Plazos de la Ley 1581 (arts. 14 y 15), en días hábiles
    public const int DiasHabilesConsulta = 10;
    public const int DiasHabilesReclamo = 15;

    public const string Recurso = "PoliticaDatos";

    public const string AccionAutorizacion = "AutorizacionDatos";
    public const string AccionActualizacion = "ActualizacionDatos";
    public const string AccionSolicitudSupresion = "SolicitudSupresion";

    public const string ResultadoRegistrada = "registrada";

    public static Auditoria Registrar(int usuarioId, string accion) => new()
    {
        UsuarioId = usuarioId,
        Accion = accion,
        Recurso = $"{Recurso} v{Version}",
        Resultado = ResultadoRegistrada,
        CreadoEn = DateTime.UtcNow,
    };

    public static string Describir(string accion) => accion switch
    {
        AccionAutorizacion => "Autorizó el tratamiento de datos",
        AccionActualizacion => "Actualizó sus datos",
        AccionSolicitudSupresion => "Solicitó la supresión de sus datos",
        _ => accion,
    };
}
