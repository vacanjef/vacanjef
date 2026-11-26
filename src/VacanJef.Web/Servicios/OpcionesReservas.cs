using Microsoft.Extensions.Configuration;
using VacanJef.Core.Enums;

namespace VacanJef.Web.Servicios;

// HU-005: cupos (RN-003) y tarifas (RN-007) configurables.
// Se leen de la sección "Reservas" de appsettings o de variables de entorno
// (Reservas__CapacidadDiaria, Reservas__TarifaBasico, ...), sin tocar el código.
// Los valores por defecto son provisionales hasta que la guardería piloto confirme los suyos.
public class OpcionesReservas
{
    public const string Seccion = "Reservas";

    // Perros que la guardería puede atender por día
    public int CapacidadDiaria { get; set; } = 10;

    // Tarifas por día en pesos colombianos
    public decimal TarifaBasico { get; set; } = 50000m;
    public decimal TarifaPremium { get; set; } = 80000m;
    public decimal TarifaSpa { get; set; } = 110000m;

    public int DuracionMaximaDias { get; set; } = 60;
    public int AnticipacionMaximaDias { get; set; } = 365;

    public decimal Tarifa(TipoServicio servicio) => servicio switch
    {
        TipoServicio.Basico => TarifaBasico,
        TipoServicio.Premium => TarifaPremium,
        TipoServicio.Spa => TarifaSpa,
        _ => throw new ArgumentOutOfRangeException(nameof(servicio)),
    };

    public static OpcionesReservas Desde(IConfiguration configuracion) =>
        configuracion.GetSection(Seccion).Get<OpcionesReservas>() ?? new OpcionesReservas();
}
