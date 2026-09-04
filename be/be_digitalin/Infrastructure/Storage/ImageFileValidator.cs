using Microsoft.AspNetCore.Http;

namespace be_digitalin.Infrastructure.Storage;

/// <summary>
/// Validasi file gambar (JPG/JPEG/PNG/WebP) SEBELUM upload ke object storage.
/// Ekstensi dan content-type dari klien tidak dipercaya — signature byte file
/// diperiksa langsung. Tanpa library tambahan.
/// </summary>
public static class ImageFileValidator
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".jpg", ".jpeg", ".png", ".webp" };

    public static async Task EnsureValidImageAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException(
                "Format file harus berupa gambar JPG, JPEG, PNG, atau WebP.");

        // Magic bytes: ekstensi benar belum cukup — isi file wajib gambar dikenal.
        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = 0;
        while (read < header.Length)
        {
            var n = await stream.ReadAsync(header.AsMemory(read, header.Length - read), cancellationToken);
            if (n == 0) break;
            read += n;
        }

        var isJpeg = read >= 3 &&
                     header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var isPng = read >= 8 &&
                    header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
        var isWebP = read >= 12 &&
                     header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                     header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;

        if (!isJpeg && !isPng && !isWebP)
            throw new InvalidOperationException(
                "Isi file bukan gambar JPG, JPEG, PNG, atau WebP yang valid.");
    }
}
