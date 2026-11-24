using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using VacanJef.Web.Controllers;
using VacanJef.Web.Navegacion;

namespace VacanJef.Tests.Web;

// TT-009: comportamiento separado por ambiente
public class AmbientesTests
{
    private sealed class EntornoFalso(string nombre) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = nombre;
        public string ApplicationName { get; set; } = "VacanJef.Web";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public void Indicador_EnProduccion_NoSeMuestra()
    {
        Assert.Null(IndicadorAmbiente.Etiqueta(new EntornoFalso(Environments.Production)));
    }

    [Fact]
    public void Indicador_EnDesarrollo_DiceDesarrollo()
    {
        Assert.Equal("Desarrollo", IndicadorAmbiente.Etiqueta(new EntornoFalso(Environments.Development)));
    }

    [Fact]
    public void Indicador_EnOtroAmbiente_MuestraSuNombre()
    {
        Assert.Equal("Staging", IndicadorAmbiente.Etiqueta(new EntornoFalso(Environments.Staging)));
    }

    [Fact]
    public void GuiaEstilos_EnDesarrollo_MuestraLaVista()
    {
        var controlador = new HomeController(new EntornoFalso(Environments.Development));

        Assert.IsType<ViewResult>(controlador.GuiaEstilos());
    }

    [Fact]
    public void GuiaEstilos_EnProduccion_NoExiste()
    {
        var controlador = new HomeController(new EntornoFalso(Environments.Production));

        Assert.IsType<NotFoundResult>(controlador.GuiaEstilos());
    }
}
