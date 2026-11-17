using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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

namespace VacanJef.Tests.Web;

// HU-003: registro de mascota con información de salud (RN-002)
public class RegistroMascotaTests
{
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
            new() { Tipo = TipoRegistroSalud.Alergia, Descripcion = "Pollo" },
        ],
    };

    private static List<ValidationResult> Validar(object modelo)
    {
        var errores = new List<ValidationResult>();
        Validator.TryValidateObject(modelo, new ValidationContext(modelo), errores, validateAllProperties: true);
        return errores;
    }

    private static bool TieneError(List<ValidationResult> errores, string campo) =>
        errores.Any(e => e.MemberNames.Contains(campo));

    [Fact]
    public void DatosValidos_NoTienenErrores()
    {
        Assert.Empty(Validar(ModeloValido()));
    }

    [Fact]
    public void SinVacunas_TieneError()
    {
        var modelo = ModeloValido();
        modelo.RegistrosSalud = [new() { Tipo = TipoRegistroSalud.Alergia, Descripcion = "Pollo" }];

        Assert.True(TieneError(Validar(modelo), nameof(modelo.RegistrosSalud)));
    }

    [Fact]
    public void VacunaSinFecha_TieneError()
    {
        var modelo = ModeloValido();
        modelo.RegistrosSalud[0].Fecha = null;

        Assert.True(TieneError(Validar(modelo), "RegistrosSalud[0].Fecha"));
    }

    [Fact]
    public void FechaFutura_TieneError()
    {
        var modelo = ModeloValido();
        modelo.RegistrosSalud[0].Fecha = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        Assert.True(TieneError(Validar(modelo), "RegistrosSalud[0].Fecha"));
    }

    [Fact]
    public void EdadFueraDeRango_TieneError()
    {
        var modelo = ModeloValido();
        modelo.Edad = 31;

        Assert.True(TieneError(Validar(modelo), nameof(modelo.Edad)));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("150")]
    [InlineData("doce")]
    public void PesoInvalido_TieneError(string peso)
    {
        var modelo = ModeloValido();
        modelo.Peso = peso;

        Assert.True(TieneError(Validar(modelo), nameof(modelo.Peso)));
    }

    [Fact]
    public void PesoConComa_SeAcepta()
    {
        var modelo = ModeloValido();
        modelo.Peso = "12,5";

        Assert.Empty(Validar(modelo));
        Assert.Equal(12.5m, modelo.CrearMascota(1).PesoKg);
    }

    [Fact]
    public void CrearMascota_AsignaDueno_EIgnoraFilasVacias()
    {
        var modelo = ModeloValido();
        modelo.RegistrosSalud.Add(new RegistroSaludItemViewModel());

        var mascota = modelo.CrearMascota(7);

        Assert.Equal(7, mascota.DuenoId);
        Assert.Equal(2, mascota.RegistrosSalud.Count);
    }

    [Fact]
    public void FormularioNoPermiteElegirDueno()
    {
        // El dueño sale de la sesión, nunca del formulario
        Assert.Null(typeof(RegistroMascotaViewModel).GetProperty("DuenoId"));
    }

    [Fact]
    public void ControladorSoloParaDuenos()
    {
        var atributo = typeof(MascotasController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(atributo);
        Assert.Equal(Roles.Dueno, atributo.Roles);
    }

    [Fact]
    public async Task Registrar_GuardaMascotaDelDuenoDeLaSesion()
    {
        using var contexto = CrearContexto();
        var controlador = CrearControlador(contexto, duenoId: 1);

        var resultado = await controlador.Registrar(ModeloValido());

        Assert.IsType<RedirectToActionResult>(resultado);
        var guardada = Assert.Single(contexto.Set<Mascota>().Include(m => m.RegistrosSalud));
        Assert.Equal(1, guardada.DuenoId);
        Assert.Equal(2, guardada.RegistrosSalud.Count);
    }

    [Fact]
    public async Task Registrar_ConErrores_NoGuarda()
    {
        using var contexto = CrearContexto();
        var controlador = CrearControlador(contexto, duenoId: 1);
        controlador.ModelState.AddModelError("Nombre", "El nombre es obligatorio.");

        var resultado = await controlador.Registrar(ModeloValido());

        Assert.IsType<ViewResult>(resultado);
        Assert.Empty(contexto.Set<Mascota>());
    }

    [Fact]
    public async Task Index_SoloMuestraMascotasActivasDelDueno()
    {
        using var contexto = CrearContexto();
        var toby = new Mascota { DuenoId = 1, Nombre = "Toby", Raza = "Criollo", Edad = 3, PesoKg = 12.5m };
        toby.RegistrosSalud.Add(new RegistroSalud { Tipo = TipoRegistroSalud.Vacuna, Descripcion = "Rabia" });
        contexto.Set<Mascota>().AddRange(
            toby,
            new Mascota { DuenoId = 1, Nombre = "Luna", Raza = "Pug", Edad = 2, PesoKg = 8m, Activo = false },
            new Mascota { DuenoId = 2, Nombre = "Max", Raza = "Beagle", Edad = 5, PesoKg = 14m });
        await contexto.SaveChangesAsync();

        var vista = Assert.IsType<ViewResult>(await CrearControlador(contexto, duenoId: 1).Index());

        var lista = Assert.IsAssignableFrom<IEnumerable<MascotaResumenViewModel>>(vista.Model);
        var unica = Assert.Single(lista);
        Assert.Equal("Toby", unica.Nombre);
        Assert.Equal(1, unica.Vacunas);
    }

    private static VacanJefDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<VacanJefDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MascotasController CrearControlador(VacanJefDbContext contexto, int duenoId)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, duenoId.ToString()),
                new Claim(ClaimTypes.Role, Roles.Dueno),
            ], "Prueba")),
        };

        return new MascotasController(contexto, NullLogger<MascotasController>.Instance)
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
