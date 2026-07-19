using Microsoft.AspNetCore.Mvc;

namespace be_digitalin.Sandbox.Digiflazz;

public static class DigiflazzSandboxEndpoints
{
    public static void MapDigiflazzSandbox(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sandbox/digiflazz")
            .WithTags("Sandbox - Digiflazz");

        group.MapGet("/", () =>
        {
            return Results.Ok(new
            {
                message = "Digiflazz Sandbox is running."
            });
        });

        group.MapGet("/price-list",
            async (
                [FromServices] DigiflazzSandboxService service,
                CancellationToken cancellationToken) =>
            {
                var json = await service.DumpPriceListAsync(cancellationToken);

                return Results.Text(
                    json.RootElement.GetRawText(),
                    "application/json");
            });
    }
}