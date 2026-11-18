using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Models.DatosPersonales;
using VacanJef.Web.Seguridad;

namespace VacanJef.Tests.Web;

// TT-004: protocolo de protección de datos personales (Ley 1581 de 2012)
public class ProteccionDatosTests
{
    private static VacanJefDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<VacanJefDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static List<ValidationResult> Validar(object modelo)
    {
        var errores = new List<ValidationResult>();
        Validator.TryValidateObject(modelo, new ValidationContext(modelo), errores, validateAllProperties: true);
        return errores;
    }

    public static TheoryData<string> Acciones => new()
    {
        PoliticaDatos.AccionAutorizacion,
        PoliticaDatos.AccionActualizacion,
        PoliticaDatos.AccionSolicitudSupresion,
    };

    [Fact]
    public void Registro_GuardaUsuarioVersionYFecha()
    {
        var antes = DateTime.UtcNow;

        var registro = PoliticaDatos.Registrar(7, PoliticaDatos.AccionAutorizacion);

        Assert.Equal(7, registro.UsuarioId);
        Assert.Equal(PoliticaDatos.AccionAutorizacion, registro.Accion);
        Assert.Equal($"PoliticaDatos v{PoliticaDatos.Version}", registro.Recurso);
        Assert.Equal(PoliticaDatos.ResultadoRegistrada, registro.Resultado);
        Assert.InRange(registro.CreadoEn, antes, DateTime.UtcNow);
    }

    [Theory]
    [MemberData(nameof(Acciones))]
    public void Registro_CabeEnLasColumnasDeAuditoria(string accion)
    {
        // Longitudes de AuditoriaConfiguration: Accion 60, Recurso 60, Resultado 20
        var registro = PoliticaDatos.Registrar(1, accion);

        Assert.InRange(registro.Accion.Length, 1, 60);
        Assert.InRange(registro.Recurso.Length, 1, 60);
        Assert.InRange(registro.Resultado.Length, 1, 20);
    }

    [Theory]
    [MemberData(nameof(Acciones))]
    public void CadaAccion_TieneDescripcionEnEspanol(string accion)
    {
        Assert.NotEqual(accion, PoliticaDatos.Describir(accion));
    }

    [Fact]
    public async Task Autorizacion_QuedaComoPruebaEnAuditoria()
    {
        using var db = CrearContexto();
        db.Auditorias.Add(PoliticaDatos.Registrar(3, PoliticaDatos.AccionAutorizacion));
        await db.SaveChangesAsync();

        var prueba = await db.Auditorias.SingleAsync(a => a.UsuarioId == 3);

        Assert.Equal(PoliticaDatos.AccionAutorizacion, prueba.Accion);
        Assert.StartsWith(PoliticaDatos.Recurso, prueba.Recurso);
    }

    [Fact]
    public void PlazosDeLaLey_SonLosDeLosArticulos14y15()
    {
        Assert.Equal(10, PoliticaDatos.DiasHabilesConsulta);
        Assert.Equal(15, PoliticaDatos.DiasHabilesReclamo);
    }

    [Fact]
    public void ActualizarDatos_Validos_NoTienenErrores()
    {
        var datos = new ActualizarDatosViewModel { Nombre = "Ana Pérez", Telefono = "+57 300 123 4567" };

        Assert.Empty(Validar(datos));
    }

    [Fact]
    public void ActualizarDatos_SinNombre_TieneError()
    {
        var datos = new ActualizarDatosViewModel { Nombre = "", Telefono = "3001234567" };

        Assert.Contains(Validar(datos), e => e.MemberNames.Contains(nameof(datos.Nombre)));
    }

    [Fact]
    public void ActualizarDatos_TelefonoConLetras_TieneError()
    {
        var datos = new ActualizarDatosViewModel { Nombre = "Ana Pérez", Telefono = "trescientos" };

        Assert.Contains(Validar(datos), e => e.MemberNames.Contains(nameof(datos.Telefono)));
    }

    [Fact]
    public void MisDatos_NoPermiteCambiarElCorreoDesdeElFormulario()
    {
        // El correo es el usuario de acceso: el formulario de actualización no lo recibe
        Assert.Null(typeof(ActualizarDatosViewModel).GetProperty("Correo"));
    }
}
