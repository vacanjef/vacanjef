using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VacanJef.Core.Entities;
using VacanJef.Core.Enums;
using VacanJef.Infrastructure.Data;

namespace VacanJef.Web.Servicios;

public record RangoFechas(DateOnly Ingreso, DateOnly Salida);

public record Cotizacion(int Dias, decimal TarifaDiaria, decimal CostoTotal);

// Campo: nombre de la propiedad del formulario ("" = error general)
public record ErrorReserva(string Campo, string Mensaje);

public record ResultadoReserva(
    Reserva? Reserva,
    Cotizacion? Cotizacion,
    IReadOnlyList<ErrorReserva> Errores,
    IReadOnlyList<RangoFechas> Alternativas)
{
    public bool Exitoso => Errores.Count == 0;

    public static ResultadoReserva ConErrores(IReadOnlyList<ErrorReserva> errores, IReadOnlyList<RangoFechas>? alternativas = null) =>
        new(null, null, errores, alternativas ?? []);
}

// HU-005 / CU-002: reglas de la reserva de estadía.
// RN-001: solo se reserva para una mascota registrada y activa del dueño de la sesión.
// RN-003: no se supera la capacidad diaria; la última plaza no puede reservarse dos veces.
// RN-007: el costo lo calcula el sistema según el servicio y los días.
public class ServicioReservas(VacanJefDbContext contexto, OpcionesReservas opciones)
{
    public const int AlternativasMaximas = 3;
    public const int VentanaAlternativasDias = 60;

    // Estados que ocupan un cupo (las canceladas y finalizadas lo liberan).
    // En las consultas se escriben como comparaciones para que EF las traduzca a SQL sin sorpresas.
    public static bool OcupaCupo(EstadoReserva estado) =>
        estado is EstadoReserva.Confirmada or EstadoReserva.Activa or EstadoReserva.Pagada;

    // Colombia no tiene horario de verano (UTC-5 todo el año) y el servidor de Azure está en UTC
    public static DateOnly HoyEnColombia() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));

    // Se cobra por noche; si la mascota entra y sale el mismo día se cobra un día
    public static int Dias(DateOnly ingreso, DateOnly salida) => Math.Max(1, salida.DayNumber - ingreso.DayNumber);

    // Días en que la reserva ocupa un cupo: desde el ingreso hasta la noche anterior a la salida
    public static IEnumerable<DateOnly> DiasOcupados(DateOnly ingreso, DateOnly salida) =>
        Enumerable.Range(0, Dias(ingreso, salida)).Select(ingreso.AddDays);

    private static bool SeSolapan(DateOnly ingresoA, DateOnly salidaA, DateOnly ingresoB, DateOnly salidaB) =>
        ingresoA.AddDays(Dias(ingresoA, salidaA)) > ingresoB && ingresoB.AddDays(Dias(ingresoB, salidaB)) > ingresoA;

    public Cotizacion Cotizar(TipoServicio servicio, DateOnly ingreso, DateOnly salida)
    {
        var dias = Dias(ingreso, salida);
        var tarifa = opciones.Tarifa(servicio);
        return new Cotizacion(dias, tarifa, dias * tarifa);
    }

    public IReadOnlyList<ErrorReserva> ValidarFechas(DateOnly ingreso, DateOnly salida, DateOnly hoy)
    {
        var errores = new List<ErrorReserva>();

        if (ingreso < hoy)
            errores.Add(new("FechaIngreso", "La fecha de ingreso no puede ser anterior a hoy."));
        else if (ingreso > hoy.AddDays(opciones.AnticipacionMaximaDias))
            errores.Add(new("FechaIngreso", $"Se puede reservar con máximo {opciones.AnticipacionMaximaDias} días de anticipación."));

        // CA-HU-005-03
        if (salida < ingreso)
            errores.Add(new("FechaSalida", "La fecha de salida no puede ser anterior a la de ingreso."));
        else if (Dias(ingreso, salida) > opciones.DuracionMaximaDias)
            errores.Add(new("FechaSalida", $"La estadía puede durar máximo {opciones.DuracionMaximaDias} días."));

        return errores;
    }

    // Valida todo y calcula el costo, sin guardar (botón "Ver costo y disponibilidad")
    public Task<ResultadoReserva> RevisarAsync(int duenoId, int mascotaId, DateOnly ingreso, DateOnly salida,
        TipoServicio servicio, DateOnly hoy) =>
        RevisarInternoAsync(duenoId, mascotaId, ingreso, salida, servicio, hoy);

    // Vuelve a validar y guarda en una sola transacción. Con SQL Server se usa aislamiento
    // Serializable: dos dueños que piden la última plaza al mismo tiempo no pueden quedar
    // ambos confirmados. Va dentro de CreateExecutionStrategy por los reintentos de Azure SQL.
    public async Task<ResultadoReserva> CrearAsync(int duenoId, int mascotaId, DateOnly ingreso, DateOnly salida,
        TipoServicio servicio, DateOnly hoy)
    {
        var estrategia = contexto.Database.CreateExecutionStrategy();

        return await estrategia.ExecuteAsync(async () =>
        {
            contexto.ChangeTracker.Clear();

            await using IDbContextTransaction? transaccion = contexto.Database.IsRelational()
                ? await contexto.Database.BeginTransactionAsync(IsolationLevel.Serializable)
                : null;

            var revision = await RevisarInternoAsync(duenoId, mascotaId, ingreso, salida, servicio, hoy);
            if (!revision.Exitoso)
                return revision;

            var reserva = new Reserva
            {
                MascotaId = mascotaId,
                DuenoId = duenoId,
                FechaIngreso = ingreso,
                FechaSalida = salida,
                Servicio = servicio,
                CostoTotal = revision.Cotizacion!.CostoTotal,
                Estado = EstadoReserva.Confirmada,
            };

            contexto.Reservas.Add(reserva);
            await contexto.SaveChangesAsync();

            if (transaccion is not null)
                await transaccion.CommitAsync();

            return revision with { Reserva = reserva };
        });
    }

    private async Task<ResultadoReserva> RevisarInternoAsync(int duenoId, int mascotaId, DateOnly ingreso, DateOnly salida,
        TipoServicio servicio, DateOnly hoy)
    {
        var errores = new List<ErrorReserva>(ValidarFechas(ingreso, salida, hoy));

        // RN-001
        var mascotaValida = await contexto.Mascotas
            .AsNoTracking()
            .AnyAsync(m => m.Id == mascotaId && m.DuenoId == duenoId && m.Activo);

        if (!mascotaValida)
            errores.Add(new("MascotaId", "Seleccione una de sus mascotas registradas."));

        if (errores.Count > 0)
            return ResultadoReserva.ConErrores(errores);

        var fin = ingreso.AddDays(Dias(ingreso, salida));

        // La misma mascota no puede tener dos estadías al mismo tiempo
        var reservasDeLaMascota = await contexto.Reservas
            .AsNoTracking()
            .Where(r => r.MascotaId == mascotaId
                && (r.Estado == EstadoReserva.Confirmada || r.Estado == EstadoReserva.Activa || r.Estado == EstadoReserva.Pagada)
                && r.FechaIngreso < fin
                && r.FechaSalida >= ingreso)
            .Select(r => new { r.FechaIngreso, r.FechaSalida })
            .ToListAsync();

        if (reservasDeLaMascota.Any(r => SeSolapan(r.FechaIngreso, r.FechaSalida, ingreso, salida)))
            return ResultadoReserva.ConErrores(
                [new("", "Esta mascota ya tiene una reserva que se cruza con esas fechas.")]);

        // RN-003: ocupación desde el ingreso hasta el final de la ventana de alternativas
        var ocupacion = await OcupacionAsync(ingreso, fin.AddDays(VentanaAlternativasDias));

        var diasLlenos = DiasOcupados(ingreso, salida)
            .Where(dia => ocupacion.GetValueOrDefault(dia) >= opciones.CapacidadDiaria)
            .ToList();

        if (diasLlenos.Count > 0)
        {
            // CA-HU-005-02: no se reserva y se muestran fechas alternativas
            var lista = string.Join(", ", diasLlenos.Take(5).Select(d => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
            return ResultadoReserva.ConErrores(
                [new("", $"No hay cupo disponible en las fechas elegidas (sin cupo: {lista}). Pruebe con una de las fechas sugeridas.")],
                BuscarAlternativas(ingreso, salida, ocupacion));
        }

        return new ResultadoReserva(null, Cotizar(servicio, ingreso, salida), [], []);
    }

    // Mismo número de noches, empezando cada vez un día más tarde
    private List<RangoFechas> BuscarAlternativas(DateOnly ingreso, DateOnly salida, Dictionary<DateOnly, int> ocupacion)
    {
        var duracion = salida.DayNumber - ingreso.DayNumber;
        var alternativas = new List<RangoFechas>();

        for (var desplazamiento = 1; desplazamiento <= VentanaAlternativasDias && alternativas.Count < AlternativasMaximas; desplazamiento++)
        {
            var nuevoIngreso = ingreso.AddDays(desplazamiento);
            var nuevaSalida = nuevoIngreso.AddDays(duracion);

            if (DiasOcupados(nuevoIngreso, nuevaSalida).All(dia => ocupacion.GetValueOrDefault(dia) < opciones.CapacidadDiaria))
                alternativas.Add(new RangoFechas(nuevoIngreso, nuevaSalida));
        }

        return alternativas;
    }

    // Cuántas mascotas ocupan cupo cada día del rango [desde, hasta)
    private async Task<Dictionary<DateOnly, int>> OcupacionAsync(DateOnly desde, DateOnly hasta)
    {
        var reservas = await contexto.Reservas
            .AsNoTracking()
            .Where(r => (r.Estado == EstadoReserva.Confirmada || r.Estado == EstadoReserva.Activa || r.Estado == EstadoReserva.Pagada)
                && r.FechaIngreso < hasta
                && r.FechaSalida >= desde)
            .Select(r => new { r.FechaIngreso, r.FechaSalida })
            .ToListAsync();

        var ocupacion = new Dictionary<DateOnly, int>();

        foreach (var reserva in reservas)
        {
            foreach (var dia in DiasOcupados(reserva.FechaIngreso, reserva.FechaSalida))
            {
                if (dia >= desde && dia < hasta)
                    ocupacion[dia] = ocupacion.GetValueOrDefault(dia) + 1;
            }
        }

        return ocupacion;
    }
}
