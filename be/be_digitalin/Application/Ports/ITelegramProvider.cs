namespace be_digitalin.Application.Ports;

/// <summary>
/// Port notifikasi Telegram (auxiliary — tidak boleh menahan request).
/// Implemented by <c>be_digitalin.Providers.TelegramProvider</c>.
/// </summary>
public interface ITelegramProvider
{
    Task SendNotificationAsync(string message, CancellationToken cancellationToken = default);
}