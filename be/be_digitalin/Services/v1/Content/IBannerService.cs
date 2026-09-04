using System.Threading;

using be_digitalin.Models;

namespace be_digitalin.Services.Content;

public interface IBannerService
{
    Task<IEnumerable<Banner>> GetActiveBannersAsync();
    Task<IEnumerable<Banner>> GetAllBannersAsync();
    Task<Banner> CreateBannerAsync(string title, IFormFile file, CancellationToken cancellationToken = default);
    Task<bool> ToggleBannerStatusAsync(int id);
    Task<bool> DeleteBannerAsync(int id, CancellationToken cancellationToken = default);
}
