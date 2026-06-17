using System.Text.Json;
using be_digitalin.Constants;
using be_digitalin.Data;
using be_digitalin.Providers;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Workers;

public class OrderSweepWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderSweepWorker> _logger;

    public OrderSweepWorker(IServiceScopeFactory scopeFactory, ILogger<OrderSweepWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Bot Patroli (Order Sweep) mulai berpatroli...");

        // Jalankan bot setiap 2 menit
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));

        do
        {
            try
            {
                await SweepStuckOrdersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bot Patroli mengalami gangguan sistem.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepStuckOrdersAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DigitalinDbContext>();
        var digiflazz = scope.ServiceProvider.GetRequiredService<DigiflazzProvider>();
        var telegram = scope.ServiceProvider.GetRequiredService<TelegramProvider>();

        // Cari pesanan yang statusnya PROCESSING dan sudah lebih dari 3 menit
        var timeThreshold = DateTime.UtcNow.AddMinutes(-3);
        var stuckOrders = await db.TopupOrders
            .Include(o => o.GameProduct)
            .Where(o => o.Status == AppConstants.OrderStatus.Processing && o.OrderDate <= timeThreshold)
            .ToListAsync();

        if (!stuckOrders.Any()) return; // matikan waktu tidak ada yang nyangkut

        _logger.LogInformation("[SWEEP] Menemukan {Count} pesanan nyangkut. Memulai interogasi ke Digiflazz...", stuckOrders.Count);

        foreach (var order in stuckOrders)
        {
            try
            {
                // Minta penjelasan status ke Digiflazz
                var (status, sn, message) = await digiflazz.CheckStatusAsync(order.GameProduct!.BuyerSkuCode, order.PlayerId, order.OrderNo);

                if (status == AppConstants.DigiflazzStatus.Sukses)
                {
                    order.Status = AppConstants.OrderStatus.Completed;
                    order.Sn = sn;
                    _logger.LogInformation("[SWEEP] Order {OrderNo} ternyata SUKSES. Data SN diperbarui.", order.OrderNo);
                }
                else if (status == AppConstants.DigiflazzStatus.Gagal)
                {
                    order.Status = AppConstants.OrderStatus.NeedsAction;
                    _logger.LogWarning("[SWEEP] Order {OrderNo} dipastikan GAGAL dari pusat. Mengunci pesanan...", order.OrderNo);

                    string alertMessage = $@"⚠️ <b>PESANAN TERTUNDA (Hasil Sapuan Sistem)</b> ⚠️

Halo Admin, sistem menemukan pesanan lama yang gagal diproses oleh Digiflazz.

<b>📝 Detail Pesanan:</b>
• <b>Invoice:</b> {order.OrderNo}
• <b>Produk:</b> {order.GameProduct!.Name}
• <b>ID Tujuan:</b> {order.PlayerId}
• <b>Keterangan:</b> {message}

Silakan buka Admin Panel untuk klik Retry atau hubungi pembeli untuk Refund.";

                    await telegram.SendNotificationAsync(alertMessage);
                }
                else
                {
                    // Jika status dari pusat masih "Pending", tapi usia orderan sudah > 15 menit
                    var maxToleransi = DateTime.UtcNow.AddMinutes(-15);
                    
                    if (order.OrderDate <= maxToleransi)
                    {
                        order.Status = AppConstants.OrderStatus.NeedsAction;
                        _logger.LogWarning("[SWEEP] Order {OrderNo} kelamaan Pending di Digiflazz (>15 menit). Force Needs Action!", order.OrderNo);

                        string timeoutMessage = $@"⚠️ <b>PESANAN MENGGANTUNG / TIMEOUT</b> ⚠️

                            Halo Admin, sistem menemukan pesanan yang sudah lebih dari 15 menit dibiarget 'Pending' oleh Digiflazz tanpa kejelasan.

                            <b>📝 Detail Pesanan:</b>
                            • <b>Invoice:</b> {order.OrderNo}
                            • <b>Produk:</b> {order.GameProduct!.Name}
                            • <b>ID Tujuan:</b> {order.PlayerId}

                            Silakan cek manual di dashboard Digiflazz. Jika pesanan tidak masuk, segera proses <b>Refund</b> ke pembeli agar tidak dikomplain.";

                        await telegram.SendNotificationAsync(timeoutMessage);
                    }
                    else
                    {
                        _logger.LogInformation("[SWEEP] Order {OrderNo} masih Pending dari pusat. Sabar, tunggu putaran selanjutnya.", order.OrderNo);
                        // tidak ada yang berubah, langsung skip
                        continue;
                    }
                } 

                try
                {
                    await db.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    _logger.LogWarning(
                        "[SWEEP] Concurrency conflict on Order {OrderNo}. Another process already updated it — skipping.",
                        order.OrderNo);
                    await db.Entry(order).ReloadAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("[SWEEP] Gagal mengecek order {OrderNo}: {Message}", order.OrderNo, ex.Message);
            }
        }
    }
}
