using be_digitalin.Services.Orders;

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
                using var scope = _scopeFactory.CreateScope();
                var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
                await orderService.SweepStuckOrdersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bot Patroli mengalami gangguan sistem.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}




