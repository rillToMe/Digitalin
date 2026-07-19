using System.Text.Json;

namespace be_digitalin.Sandbox.Digiflazz;

public class DigiflazzSandboxService
{
    private readonly DigiflazzSandboxClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public DigiflazzSandboxService(
        DigiflazzSandboxClient client)
    {
        _client = client;
    }

    public async Task<JsonDocument> DumpPriceListAsync(
        CancellationToken cancellationToken = default)
    {
        var json = await _client.GetPriceListAsync(cancellationToken);

        await SaveJsonAsync(
            CreateFileName("01_PriceList_Response"),
            json,
            cancellationToken);

        return json;
    }

    // ======================================================
    // PRIVATE
    // ======================================================

    private static string CreateFileName(string name)
    {
        return $"{DateTime.Now:yyyyMMdd_HHmmss}_{name}.json";
    }

    private static async Task SaveJsonAsync(
        string fileName,
        JsonDocument document,
        CancellationToken cancellationToken)
    {
        var outputFolder = Path.Combine(
            AppContext.BaseDirectory,
            "Sandbox",
            "Digiflazz",
            "Output");

        Directory.CreateDirectory(outputFolder);

        var fullPath = Path.Combine(
            outputFolder,
            fileName);

        await File.WriteAllTextAsync(
            fullPath,
            JsonSerializer.Serialize(
                document.RootElement,
                JsonOptions),
            cancellationToken);
    }
}