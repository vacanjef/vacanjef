namespace VacanJef.Web.Navegacion;

// TT-009: etiqueta del ambiente en el pie de página, para no confundir una copia local
// o de pruebas con la aplicación publicada. En producción no se muestra nada.
public static class IndicadorAmbiente
{
    public static string? Etiqueta(IHostEnvironment entorno)
    {
        if (entorno.IsProduction())
        {
            return null;
        }

        return entorno.IsDevelopment() ? "Desarrollo" : entorno.EnvironmentName;
    }
}
