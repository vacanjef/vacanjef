namespace VacanJef.Web.Servicios;

// HU-004 / RN-008: fotos de mascotas en JPG o PNG de máximo 5 MB.
// Se guardan fuera de wwwroot con un nombre aleatorio; solo se entregan
// a través de MascotasController, que revisa que la mascota sea del dueño.
public class AlmacenFotosMascotas
{
    public const long TamanoMaximoBytes = 5 * 1024 * 1024;

    private static readonly byte[] FirmaJpg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] FirmaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly string carpeta;

    public AlmacenFotosMascotas(string carpeta)
    {
        this.carpeta = Path.GetFullPath(carpeta);
        Directory.CreateDirectory(this.carpeta);
    }

    // Se revisan los primeros bytes del archivo, no la extensión:
    // un ejecutable renombrado a .jpg no pasa.
    public static string? DetectarFormato(ReadOnlySpan<byte> cabecera)
    {
        if (cabecera.StartsWith(FirmaJpg)) return ".jpg";
        if (cabecera.StartsWith(FirmaPng)) return ".png";
        return null;
    }

    public static async Task<(string? Extension, string? Error)> ValidarAsync(IFormFile archivo)
    {
        if (archivo.Length == 0)
            return (null, "El archivo está vacío.");

        if (archivo.Length > TamanoMaximoBytes)
            return (null, "La foto supera el máximo de 5 MB.");

        var cabecera = new byte[FirmaPng.Length];
        await using var flujo = archivo.OpenReadStream();
        var leidos = await flujo.ReadAtLeastAsync(cabecera, cabecera.Length, throwOnEndOfStream: false);

        var extension = DetectarFormato(cabecera.AsSpan(0, leidos));
        return extension is null
            ? (null, "Solo se permiten fotos JPG o PNG.")
            : (extension, null);
    }

    public async Task<string> GuardarAsync(IFormFile archivo, string extension)
    {
        var nombre = $"{Guid.NewGuid():N}{extension}";
        await using var destino = File.Create(Path.Combine(carpeta, nombre));
        await archivo.CopyToAsync(destino);
        return nombre;
    }

    // Devuelve null si el nombre intenta salir de la carpeta o si el archivo no existe
    public string? RutaCompleta(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre) || nombre != Path.GetFileName(nombre) || nombre.Contains(".."))
            return null;

        var ruta = Path.Combine(carpeta, nombre);
        return File.Exists(ruta) ? ruta : null;
    }

    public void Eliminar(string? nombre)
    {
        if (RutaCompleta(nombre) is string ruta)
            File.Delete(ruta);
    }

    public static string TipoContenido(string nombre) =>
        Path.GetExtension(nombre).Equals(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
}
