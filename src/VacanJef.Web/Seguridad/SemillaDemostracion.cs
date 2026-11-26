using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VacanJef.Core.Constantes;
using VacanJef.Core.Entities;
using VacanJef.Core.Enums;
using VacanJef.Infrastructure.Data;

namespace VacanJef.Web.Seguridad;

// TT-009: datos ficticios para probar y demostrar el sistema (Ley 1581: nada es de personas reales).
// Solo en Desarrollo y solo con VACANJEF_DEMO=true. La contraseña de las cuentas viene de una variable.
// Si las cuentas de demostración ya existen, no se carga nada de nuevo.
public static class SemillaDemostracion
{
    public const string VariableActivar = "VACANJEF_DEMO";
    public const string VariableContrasena = "VACANJEF_DEMO_CONTRASENA";

    public const string CorreoCuidador = "cuidador.demo@ejemplo.com";

    public static IReadOnlyList<string> Correos => Cuentas.Select(c => c.Correo).ToList();

    // Correo, nombre y rol de cada cuenta. El orden importa: ver UsuariosDemostracion.
    private static readonly (string Correo, string Nombre, string Rol)[] Cuentas =
    [
        (CorreoCuidador, "Sofía Ramírez (demo)", Roles.Cuidador),
        ("carolina.demo@ejemplo.com", "Carolina Gómez (demo)", Roles.Dueno),
        ("andres.demo@ejemplo.com", "Andrés Restrepo (demo)", Roles.Dueno),
        ("valentina.demo@ejemplo.com", "Valentina Ospina (demo)", Roles.Dueno),
    ];

    // Tarifas por noche por defecto de HU-005 (Reservas__Tarifas); aquí solo se usan para datos de ejemplo.
    private static decimal Tarifa(TipoServicio servicio) => servicio switch
    {
        TipoServicio.Premium => 80000m,
        TipoServicio.Spa => 110000m,
        _ => 50000m,
    };

    public static bool DebeCargar(IConfiguration configuracion, IHostEnvironment ambiente) =>
        ambiente.IsDevelopment()
        && string.Equals(configuracion[VariableActivar]?.Trim(), "true", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrEmpty(configuracion[VariableContrasena]);

    public static async Task<bool> CrearAsync(IServiceProvider servicios, IConfiguration configuracion, IHostEnvironment ambiente)
    {
        var logger = servicios.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(SemillaDemostracion));

        if (!DebeCargar(configuracion, ambiente))
        {
            if (!ambiente.IsDevelopment() && !string.IsNullOrEmpty(configuracion[VariableActivar]))
                logger.LogWarning("{Variable} se ignora: los datos de demostración solo se cargan en Desarrollo.", VariableActivar);
            return false;
        }

        using var alcance = servicios.CreateScope();
        var gestor = alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var contexto = alcance.ServiceProvider.GetRequiredService<VacanJefDbContext>();

        if (await gestor.FindByEmailAsync(CorreoCuidador) is not null)
        {
            logger.LogInformation("Los datos de demostración ya estaban cargados.");
            return false;
        }

        var contrasena = configuracion[VariableContrasena]!;
        var ids = new List<int>();

        foreach (var (correo, nombre, rol) in Cuentas)
        {
            var usuario = new Usuario { UserName = correo, Email = correo, Nombre = nombre, EmailConfirmed = true };
            var creacion = await gestor.CreateAsync(usuario, contrasena);
            if (!creacion.Succeeded)
            {
                logger.LogError("No se pudieron crear las cuentas de demostración: {Errores}",
                    string.Join(" ", creacion.Errors.Select(e => e.Description)));
                return false;
            }

            await gestor.AddToRoleAsync(usuario, rol);
            ids.Add(usuario.Id);
        }

        var usuarios = new UsuariosDemostracion(ids[0], ids[1], ids[2], ids[3]);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5)); // Colombia, UTC-5 todo el año
        await CargarDatosAsync(contexto, usuarios, hoy);

        logger.LogInformation("Datos de demostración cargados: {Cuentas} cuentas, mascotas, reservas y observaciones.", Cuentas.Length);
        return true;
    }

    // Mascotas, salud, reservas en todos los estados y observaciones. Las fechas son relativas a hoy
    // para que siempre haya una estadía activa y un ingreso pendiente para hoy.
    public static async Task<bool> CargarDatosAsync(VacanJefDbContext contexto, UsuariosDemostracion u, DateOnly hoy)
    {
        if (await contexto.Mascotas.AnyAsync(m => m.DuenoId == u.Dueno1Id || m.DuenoId == u.Dueno2Id || m.DuenoId == u.Dueno3Id))
            return false;

        var max = NuevaMascota(u.Dueno1Id, "Max", "Labrador", 4, 28.5m, SexoMascota.Macho);
        var lola = NuevaMascota(u.Dueno1Id, "Lola", "Poodle", 2, 6.2m, SexoMascota.Hembra);
        var rocky = NuevaMascota(u.Dueno2Id, "Rocky", "Bulldog francés", 3, 12m, SexoMascota.Macho);
        var nala = NuevaMascota(u.Dueno3Id, "Nala", "Golden retriever", 1, 22m, SexoMascota.Hembra);
        var simon = NuevaMascota(u.Dueno3Id, "Simón", "Criollo", 6, 15.3m, SexoMascota.Macho); // sin estadías (CA-HU-013-03)
        contexto.Mascotas.AddRange(max, lola, rocky, nala, simon);
        await contexto.SaveChangesAsync();

        contexto.RegistrosSalud.AddRange(
            NuevoRegistroSalud(max, TipoRegistroSalud.Vacuna, "Rabia", hoy.AddMonths(-5)),
            NuevoRegistroSalud(max, TipoRegistroSalud.Vacuna, "Parvovirus", hoy.AddMonths(-5)),
            NuevoRegistroSalud(lola, TipoRegistroSalud.Vacuna, "Moquillo", hoy.AddMonths(-2)),
            NuevoRegistroSalud(lola, TipoRegistroSalud.Alergia, "Pollo (en el alimento)", null),
            NuevoRegistroSalud(rocky, TipoRegistroSalud.Vacuna, "Rabia", hoy.AddMonths(-8)),
            NuevoRegistroSalud(rocky, TipoRegistroSalud.Medicamento, "Antipulgas mensual", hoy.AddDays(-10)),
            NuevoRegistroSalud(nala, TipoRegistroSalud.Vacuna, "Rabia", hoy.AddMonths(-1)),
            NuevoRegistroSalud(simon, TipoRegistroSalud.Medicamento, "Suplemento para articulaciones", hoy.AddMonths(-3)));

        var maxPasada = NuevaReserva(max, hoy.AddDays(-20), hoy.AddDays(-17), TipoServicio.Premium, EstadoReserva.Finalizada);
        maxPasada.IngresoReal = EnUtc(hoy.AddDays(-20), 13);
        maxPasada.SalidaReal = EnUtc(hoy.AddDays(-17), 22);
        var maxFutura = NuevaReserva(max, hoy.AddDays(10), hoy.AddDays(12), TipoServicio.Basico, EstadoReserva.Confirmada);
        var lolaActiva = NuevaReserva(lola, hoy.AddDays(-1), hoy.AddDays(2), TipoServicio.Spa, EstadoReserva.Activa);
        lolaActiva.IngresoReal = EnUtc(hoy.AddDays(-1), 13);
        var rockyActiva = NuevaReserva(rocky, hoy, hoy.AddDays(3), TipoServicio.Basico, EstadoReserva.Activa);
        rockyActiva.IngresoReal = EnUtc(hoy, 13);
        var rockyCancelada = NuevaReserva(rocky, hoy.AddDays(5), hoy.AddDays(6), TipoServicio.Basico, EstadoReserva.Cancelada);
        var nalaHoy = NuevaReserva(nala, hoy, hoy.AddDays(2), TipoServicio.Premium, EstadoReserva.Confirmada); // ingreso pendiente (HU-014)
        contexto.Reservas.AddRange(maxPasada, maxFutura, lolaActiva, rockyActiva, rockyCancelada, nalaHoy);
        await contexto.SaveChangesAsync();

        var vomito = NuevaObservacion(maxPasada, u.CuidadorId, TipoObservacion.Salud, "Vomitó una vez después del almuerzo.", EnUtc(hoy.AddDays(-19), 19));
        contexto.Observaciones.AddRange(
            NuevaObservacion(maxPasada, u.CuidadorId, TipoObservacion.Alimentacion, "Comió toda su ración de la mañana.", EnUtc(hoy.AddDays(-19), 14)),
            vomito,
            NuevaObservacion(maxPasada, u.CuidadorId, TipoObservacion.Actividad, "Jugó con la pelota en el patio durante 30 minutos.", EnUtc(hoy.AddDays(-18), 16)),
            NuevaObservacion(lolaActiva, u.CuidadorId, TipoObservacion.Comportamiento, "Llegó un poco nerviosa; se tranquilizó en la tarde.", EnUtc(hoy.AddDays(-1), 18)),
            NuevaObservacion(lolaActiva, u.CuidadorId, TipoObservacion.Alimentacion, "Se le dio su alimento sin pollo, como indica su alergia.", EnUtc(hoy.AddDays(-1), 23)),
            NuevaObservacion(rockyActiva, u.CuidadorId, TipoObservacion.Salud, "Se le aplicó el antipulgas según la indicación del dueño.", EnUtc(hoy, 14)));
        await contexto.SaveChangesAsync();

        // RN-006: la corrección es una observación nueva; la original queda igual
        contexto.Observaciones.Add(NuevaObservacion(maxPasada, u.CuidadorId, TipoObservacion.Salud,
            "Corrección: no vomitó; fue otra mascota del mismo grupo.", EnUtc(hoy.AddDays(-19), 20), vomito.Id));
        await contexto.SaveChangesAsync();

        return true;
    }

    private static Mascota NuevaMascota(int duenoId, string nombre, string raza, byte edad, decimal peso, SexoMascota sexo) => new()
    {
        DuenoId = duenoId, Nombre = nombre, Raza = raza, Edad = edad, PesoKg = peso, Sexo = sexo,
    };

    private static RegistroSalud NuevoRegistroSalud(Mascota mascota, TipoRegistroSalud tipo, string descripcion, DateOnly? fecha) => new()
    {
        MascotaId = mascota.Id, Tipo = tipo, Descripcion = descripcion, Fecha = fecha,
    };

    private static Reserva NuevaReserva(Mascota mascota, DateOnly ingreso, DateOnly salida, TipoServicio servicio, EstadoReserva estado) => new()
    {
        MascotaId = mascota.Id,
        DuenoId = mascota.DuenoId,
        FechaIngreso = ingreso,
        FechaSalida = salida,
        Servicio = servicio,
        CostoTotal = Tarifa(servicio) * (salida.DayNumber - ingreso.DayNumber), // RN-007: por noche
        Estado = estado,
    };

    private static Observacion NuevaObservacion(Reserva reserva, int autorId, TipoObservacion tipo, string detalle, DateTime creadoEn, int? corrigeA = null) => new()
    {
        ReservaId = reserva.Id, AutorId = autorId, Tipo = tipo, Detalle = detalle, CreadoEn = creadoEn, CorrigeAId = corrigeA,
    };

    // Hora UTC de un día; 13 UTC = 8:00 a. m. en Colombia
    private static DateTime EnUtc(DateOnly dia, int horaUtc) =>
        DateTime.SpecifyKind(dia.ToDateTime(new TimeOnly(horaUtc, 0)), DateTimeKind.Utc);
}

public record UsuariosDemostracion(int CuidadorId, int Dueno1Id, int Dueno2Id, int Dueno3Id);
