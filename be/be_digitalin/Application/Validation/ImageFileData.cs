namespace be_digitalin.Application.Validation;

/// <summary>
/// Kontrak file upload di Application (Fase 4 Clean Architecture).
/// Menggantikan <c>Microsoft.AspNetCore.Http.IFormFile</c> di service/port
/// (P4). Mapping dari IFormFile dilakukan di Controller (API layer) —
/// Application tidak lagi bergantung pada tipe ASP.NET.
/// </summary>
public record ImageFileData(string FileName, string ContentType, Stream Content);