using Microsoft.EntityFrameworkCore;
using VacanJef.Core.Entities;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Seguridad;
using VacanJef.Web.Servicios;

namespace VacanJef.Tests.Web;

// HU-017: panel de auditoría y atención de solicitudes de supresión (TT-004, Ley 1581)
public class AuditoriaTests
{
    private static VacanJefDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<VacanJefDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<Usuario> AgregarUsuarioAsync(VacanJefDbContext db, string nombre, string correo)
    {
        var usuario = new Usuario { UserName = correo, Email = correo, Nombre = nombre };
        db.Users.Add(usuario);
        await db.SaveChangesAsync();
        return usuario;
    }

    private static async Task RegistrarAsync(VacanJefDbContext db, int? usuarioId, string accion, string recurso, DateTime fecha)
    {
        db.Auditorias.Add(new Auditoria
        {
            UsuarioId = usuarioId,
            Accion = accion,
            Recurso = recurso,
            Resultado = "permitido",
            CreadoEn = fecha,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Listar_MuestraLoMasRecientePrimeroConSuAutor()
    {
        using var db = CrearContexto();
        var ana = await AgregarUsuarioAsync(db, "Ana", "ana@ejemplo.com");
        await RegistrarAsync(db, ana.Id, PoliticaDatos.AccionAutorizacion, "PoliticaDatos v1.0", DateTime.UtcNow.AddHours(-2));
        await RegistrarAsync(db, ana.Id, PoliticaDatos.AccionActualizacion, "PoliticaDatos v1.0", DateTime.UtcNow);

        var registros = await new ConsultaAuditoria(db).ListarAsync(new FiltroAuditoria());

        Assert.Equal(2, registros.Count);
        Assert.Equal(PoliticaDatos.AccionActualizacion, registros[0].Accion);
        Assert.Equal("Ana (ana@ejemplo.com)", registros[0].Autor);
    }

    [Fact]
    public async Task Listar_FiltraPorAccionYPorTexto()
    {
        using var db = CrearContexto();
        var ana = await AgregarUsuarioAsync(db, "Ana", "ana@ejemplo.com");
        var luis = await AgregarUsuarioAsync(db, "Luis", "luis@ejemplo.com");
        await RegistrarAsync(db, ana.Id, PoliticaDatos.AccionAutorizacion, "PoliticaDatos v1.0", DateTime.UtcNow);
        await RegistrarAsync(db, luis.Id, PoliticaDatos.AccionAutorizacion, "PoliticaDatos v1.0", DateTime.UtcNow);
        await RegistrarAsync(db, luis.Id, GestionUsuarios.AccionDesactivar, "Usuario #99", DateTime.UtcNow);
        var consulta = new ConsultaAuditoria(db);

        var autorizaciones = await consulta.ListarAsync(new FiltroAuditoria(Accion: PoliticaDatos.AccionAutorizacion));
        var deLuis = await consulta.ListarAsync(new FiltroAuditoria(Texto: "luis@"));
        var porRecurso = await consulta.ListarAsync(new FiltroAuditoria(Texto: "Usuario #99"));

        Assert.Equal(2, autorizaciones.Count);
        Assert.Equal(2, deLuis.Count);
        Assert.Single(porRecurso);
    }

    [Fact]
    public async Task Listar_RegistroSinUsuario_MuestraGuion()
    {
        using var db = CrearContexto();
        await RegistrarAsync(db, null, "AccesoCamara", "Camara #1", DateTime.UtcNow);

        var registro = Assert.Single(await new ConsultaAuditoria(db).ListarAsync(new FiltroAuditoria()));

        Assert.Equal("—", registro.Autor);
        Assert.Equal("AccesoCamara", registro.Descripcion); // acción desconocida: se muestra tal cual
    }

    [Fact]
    public async Task SolicitudSinAtender_QuedaPendienteConFechaLimite()
    {
        using var db = CrearContexto();
        var ana = await AgregarUsuarioAsync(db, "Ana", "ana@ejemplo.com");
        var fecha = new DateTime(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc); // lunes
        await RegistrarAsync(db, ana.Id, PoliticaDatos.AccionSolicitudSupresion, "PoliticaDatos v1.0", fecha);

        var pendiente = Assert.Single(await new ConsultaAuditoria(db).SolicitudesPendientesAsync());

        Assert.Equal(ana.Id, pendiente.UsuarioId);
        Assert.Equal("Ana", pendiente.Nombre);
        Assert.Equal(new DateTime(2026, 12, 7, 15, 0, 0, DateTimeKind.Utc), pendiente.FechaLimite); // 15 días hábiles
    }

    [Fact]
    public async Task MarcarAtendida_LaSacaDePendientesYQuedaEnAuditoria()
    {
        using var db = CrearContexto();
        var admin = await AgregarUsuarioAsync(db, "Admin", "admin@ejemplo.com");
        var ana = await AgregarUsuarioAsync(db, "Ana", "ana@ejemplo.com");
        await RegistrarAsync(db, ana.Id, PoliticaDatos.AccionSolicitudSupresion, "PoliticaDatos v1.0", DateTime.UtcNow.AddMinutes(-5));
        var consulta = new ConsultaAuditoria(db);

        var resultado = await consulta.MarcarAtendidaAsync(ana.Id, admin.Id);

        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Empty(await consulta.SolicitudesPendientesAsync());
        Assert.True(await db.Auditorias.AnyAsync(a =>
            a.Accion == ConsultaAuditoria.AccionSupresionAtendida && a.UsuarioId == admin.Id && a.Recurso == $"Usuario #{ana.Id}"));
    }

    [Fact]
    public async Task NuevaSolicitudDespuesDeAtendida_VuelveAPendientes()
    {
        using var db = CrearContexto();
        var admin = await AgregarUsuarioAsync(db, "Admin", "admin@ejemplo.com");
        var ana = await AgregarUsuarioAsync(db, "Ana", "ana@ejemplo.com");
        await RegistrarAsync(db, ana.Id, PoliticaDatos.AccionSolicitudSupresion, "PoliticaDatos v1.0", DateTime.UtcNow.AddDays(-3));
        await RegistrarAsync(db, admin.Id, ConsultaAuditoria.AccionSupresionAtendida, $"Usuario #{ana.Id}", DateTime.UtcNow.AddDays(-2));
        await RegistrarAsync(db, ana.Id, PoliticaDatos.AccionSolicitudSupresion, "PoliticaDatos v1.0", DateTime.UtcNow);

        Assert.Single(await new ConsultaAuditoria(db).SolicitudesPendientesAsync());
    }

    [Fact]
    public async Task MarcarAtendida_SinSolicitudPendiente_SeRechaza()
    {
        using var db = CrearContexto();
        var admin = await AgregarUsuarioAsync(db, "Admin", "admin@ejemplo.com");
        var ana = await AgregarUsuarioAsync(db, "Ana", "ana@ejemplo.com");

        var resultado = await new ConsultaAuditoria(db).MarcarAtendidaAsync(ana.Id, admin.Id);

        Assert.False(resultado.Exito);
        Assert.Empty(await db.Auditorias.ToListAsync());
    }

    [Theory]
    [InlineData(2026, 11, 20, 1, 2026, 11, 23)] // viernes + 1 hábil = lunes
    [InlineData(2026, 11, 21, 1, 2026, 11, 23)] // sábado + 1 hábil = lunes
    [InlineData(2026, 11, 16, 5, 2026, 11, 23)] // lunes + 5 hábiles = lunes siguiente
    public void SumarDiasHabiles_SaltaFinesDeSemana(int a, int m, int d, int dias, int ra, int rm, int rd)
    {
        Assert.Equal(new DateTime(ra, rm, rd), ConsultaAuditoria.SumarDiasHabiles(new DateTime(a, m, d), dias));
    }

    [Fact]
    public void AccionesConocidas_TienenDescripcionEnEspanol()
    {
        Assert.All(ConsultaAuditoria.AccionesConocidas, accion => Assert.NotEqual(accion.Key, accion.Value));
        Assert.Equal("Desactivó una cuenta", ConsultaAuditoria.Describir(GestionUsuarios.AccionDesactivar));
    }
}
