using Microsoft.AspNetCore.Mvc;
using be_digitalin.Services.Games;

namespace be_digitalin.Endpoints;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        var searchGroup = app.MapGroup("/search")
            .WithTags("Search");

        // GET /search/games?q=keyword&limit=10
        searchGroup.MapGet("/games", async (
            [FromQuery] string? q,
            [FromQuery] int limit = 10,
            [FromServices] ISearchService searchService = null!) =>
        {
            var results = await searchService.SearchGamesAsync(q, limit);
            return Results.Ok(results);
        });
    }
}
