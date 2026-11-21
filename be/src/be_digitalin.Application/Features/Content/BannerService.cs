using be_digitalin.Constants;
using be_digitalin.Application.Ports;
using be_digitalin.Domain.Entities;

using be_digitalin.Application.Validation;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services.Content;

public class BannerService : IBannerService
{
    private readonly IApplicationDbContext _db;
    private readonly ICloudflareR2Provider _r2Provider; 

    public BannerService(IApplicationDbContext db, ICloudflareR2Provider r2Provider)
    {
        _db = db;
        _r2Provider = r2Provider;
    }

    public async Task<IEnumerable<Banner>> GetActiveBannersAsync()
    {
        return await _db.Banners.Where(b => b.IsActive).OrderByDescending(b => b.CreatedAt).ToListAsync();
    }

    public async Task<IEnumerable<Banner>> GetAllBannersAsync()
    {
        return await _db.Banners
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }
    
    public async Task<Banner> CreateBannerAsync(string title, ImageFileData file, CancellationToken cancellationToken = default)
    {
        await ImageFileValidator.EnsureValidImageAsync(file, cancellationToken);

        var imageUrl = await _r2Provider.UploadFileAsync(file, AppConstants.StorageFolders.Banners, cancellationToken);

        var banner = new Banner
        {
            Title = title, ImageUrl = imageUrl, IsActive = true, CreatedAt = DateTime.UtcNow
        };

        _db.Banners.Add(banner);
        await _db.SaveChangesAsync(cancellationToken);

        return banner;
    }

    public async Task<bool> ToggleBannerStatusAsync(int id)
    {
        var banner = await _db.Banners.FindAsync(id);
        if (banner == null) return false;

        banner.IsActive = !banner.IsActive;
        await _db.SaveChangesAsync();
        
        return banner.IsActive;
    }
    
    public async Task<bool> DeleteBannerAsync(int id, CancellationToken cancellationToken = default)
    {
        var banner = await _db.Banners.FindAsync(id);
        if (banner == null) return false;

        if (!string.IsNullOrEmpty(banner.ImageUrl))
        {
            await _r2Provider.DeleteFileAsync(banner.ImageUrl, cancellationToken);
        }

        _db.Banners.Remove(banner);
        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }
}



