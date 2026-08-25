using be_digitalin.DTOs;

namespace be_digitalin.Services.Games;

public interface ISearchService
{
    Task<IEnumerable<GameSearchResult>> SearchGamesAsync(string? keyword, int limit);
}
