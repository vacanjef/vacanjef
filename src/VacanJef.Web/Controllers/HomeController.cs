using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VacanJef.Web.Models;

namespace VacanJef.Web.Controllers;

public class HomeController(IWebHostEnvironment entorno) : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    // TT-003: guía de estilos, solo disponible en desarrollo
    public IActionResult GuiaEstilos()
    {
        return entorno.IsDevelopment() ? View() : NotFound();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
