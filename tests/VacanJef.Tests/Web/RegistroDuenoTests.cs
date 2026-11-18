using System.ComponentModel.DataAnnotations;
using VacanJef.Web.Models.Cuenta;
using VacanJef.Web.Seguridad;

namespace VacanJef.Tests.Web;

// HU-001 / RF-001: validaciones del registro de dueño
public class RegistroDuenoTests
{
    private static RegistroDuenoViewModel ModeloValido() => new()
    {
        Nombre = "Ana Pérez",
        Correo = "ana.perez@ejemplo.com",
        Telefono = "+57 300 123 4567",
        Contrasena = "Vacanjef#2026",
        ConfirmarContrasena = "Vacanjef#2026",
        AceptaTratamientoDatos = true,
    };

    private static List<ValidationResult> Validar(object modelo)
    {
        var errores = new List<ValidationResult>();
        Validator.TryValidateObject(modelo, new ValidationContext(modelo), errores, validateAllProperties: true);
        return errores;
    }

    [Fact]
    public void DatosValidos_NoTienenErrores()
    {
        Assert.Empty(Validar(ModeloValido()));
    }

    [Fact]
    public void CorreoInvalido_TieneError()
    {
        var modelo = ModeloValido();
        modelo.Correo = "esto-no-es-un-correo";

        Assert.Contains(Validar(modelo), e => e.MemberNames.Contains(nameof(modelo.Correo)));
    }

    [Fact]
    public void ContrasenasDistintas_TieneError()
    {
        var modelo = ModeloValido();
        modelo.ConfirmarContrasena = "OtraClave#2026";

        Assert.Contains(Validar(modelo), e => e.MemberNames.Contains(nameof(modelo.ConfirmarContrasena)));
    }

    [Fact]
    public void ContrasenaCorta_TieneError()
    {
        var modelo = ModeloValido();
        modelo.Contrasena = "Ab#1";
        modelo.ConfirmarContrasena = "Ab#1";

        Assert.Contains(Validar(modelo), e => e.MemberNames.Contains(nameof(modelo.Contrasena)));
    }

    [Fact]
    public void TelefonoConLetras_TieneError()
    {
        var modelo = ModeloValido();
        modelo.Telefono = "trescientos";

        Assert.Contains(Validar(modelo), e => e.MemberNames.Contains(nameof(modelo.Telefono)));
    }

    [Fact]
    public void FormularioNoPermiteElegirRol()
    {
        // Criterio "permiso denegado" de RF-001: el rol no se puede enviar desde el formulario
        Assert.Null(typeof(RegistroDuenoViewModel).GetProperty("Rol"));
    }

    [Fact]
    public void CorreoDuplicado_MensajeEnEspanol()
    {
        var error = new ErroresIdentityEnEspanol().DuplicateEmail("ana.perez@ejemplo.com");

        Assert.Equal("Ya existe una cuenta registrada con ese correo.", error.Description);
    }

    [Fact]
    public void ContrasenaCorta_MensajeIndicaLongitud()
    {
        var error = new ErroresIdentityEnEspanol().PasswordTooShort(8);

        Assert.Contains("8", error.Description);
    }
}
