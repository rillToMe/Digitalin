namespace be_digitalin.DTOs;

/// <summary>
/// Respons upload gambar — pengganti `new { message, url }`
/// (upload icon produk & cover game).
/// </summary>
public record UploadImageResponse(string Message, string Url);