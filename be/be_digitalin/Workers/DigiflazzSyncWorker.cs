using be_digitalin.Services.Games;

namespace be_digitalin.Workers;

public class DigiflazzSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DigiflazzSyncWorker> _logger;
    
    public DigiflazzSyncWorker(IServiceScopeFactory scopeFactory, ILogger<DigiflazzSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Bot Auto-Sync Digiflazz mulai jalan...");

        // Atur interval waktu
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        // Loop abadi selama server hidup
        do
        {
            try
            {
                await SyncKatalogAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bot gagal melakukan sync otomatis.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SyncKatalogAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[AUTO-SYNC] Menarik data terbaru dari Digiflazz...");

        using var scope = _scopeFactory.CreateScope();
        
        // Panggil IProductService lewat scope provider
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();

        var (added, updated, deactivated) =
            await productService.SyncCatalogFromDigiflazzAsync(stoppingToken);

        _logger.LogInformation(
            "[AUTO-SYNC] Selesai! {Added} produk baru, {Updated} produk diupdate, {Deactivated} produk dinonaktifkan.",
            added,
            updated,
            deactivated
        );
    }
}
