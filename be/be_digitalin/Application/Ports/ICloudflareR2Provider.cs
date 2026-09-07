using be_digitalin.Application.Validation;

namespace be_digitalin.Application.Ports;

/// <summary>
/// Port object storage (Cloudflare R2). Implemented by
/// <c>be_digitalin.Providers.CloudflareR2Provider</c>.
/// Kontrak file memakai <see cref="ImageFileData"/> (Fase 4) — bukan IFormFile,
/// supaya Application tidak bergantung pada tipe ASP.NET.
/// </summary>
public interface ICloudflareR2Provider
{
    Task<string> UploadFileAsync(
        ImageFileData file,
        string folder,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteFileAsync(
        string fileUrl,
        CancellationToken cancellationToken = default);
}