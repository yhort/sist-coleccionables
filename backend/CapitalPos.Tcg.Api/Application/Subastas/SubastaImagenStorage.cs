using CapitalPos.Tcg.Api.Application;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;

namespace CapitalPos.Tcg.Api.Application.Subastas;

/// <summary>
/// Guarda imágenes de lote FB en wwwroot/uploads/subastas/{empresaId}/.
/// </summary>
public sealed class SubastaImagenStorage(
    IWebHostEnvironment env,
    ITenantProvider tenant)
{
    private const int MaxBytes = 4 * 1024 * 1024;

    public async Task<string?> GuardarAsync(
        string? imagenBase64,
        string? contentType,
        string? nombreArchivo,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imagenBase64))
        {
            return null;
        }

        var (bytes, mime) = Decodificar(imagenBase64, contentType);
        if (bytes.Length == 0)
        {
            return null;
        }

        if (bytes.Length > MaxBytes)
        {
            throw new BusinessRuleException("Cada imagen del lote debe pesar menos de 4 MB.");
        }

        var ext = ExtensionDesdeMime(mime, nombreArchivo);
        var root = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"));
        var carpeta = Path.Combine(root, "uploads", "subastas", tenant.EmpresaId.ToString("N"));
        Directory.CreateDirectory(carpeta);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(carpeta, fileName);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);

        return $"/uploads/subastas/{tenant.EmpresaId:N}/{fileName}";
    }

    private static (byte[] Bytes, string Mime) Decodificar(string raw, string? contentType)
    {
        var valor = raw.Trim();
        var mime = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType.Trim();

        if (valor.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = valor.IndexOf(',');
            if (comma <= 0)
            {
                throw new BusinessRuleException("Imagen en data URL inválida.");
            }

            var header = valor[..comma];
            valor = valor[(comma + 1)..];
            var colon = header.IndexOf(':');
            var semi = header.IndexOf(';');
            if (colon >= 0 && semi > colon)
            {
                mime = header[(colon + 1)..semi];
            }
        }

        try
        {
            return (Convert.FromBase64String(valor), mime);
        }
        catch (FormatException)
        {
            throw new BusinessRuleException("No se pudo decodificar la imagen (base64 inválido).");
        }
    }

    private static string ExtensionDesdeMime(string mime, string? nombreArchivo)
    {
        var fromName = Path.GetExtension(nombreArchivo ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(fromName) && fromName.Length <= 5)
        {
            return fromName.ToLowerInvariant();
        }

        return mime.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".jpg"
        };
    }
}
