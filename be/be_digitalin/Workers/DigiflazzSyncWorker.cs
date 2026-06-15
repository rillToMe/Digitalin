using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using be_digitalin.Config;
using be_digitalin.Data;
using be_digitalin.Models;
using be_digitalin.Services;
using be_digitalin.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace be_digitalin.Workers;

public class DigiflazzSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DigiflazzSyncWorker> _logger;

    // Kita pakai IServiceScopeFactory karena DbContext itu "Scoped", sedangkan Worker ini "Singleton" (hidup terus)
    public DigiflazzSyncWorker(IServiceScopeFactory scopeFactory, ILogger<DigiflazzSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🤖 Robot Auto-Sync Digiflazz mulai bertugas...");

        // Atur interval waktu di sini (Misal: Sinkronisasi setiap 6 Jam)
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        // Loop abadi selama server hidup
        do
        {
            try
            {
                await SyncKatalogAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Robot gagal melakukan sinkronisasi otomatis.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SyncKatalogAsync()
    {
        _logger.LogInformation("🔄 [AUTO-SYNC] Menarik data terbaru dari Digiflazz...");

        using var scope = _scopeFactory.CreateScope();
        
        // Panggil IProductService lewat scope provider
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();

        // Cukup panggil fungsi master dalam 1 baris!
        var (added, updated) = await productService.SyncCatalogFromDigiflazzAsync();

        _logger.LogInformation($"✅ [AUTO-SYNC] Selesai! {added} produk baru ditambah, {updated} produk diupdate harganya.");
    }
}