#:package Microsoft.Data.SqlClient@6.1.1
#:property PublishAot=false

// TT-011: respaldo y prueba de restauración de la base LOCAL de VacanJef y de las fotos de mascotas.
// Se ejecuta desde la raíz del repositorio (ver docs/respaldo-y-restauracion.md):
//   dotnet run herramientas/respaldo/respaldo.cs -- respaldar
//   dotnet run herramientas/respaldo/respaldo.cs -- probar [archivo.bak]
// Usa la misma cadena de la aplicación (variable VACANJEF_DB). Nunca toca Azure SQL.
// Los respaldos tienen datos personales (Ley 1581): se guardan fuera del repositorio y no se comparten.
// La salida solo muestra nombres de tablas y cantidades de filas, nunca datos.

using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Data.SqlClient;

const string SufijoPrueba = "_PruebaRestauracion";

var comando = args.Length > 0 ? args[0].ToLowerInvariant() : string.Empty;
if (comando is not ("respaldar" or "probar"))
{
    Console.WriteLine("""
        Uso, desde la raíz del repositorio:
          dotnet run herramientas/respaldo/respaldo.cs -- respaldar
          dotnet run herramientas/respaldo/respaldo.cs -- probar [archivo.bak]

        Variables opcionales:
          VACANJEF_RESPALDOS            carpeta de los respaldos (por defecto ~/respaldos-vacanjef)
          VACANJEF_RESPALDOS_CONSERVAR  cuántos respaldos conservar (por defecto 7)
          VACANJEF_FOTOS                carpeta de fotos (por defecto src/VacanJef.Web/App_Data/fotos-mascotas)
          VACANJEF_CONSERVAR_PRUEBA=1   no borrar la base de prueba al terminar
        """);
    return 1;
}

if (!File.Exists("VacanJef.slnx"))
{
    return Fallar("Ejecute el comando desde la raíz del repositorio (donde está VacanJef.slnx).");
}

var cadena = Environment.GetEnvironmentVariable("VACANJEF_DB");
if (string.IsNullOrWhiteSpace(cadena))
{
    return Fallar("Falta la variable VACANJEF_DB. Ejecute: source ~/.bashrc");
}

SqlConnectionStringBuilder datosConexion;
try
{
    datosConexion = new SqlConnectionStringBuilder(cadena);
}
catch (ArgumentException)
{
    return Fallar("La cadena de VACANJEF_DB no es válida.");
}

if (datosConexion.DataSource.Contains("database.windows.net", StringComparison.OrdinalIgnoreCase))
{
    return Fallar("VACANJEF_DB apunta a Azure SQL. Esta herramienta es solo para bases locales; "
        + "Azure SQL se respalda solo (ver docs/respaldo-y-restauracion.md).");
}

var baseDatos = datosConexion.InitialCatalog;
if (string.IsNullOrWhiteSpace(baseDatos))
{
    return Fallar("La cadena de VACANJEF_DB no indica la base (Database=...).");
}

datosConexion.InitialCatalog = "master";
var cadenaMaestra = datosConexion.ConnectionString;

var carpeta = Environment.GetEnvironmentVariable("VACANJEF_RESPALDOS")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "respaldos-vacanjef");
carpeta = Path.GetFullPath(carpeta);

try
{
    if (await ServidorEnLinuxAsync(cadenaMaestra))
    {
        return Fallar("El servidor SQL corre en Linux (por ejemplo, el contenedor de Docker): "
            + "no puede escribir en las carpetas de este PC. Use los comandos de la sección Docker "
            + "de docs/respaldo-y-restauracion.md.");
    }

    return comando == "respaldar"
        ? await RespaldarAsync()
        : await ProbarAsync(args.Length > 1 ? args[1] : null);
}
catch (SqlException ex) when (ex.Message.Contains("error 5", StringComparison.OrdinalIgnoreCase)
                              || ex.Message.Contains("Access is denied", StringComparison.OrdinalIgnoreCase)
                              || ex.Message.Contains("Acceso denegado", StringComparison.OrdinalIgnoreCase))
{
    return Fallar($"SQL Server no tiene permiso para usar la carpeta {carpeta}. "
        + "Con un servicio SQL Server (no LocalDB), defina VACANJEF_RESPALDOS con una carpeta "
        + "en la que el servicio pueda escribir (ver docs/respaldo-y-restauracion.md).");
}
catch (SqlException ex)
{
    return Fallar($"SQL Server respondió con el error {ex.Number}: {ex.Message}");
}

async Task<int> RespaldarAsync()
{
    Directory.CreateDirectory(carpeta);
    var sello = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    var archivoBase = Path.Combine(carpeta, $"{baseDatos}_{sello}.bak");
    var reloj = Stopwatch.StartNew();

    Console.WriteLine($"Respaldando la base {baseDatos} en {archivoBase} ...");
    await EjecutarAsync(cadenaMaestra,
        "BACKUP DATABASE @base TO DISK = @ruta WITH COPY_ONLY, INIT, CHECKSUM, NAME = @nombre",
        ("@base", baseDatos), ("@ruta", archivoBase), ("@nombre", $"VacanJef {sello}"));

    Console.WriteLine("Verificando el archivo (RESTORE VERIFYONLY) ...");
    await EjecutarAsync(cadenaMaestra, "RESTORE VERIFYONLY FROM DISK = @ruta WITH CHECKSUM",
        ("@ruta", archivoBase));

    var carpetaFotos = CarpetaFotos();
    if (Directory.Exists(carpetaFotos) && Directory.EnumerateFiles(carpetaFotos, "*", SearchOption.AllDirectories).Any())
    {
        var archivoFotos = Path.Combine(carpeta, $"{baseDatos}_{sello}_fotos.zip");
        ZipFile.CreateFromDirectory(carpetaFotos, archivoFotos, CompressionLevel.Optimal, includeBaseDirectory: false);
        using var zip = ZipFile.OpenRead(archivoFotos);
        Console.WriteLine($"Fotos: {zip.Entries.Count} archivo(s) en {archivoFotos}");
    }
    else
    {
        Console.WriteLine($"Fotos: no hay fotos en {carpetaFotos} (no se crea el .zip).");
    }

    var borrados = AplicarRetencion();
    reloj.Stop();

    Console.WriteLine();
    Console.WriteLine($"OK: respaldo creado y verificado en {reloj.Elapsed.TotalSeconds:0.0} s.");
    Console.WriteLine($"     Base:  {archivoBase} ({new FileInfo(archivoBase).Length / 1024.0:0} KB)");
    if (borrados > 0)
    {
        Console.WriteLine($"     Se borraron {borrados} respaldo(s) antiguo(s) por la retención.");
    }

    Console.WriteLine("Siguiente paso: dotnet run herramientas/respaldo/respaldo.cs -- probar");
    return 0;
}

async Task<int> ProbarAsync(string? archivoIndicado)
{
    var archivoBase = archivoIndicado is null ? UltimoRespaldo() : Path.GetFullPath(archivoIndicado);
    if (archivoBase is null || !File.Exists(archivoBase))
    {
        return Fallar($"No hay respaldos en {carpeta}. Primero ejecute: "
            + "dotnet run herramientas/respaldo/respaldo.cs -- respaldar");
    }

    var basePrueba = baseDatos + SufijoPrueba;
    var carpetaPrueba = Path.Combine(carpeta, "prueba-restauracion");
    Directory.CreateDirectory(carpetaPrueba);
    var reloj = Stopwatch.StartNew();

    Console.WriteLine($"Restaurando {Path.GetFileName(archivoBase)} en la base de prueba {basePrueba} ...");
    Console.WriteLine($"(La base {baseDatos} no se modifica.)");
    await BorrarBaseAsync(basePrueba);

    var archivos = await ArchivosDelRespaldoAsync(archivoBase);
    var parametros = new List<(string, object)> { ("@base", basePrueba), ("@ruta", archivoBase) };
    var movimientos = new List<string>();
    for (var i = 0; i < archivos.Count; i++)
    {
        var (logico, tipo) = archivos[i];
        var extension = tipo == "L" ? ".ldf" : i == 0 ? ".mdf" : ".ndf";
        parametros.Add(($"@logico{i}", logico));
        parametros.Add(($"@fisico{i}", Path.Combine(carpetaPrueba, $"{basePrueba}_{i}{extension}")));
        movimientos.Add($"MOVE @logico{i} TO @fisico{i}");
    }

    await EjecutarAsync(cadenaMaestra,
        $"RESTORE DATABASE @base FROM DISK = @ruta WITH {string.Join(", ", movimientos)}, RECOVERY, CHECKSUM",
        [.. parametros]);

    Console.WriteLine("Revisando la integridad (DBCC CHECKDB) ...");
    await EjecutarAsync(cadenaMaestra, $"DBCC CHECKDB ({Corchetes(basePrueba)}) WITH NO_INFOMSGS");

    var restaurada = await ContarFilasAsync(basePrueba);
    var actual = await ContarFilasAsync(baseDatos);
    var migracion = await UltimaMigracionAsync(basePrueba);
    reloj.Stop();

    Console.WriteLine();
    Console.WriteLine($"{"Tabla",-32} {"Actual",10} {"Restaurada",11}");
    var diferencias = 0;
    foreach (var tabla in actual.Keys.Union(restaurada.Keys).Order())
    {
        var filasActual = actual.GetValueOrDefault(tabla, -1);
        var filasRestaurada = restaurada.GetValueOrDefault(tabla, -1);
        var marca = filasActual == filasRestaurada ? string.Empty : "  <- distinto";
        diferencias += marca.Length > 0 ? 1 : 0;
        Console.WriteLine($"{tabla,-32} {Cantidad(filasActual),10} {Cantidad(filasRestaurada),11}{marca}");
    }

    var resultadoFotos = RevisarFotos(archivoBase);

    if (VariableActiva("VACANJEF_CONSERVAR_PRUEBA"))
    {
        Console.WriteLine($"\nLa base {basePrueba} se conserva (VACANJEF_CONSERVAR_PRUEBA=1). Bórrela al terminar.");
    }
    else
    {
        await BorrarBaseAsync(basePrueba);
        if (!Directory.EnumerateFileSystemEntries(carpetaPrueba).Any())
        {
            Directory.Delete(carpetaPrueba);
        }
    }

    if (restaurada.Count == 0 || migracion is null)
    {
        return Fallar("La base restaurada no tiene tablas o no tiene historial de migraciones.");
    }

    Console.WriteLine();
    Console.WriteLine($"OK: restauración probada en {reloj.Elapsed.TotalSeconds:0.0} s (tiempo de recuperación medido).");
    Console.WriteLine($"     Respaldo:        {Path.GetFileName(archivoBase)}");
    Console.WriteLine($"     Integridad:      DBCC CHECKDB sin errores");
    Console.WriteLine($"     Tablas:          {restaurada.Count}; última migración: {migracion}");
    Console.WriteLine($"     Fotos:           {resultadoFotos}");
    if (diferencias > 0)
    {
        Console.WriteLine($"     Aviso: {diferencias} tabla(s) cambiaron desde el respaldo. Es normal si la base "
            + "se usó después de respaldar.");
    }

    Console.WriteLine("Anote el resultado en la tabla \"Registro de pruebas\" de docs/respaldo-y-restauracion.md.");
    return 0;
}

string CarpetaFotos() => Path.GetFullPath(
    Environment.GetEnvironmentVariable("VACANJEF_FOTOS")
    ?? Environment.GetEnvironmentVariable("Fotos__Ruta")
    ?? Path.Combine("src", "VacanJef.Web", "App_Data", "fotos-mascotas"));

string? UltimoRespaldo() => Directory.Exists(carpeta)
    ? Directory.GetFiles(carpeta, $"{baseDatos}_*.bak").Order().LastOrDefault()
    : null;

int AplicarRetencion()
{
    var conservar = int.TryParse(Environment.GetEnvironmentVariable("VACANJEF_RESPALDOS_CONSERVAR"), out var n) && n > 0
        ? n
        : 7;
    var antiguos = Directory.GetFiles(carpeta, $"{baseDatos}_*.bak").Order().Reverse().Skip(conservar).ToList();
    foreach (var bak in antiguos)
    {
        File.Delete(bak);
        var fotos = Path.ChangeExtension(bak, null) + "_fotos.zip";
        if (File.Exists(fotos))
        {
            File.Delete(fotos);
        }
    }

    return antiguos.Count;
}

string RevisarFotos(string archivoBase)
{
    var archivoFotos = Path.ChangeExtension(archivoBase, null) + "_fotos.zip";
    if (!File.Exists(archivoFotos))
    {
        return "este respaldo no tiene .zip de fotos";
    }

    try
    {
        using var zip = ZipFile.OpenRead(archivoFotos);
        var legibles = 0;
        foreach (var entrada in zip.Entries.Where(e => e.Length > 0))
        {
            using var flujo = entrada.Open();
            flujo.CopyTo(Stream.Null);
            legibles++;
        }

        return $"{legibles} archivo(s) legibles en {Path.GetFileName(archivoFotos)}";
    }
    catch (InvalidDataException)
    {
        return $"ERROR: {Path.GetFileName(archivoFotos)} está dañado";
    }
}

async Task<List<(string Logico, string Tipo)>> ArchivosDelRespaldoAsync(string ruta)
{
    var archivos = new List<(string, string)>();
    await using var conexion = new SqlConnection(cadenaMaestra);
    await conexion.OpenAsync();
    await using var consulta = new SqlCommand("RESTORE FILELISTONLY FROM DISK = @ruta", conexion) { CommandTimeout = 0 };
    consulta.Parameters.AddWithValue("@ruta", ruta);
    await using var lector = await consulta.ExecuteReaderAsync();
    while (await lector.ReadAsync())
    {
        archivos.Add(((string)lector["LogicalName"], (string)lector["Type"]));
    }

    // Primero los archivos de datos, para que el primero quede como .mdf
    return [.. archivos.OrderBy(a => a.Item2 == "L" ? 1 : 0)];
}

async Task<Dictionary<string, long>> ContarFilasAsync(string nombreBase)
{
    var filas = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    var datos = new SqlConnectionStringBuilder(cadenaMaestra) { InitialCatalog = nombreBase };
    await using var conexion = new SqlConnection(datos.ConnectionString);
    await conexion.OpenAsync();
    await using var consulta = new SqlCommand("""
        SELECT t.name, SUM(p.rows)
        FROM sys.tables t
        JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
        WHERE t.is_ms_shipped = 0
        GROUP BY t.name
        """, conexion);
    await using var lector = await consulta.ExecuteReaderAsync();
    while (await lector.ReadAsync())
    {
        filas[lector.GetString(0)] = lector.GetInt64(1);
    }

    return filas;
}

async Task<string?> UltimaMigracionAsync(string nombreBase)
{
    var datos = new SqlConnectionStringBuilder(cadenaMaestra) { InitialCatalog = nombreBase };
    await using var conexion = new SqlConnection(datos.ConnectionString);
    await conexion.OpenAsync();
    await using var consulta = new SqlCommand(
        "IF OBJECT_ID(N'__EFMigrationsHistory') IS NOT NULL "
        + "SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC", conexion);
    return await consulta.ExecuteScalarAsync() as string;
}

async Task BorrarBaseAsync(string nombreBase)
{
    if (!nombreBase.EndsWith(SufijoPrueba, StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Solo se borra la base de prueba.");
    }

    SqlConnection.ClearAllPools();
    var nombre = Corchetes(nombreBase);
    await EjecutarAsync(cadenaMaestra, $"""
        IF DB_ID(@base) IS NOT NULL
        BEGIN
            ALTER DATABASE {nombre} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            DROP DATABASE {nombre};
        END
        """, ("@base", nombreBase));
}

static async Task<bool> ServidorEnLinuxAsync(string cadenaMaestra)
{
    await using var conexion = new SqlConnection(cadenaMaestra);
    await conexion.OpenAsync();
    await using var consulta = new SqlCommand("SELECT host_platform FROM sys.dm_os_host_info", conexion);
    try
    {
        return await consulta.ExecuteScalarAsync() is string plataforma
            && plataforma.Equals("Linux", StringComparison.OrdinalIgnoreCase);
    }
    catch (SqlException)
    {
        return false; // Versiones antiguas sin esa vista: se asume Windows
    }
}

static async Task EjecutarAsync(string cadenaConexion, string sql, params (string Nombre, object Valor)[] parametros)
{
    await using var conexion = new SqlConnection(cadenaConexion);
    await conexion.OpenAsync();
    await using var orden = new SqlCommand(sql, conexion) { CommandTimeout = 0 };
    foreach (var (nombre, valor) in parametros)
    {
        orden.Parameters.AddWithValue(nombre, valor);
    }

    await orden.ExecuteNonQueryAsync();
}

static string Corchetes(string nombre) => "[" + nombre.Replace("]", "]]") + "]";

static string Cantidad(long filas) => filas < 0 ? "(no existe)" : filas.ToString("N0");

static bool VariableActiva(string nombre) => Environment.GetEnvironmentVariable(nombre) is "1" or "true";

static int Fallar(string mensaje)
{
    Console.Error.WriteLine($"ERROR: {mensaje}");
    return 1;
}
