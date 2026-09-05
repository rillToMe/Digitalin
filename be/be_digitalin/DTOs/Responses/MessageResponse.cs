namespace be_digitalin.DTOs;

/// <summary>
/// Bentuk respons pesan tunggal — pengganti object anonymous `new { message }`.
/// Diserialisasi sebagai `{ "message": "..." }`, identik dengan kontrak lama.
/// </summary>
public record MessageResponse(string Message);