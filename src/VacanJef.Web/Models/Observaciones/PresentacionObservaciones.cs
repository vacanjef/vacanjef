using System.Globalization;
using VacanJef.Core.Enums;

namespace VacanJef.Web.Models.Observaciones;

// Textos y formatos de las vistas de observaciones (sin depender de la cultura del servidor)
public static class PresentacionObservaciones
{
    public static string Tipo(TipoObservacion tipo) => tipo switch
    {
        TipoObservacion.Salud => "Salud",
        TipoObservacion.Comportamiento => "Comportamiento",
        TipoObservacion.Alimentacion => "Alimentación",
        TipoObservacion.Actividad => "Actividad",
        _ => tipo.ToString(),
    };

    public static string ClaseTipo(TipoObservacion tipo) => tipo switch
    {
        TipoObservacion.Salud => "text-bg-danger",
        TipoObservacion.Comportamiento => "text-bg-warning",
        TipoObservacion.Alimentacion => "text-bg-success",
        TipoObservacion.Actividad => "text-bg-info",
        _ => "text-bg-secondary",
    };

    public static string Salud(TipoRegistroSalud tipo) => tipo switch
    {
        TipoRegistroSalud.Vacuna => "Vacuna",
        TipoRegistroSalud.Alergia => "Alergia",
        TipoRegistroSalud.Medicamento => "Medicamento",
        _ => tipo.ToString(),
    };

    // CreadoEn se guarda en UTC. Colombia no tiene horario de verano: UTC-5 todo el año.
    public static string FechaHora(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(-5)
            .ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
}
