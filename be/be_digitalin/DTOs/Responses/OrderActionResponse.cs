namespace be_digitalin.DTOs;

/// <summary>
/// Respons aksi order admin (retry/refund) saat sukses —
/// pengganti `new { message, status }`. Hanya dipakai untuk outcome Success.
/// </summary>
public record OrderActionResponse(string Message, string Status);