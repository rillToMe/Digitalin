using System.Threading;

using be_digitalin.Application.Validation;
using be_digitalin.Domain.Entities;

namespace be_digitalin.Services.Content;

public interface IBannerService
{
    Task<IEnumerable<Banner>> GetActiveBannersAsync();
    Task<IEnumerable<Banner>> GetAllBannersAsync();
    Task<Banner> CreateBannerAsync(string title, ImageFileData file, CancellationToken cancellationToken = default);
    Task<bool> ToggleBannerStatusAsync(int id);
    Task<bool> DeleteBannerAsync(int id, CancellationToken cancellationToken = default);
}
