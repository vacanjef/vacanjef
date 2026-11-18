using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VacanJef.Core.Constantes;
using VacanJef.Core.Entities;
using VacanJef.Core.Enums;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Controllers;
using VacanJef.Web.Models.Mascotas;
using VacanJef.Web.Servicios;

namespace VacanJef.Tests.Web;

// HU-004 / RN-008: foto de la mascota en JPG o PNG de máximo 5 MB
public class FotoMascotaTests
{
    private static readonly byte[] CabeceraJpg = [0xFF, 0xD8, 0xFF, 0xE0];
    private static readonly byte[] CabeceraPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] CabeceraEjecutable = [0x4D, 0x5A, 0x90, 0x00];

    private static IFormFile Archivo(byte[] cabecera, long tamano, string nombre)
    {
        var datos = new byte[tamano];
        cabecera.CopyTo(datos, 0);
        return new FormFile(new MemoryStream(datos), 0, datos.Length, "Foto", nombre);
    }

    private static AlmacenFotosMascotas AlmacenTemporal() =>
        new(Path.Combine(Path.GetTempPath(), "vacanjef-pruebas", Guid.NewGuid().ToString("N")));

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, ".jpg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, ".png")]
    [InlineData(new byte[] { 0x4D, 0x5A, 0x90, 0x00 }, null)]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38 }, null)]
    public void DetectarFormato_SoloJpgYPng(byte[] cabecera, string? esperado)
    {
        Assert.Equal(esperado, AlmacenFotosMascotas.DetectarFormato(cabecera));
    }

    [Fact]
    public async Task Validar_PngValido_SeAcepta()
    {
        var (extension, error) = await AlmacenFotosMascotas.ValidarAsync(Archivo(CabeceraPng, 2048, "luna.png"));

        Assert.Equal(".png", extension);
        Assert.Null(error);
    }

    [Fact]
    public async Task Validar_MasDe5MB_TieneError()
    {
        var archivo = Archivo(CabeceraJpg, AlmacenFotosMascotas.TamanoMaximoBytes + 1, "grande.jpg");

        var (_, error) = await AlmacenFotosMascotas.ValidarAsync(archivo);

        Assert.Contains("5 MB", error);
    }

    [Fact]
    public async Task Validar_ArchivoVacio_TieneError()
    {
        var (_, error) = await AlmacenFotosMascotas.ValidarAsync(Archivo([], 0, "vacia.jpg"));

        Assert.NotNull(error);
    }

    [Fact]
    public async Task Validar_EjecutableRenombradoAJpg_TieneError()
    {
        var (_, error) = await AlmacenFotosMascotas.ValidarAsync(Archivo(CabeceraEjecutable, 1024, "foto.jpg"));

        Assert.Equal("Solo se permiten fotos JPG o PNG.", error);
    }

    [Theory]
    [InlineData("../appsettings.json")]
    [InlineData("..\\appsettings.json")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("no-existe.png")]
    public void RutaCompleta_RechazaNombresPeligrososOInexistentes(string? nombre)
    {
        Assert.Null(AlmacenTemporal().RutaCompleta(nombre));
    }

    [Fact]
    public async Task Registrar_ConFoto_GuardaArchivoYNombre()
    {
        using var contexto = CrearContexto();
        var almacen = AlmacenTemporal();
        var modelo = ModeloValido();
        modelo.Foto = Archivo(CabeceraJpg, 4096, "toby.jpg");

        var resultado = await CrearControlador(contexto, almacen, duenoId: 1).Registrar(modelo);

        Assert.IsType<RedirectToActionResult>(resultado);
        var guardada = Assert.Single(contexto.Set<Mascota>());
        Assert.NotNull(guardada.FotoUrl);
        Assert.EndsWith(".jpg", guardada.FotoUrl);
        Assert.NotEqual("toby.jpg", guardada.FotoUrl); // nunca se usa el nombre del usuario
        Assert.NotNull(almacen.RutaCompleta(guardada.FotoUrl));
    }

    [Fact]
    public async Task Registrar_ConFotoInvalida_NoGuarda()
    {
        using var contexto = CrearContexto();
        var modelo = ModeloValido();
        modelo.Foto = Archivo(CabeceraEjecutable, 1024, "toby.jpg");

        var controlador = CrearControlador(contexto, AlmacenTemporal(), duenoId: 1);
        var resultado = await controlador.Registrar(modelo);

        Assert.IsType<ViewResult>(resultado);
        Assert.True(controlador.ModelState.ContainsKey(nameof(modelo.Foto)));
        Assert.Empty(contexto.Set<Mascota>());
    }

    [Fact]
    public async Task Foto_Propia_DevuelveArchivo()
    {
        using var contexto = CrearContexto();
        var almacen = AlmacenTemporal();
        var id = await SembrarMascotaConFotoAsync(contexto, almacen, duenoId: 1);

        var resultado = await CrearControlador(contexto, almacen, duenoId: 1).Foto(id);

        var archivo = Assert.IsType<PhysicalFileResult>(resultado);
        Assert.Equal("image/png", archivo.ContentType);
    }

    [Fact]
    public async Task Foto_DeMascotaAjena_NoSeEncuentra()
    {
        using var contexto = CrearContexto();
        var almacen = AlmacenTemporal();
        var id = await SembrarMascotaConFotoAsync(contexto, almacen, duenoId: 1);

        var resultado = await CrearControlador(contexto, almacen, duenoId: 2).Foto(id);

        Assert.IsType<NotFoundResult>(resultado);
    }

    [Fact]
    public async Task CambiarFoto_ReemplazaYEliminaLaAnterior()
    {
        using var contexto = CrearContexto();
        var almacen = AlmacenTemporal();
        var id = await SembrarMascotaConFotoAsync(contexto, almacen, duenoId: 1);
        var anterior = (await contexto.Set<Mascota>().SingleAsync()).FotoUrl;

        var resultado = await CrearControlador(contexto, almacen, duenoId: 1)
            .CambiarFoto(id, new CambiarFotoViewModel { Foto = Archivo(CabeceraJpg, 2048, "nueva.jpg") });

        Assert.IsType<RedirectToActionResult>(resultado);
        var mascota = await contexto.Set<Mascota>().SingleAsync();
        Assert.NotEqual(anterior, mascota.FotoUrl);
        Assert.NotNull(almacen.RutaCompleta(mascota.FotoUrl));
        Assert.Null(almacen.RutaCompleta(anterior));
    }

    [Fact]
    public async Task CambiarFoto_SinArchivo_TieneError()
    {
        using var contexto = CrearContexto();
        var almacen = AlmacenTemporal();
        var id = await SembrarMascotaConFotoAsync(contexto, almacen, duenoId: 1);

        var controlador = CrearControlador(contexto, almacen, duenoId: 1);
        var resultado = await controlador.CambiarFoto(id, new CambiarFotoViewModel());

        Assert.IsType<ViewResult>(resultado);
        Assert.True(controlador.ModelState.ContainsKey(nameof(CambiarFotoViewModel.Foto)));
    }

    [Fact]
    public async Task CambiarFoto_DeMascotaAjena_NoSeEncuentra()
    {
        using var contexto = CrearContexto();
        var almacen = AlmacenTemporal();
        var id = await SembrarMascotaConFotoAsync(contexto, almacen, duenoId: 1);

        var resultado = await CrearControlador(contexto, almacen, duenoId: 2)
            .CambiarFoto(id, new CambiarFotoViewModel { Foto = Archivo(CabeceraJpg, 2048, "nueva.jpg") });

        Assert.IsType<NotFoundResult>(resultado);
    }

    private static RegistroMascotaViewModel ModeloValido() => new()
    {
        Nombre = "Toby",
        Raza = "Criollo",
        Edad = 3,
        Peso = "12.5",
        Sexo = SexoMascota.Macho,
        RegistrosSalud =
        [
            new() { Tipo = TipoRegistroSalud.Vacuna, Descripcion = "Rabia", Fecha = new DateOnly(2026, 3, 10) },
        ],
    };

    private static async Task<int> SembrarMascotaConFotoAsync(VacanJefDbContext contexto, AlmacenFotosMascotas almacen, int duenoId)
    {
        var foto = await almacen.GuardarAsync(Archivo(CabeceraPng, 1024, "luna.png"), ".png");
        var mascota = new Mascota { DuenoId = duenoId, Nombre = "Luna", Raza = "Pug", Edad = 2, PesoKg = 8m, FotoUrl = foto };
        contexto.Set<Mascota>().Add(mascota);
        await contexto.SaveChangesAsync();
        return mascota.Id;
    }

    private static VacanJefDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<VacanJefDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MascotasController CrearControlador(VacanJefDbContext contexto, AlmacenFotosMascotas almacen, int duenoId)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, duenoId.ToString()),
                new Claim(ClaimTypes.Role, Roles.Dueno),
            ], "Prueba")),
        };

        return new MascotasController(contexto, almacen, NullLogger<MascotasController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new TempDataSinPersistencia()),
        };
    }

    private sealed class TempDataSinPersistencia : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
