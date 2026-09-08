using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace be_digitalin.Application.Validation;

/// <summary>
/// Validasi file gambar (JPG/JPEG/PNG/WebP/SVG) SEBELUM upload ke object
/// storage. Ekstensi dan content-type dari klien tidak dipercaya — signature
/// byte file diperiksa langsung. Tanpa library tambahan.
///
/// SVG adalah XML, bukan biner: deteksinya pakai prefix teks + sanitasi
/// konten (script/event-handler/foreignObject/URI berbahaya ditolak) karena
/// file di-serve publik. Sanitasi ini lapisan KEDUA — lapisan pertama adalah
/// CSP <c>sandbox</c> di domain storage (Transform Rule Cloudflare) yang
/// membuat script mustahil jalan walau payload lolos. Blocklist tetap
/// blocklist (bukan sandbox parser penuh), makanya header CSP wajib dipasang.
///
/// Dipindah dari Infrastructure/Storage (Fase 4): logic murni BCL + dipakai
/// use-case, jadi tinggal di Application. Stream milik pemanggil — TIDAK
/// di-dispose di sini; posisi di-reset ke 0 agar pembaca berikutnya (R2)
/// membaca dari awal.
/// </summary>
public static class ImageFileValidator
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".jpg", ".jpeg", ".png", ".webp", ".svg" };

    /// <summary>
    /// Penanda berbahaya di dalam SVG: tag script/iframe/embed/object/
    /// foreignObject, URI javascript: dan data:, serta atribut event-handler
    /// on*= (onload/onerror/dst). Case-insensitive. Diperiksa terhadap teks
    /// MENTAH dan hasil decode entity HTML (java&#115;cript: → javascript:).
    /// </summary>
    private static readonly Regex ForbiddenSvgPattern = new(
        @"<script|<iframe|<embed|<object|<foreignObject|javascript:|data:|\son[a-zA-Z]+\s*=",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static async Task EnsureValidImageAsync(ImageFileData file, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException(
                "Format file harus berupa gambar JPG, JPEG, PNG, WebP, atau SVG.");

        if (string.Equals(extension, ".svg", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureValidSvgAsync(file, cancellationToken);
            return;
        }

        // Magic bytes: ekstensi benar belum cukup — isi file wajib gambar dikenal.
        var header = new byte[12];
        var read = 0;
        while (read < header.Length)
        {
            var n = await file.Content.ReadAsync(header.AsMemory(read, header.Length - read), cancellationToken);
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
                "Isi file bukan gambar JPG, JPEG, PNG, WebP, atau SVG yang valid.");

        if (file.Content.CanSeek)
            file.Content.Position = 0;
    }

    // SVG: harus mulai dengan <?xml / <!DOCTYPE svg / <svg (setelah BOM &
    // whitespace), dan bebas penanda berbahaya. Ukuran sudah di-cap 2 MB di
    // controller — membaca seluruh isi untuk scan aman.
    private static async Task EnsureValidSvgAsync(ImageFileData file, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            file.Content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        var content = await reader.ReadToEndAsync(cancellationToken);

        if (file.Content.CanSeek)
            file.Content.Position = 0;

        var trimmed = content.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        var startsAsSvg =
            trimmed.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("<!DOCTYPE svg", StringComparison.OrdinalIgnoreCase);

        if (!startsAsSvg)
            throw new InvalidOperationException(
                "Isi file bukan SVG yang valid.");

        // Scan dua lapis: teks mentah + hasil decode entity. Bypass klasik
        // blocklist = menyandikan karakter (java&#115;cript:, &#x6F;nload=) —
        // browser men-decode entity saat parse, jadi keduanya wajib diperiksa.
        if (ForbiddenSvgPattern.IsMatch(content) ||
            ForbiddenSvgPattern.IsMatch(WebUtility.HtmlDecode(content)))
            throw new InvalidOperationException(
                "SVG ditolak: mengandung script, event handler, atau elemen yang tidak diizinkan.");
    }
}
