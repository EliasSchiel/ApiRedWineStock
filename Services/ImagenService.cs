namespace RedWineStock.Services;

/// <summary>
/// Guarda y borra imágenes de productos en wwwroot/uploads/productos.
/// Valida el CONTENIDO real del archivo (firma / "magic bytes"), no solo la extensión,
/// para que nunca se guarde algo que después no se pueda abrir.
/// </summary>
public class ImagenService
{
    public const long TamanioMaximo = 5 * 1024 * 1024; // 5 MB
    private const string CarpetaRelativa = "uploads/productos";

    private readonly IWebHostEnvironment _env;

    public ImagenService(IWebHostEnvironment env) => _env = env;

    private string CarpetaFisica
    {
        get
        {
            // En algunos hostings WebRootPath puede venir null si no se publicó wwwroot
            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            return Path.Combine(webRoot, "uploads", "productos");
        }
    }

    public async Task<(bool Ok, string? NombreArchivo, string? Error)> GuardarAsync(IFormFile? archivo)
    {
        if (archivo is null || archivo.Length == 0)
            return (false, null, "No se envió ningún archivo. En Postman: Body > form-data, clave 'archivo' de tipo File.");

        if (archivo.Length > TamanioMaximo)
            return (false, null, "La imagen supera el máximo de 5 MB.");

        // Se lee el archivo completo a memoria y se valida antes de tocar el disco
        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await archivo.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        if (bytes.Length != archivo.Length)
            return (false, null, "El archivo llegó incompleto. Volvé a intentarlo.");

        var extension = DetectarExtension(bytes);
        if (extension is null)
            return (false, null, "El archivo no es una imagen JPG, PNG o WEBP válida.");

        Directory.CreateDirectory(CarpetaFisica);
        var nombreArchivo = $"{Guid.NewGuid():N}{extension}";

        // WriteAllBytesAsync escribe y cierra el archivo completo de una vez
        await File.WriteAllBytesAsync(Path.Combine(CarpetaFisica, nombreArchivo), bytes);

        return (true, nombreArchivo, null);
    }

    public void Eliminar(string nombreArchivo)
    {
        // Path.GetFileName evita rutas tipo "../../appsettings.json"
        var ruta = Path.Combine(CarpetaFisica, Path.GetFileName(nombreArchivo));
        if (File.Exists(ruta)) File.Delete(ruta);
    }

    /// <summary>URL absoluta, usando el host desde el que se hizo el pedido.</summary>
    public static string ArmarUrl(HttpRequest request, string nombreArchivo) =>
        $"{request.Scheme}://{request.Host}{request.PathBase}/{CarpetaRelativa}/{nombreArchivo}";

    private static string? DetectarExtension(byte[] b)
    {
        if (b.Length < 12) return null;

        // JPEG: FF D8 FF
        if (b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ".jpg";

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 &&
            b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) return ".png";

        // WEBP: "RIFF" .... "WEBP"
        if (b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' &&
            b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return ".webp";

        return null;
    }
}
