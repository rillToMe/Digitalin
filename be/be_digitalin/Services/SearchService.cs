using be_digitalin.Data;
using be_digitalin.DTOs;
using be_digitalin.Models;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services;

public class SearchService : ISearchService
{
    private readonly DigitalinDbContext _db;

    public SearchService(DigitalinDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<GameSearchResult>> SearchGamesAsync(string? keyword, int limit)
    {
        // Business rule: empty keyword returns empty array
        if (string.IsNullOrWhiteSpace(keyword))
            return Enumerable.Empty<GameSearchResult>();

        // Clamp limit to a sensible range
        if (limit < 1) limit = 1;
        if (limit > 50) limit = 50;

        var lowerKeyword = keyword.Trim().ToLower();

        var results = await _db.GameMetadata
            .AsNoTracking()
            .Where(m => m.Brand.ToLower().Contains(lowerKeyword))
            .OrderBy(m => m.Brand.ToLower().StartsWith(lowerKeyword) ? 0 : 1)
            .ThenBy(m => m.Brand)
            .Take(limit)
            .Select(m => new
            {
                m.Id,
                m.Brand,
                m.ImageUrl,
                m.Categories
            })
            .ToListAsync();

        // Kategori disimpan CSV di satu kolom → dipecah di memori (maks 50 baris).
        return results
            .Select(m => new GameSearchResult(
                m.Id,
                m.Brand,
                m.Brand.ToLower().Replace(" ", "-"),
                m.ImageUrl,
                GameMetadata.ParseCategories(m.Categories)
            ))
            .ToList();
    }
}
