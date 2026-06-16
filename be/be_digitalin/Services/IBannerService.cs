using be_digitalin.Models;

namespace be_digitalin.Services;

public interface IBannerService
{
    Task<IEnumerable<Banner>> GetActiveBannersAsync();
    Task<IEnumerable<Banner>> GetAllBannersAsync();
    Task<Banner> CreateBannerAsync(string title, IFormFile file);
    Task<bool> ToggleBannerStatusAsync(int id);
    Task<bool> DeleteBannerAsync(int id);
}