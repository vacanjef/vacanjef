using VacanJef.Core.Enums;

namespace VacanJef.Web.Models.GestionReservas;

public static class PresentacionGestionReservas
{
    public static string Servicio(TipoServicio servicio) => servicio switch
    {
        TipoServicio.Basico => "Básico",
        TipoServicio.Premium => "Premium",
        TipoServicio.Spa => "Spa",
        _ => servicio.ToString(),
    };

    public static string ClaseOcupacion(OcupacionDia dia) => dia.Lleno
        ? "text-bg-danger"
        : dia.Porcentaje >= 80 ? "text-bg-warning" : "text-bg-success";

    public static string DiaCorto(DateOnly fecha) => fecha.DayOfWeek switch
    {
        DayOfWeek.Monday => "lun",
        DayOfWeek.Tuesday => "mar",
        DayOfWeek.Wednesday => "mié",
        DayOfWeek.Thursday => "jue",
        DayOfWeek.Friday => "vie",
        DayOfWeek.Saturday => "sáb",
        _ => "dom",
    };
}
