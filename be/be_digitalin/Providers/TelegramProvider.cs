using System.Text;
using System.Text.Json;
using be_digitalin.Application.Ports;

namespace be_digitalin.Providers;

public class TelegramSettings
{
    public string BotToken { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
}

public class TelegramProvider : ITelegramProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _botToken;
    private readonly string _chatId;
    private readonly ILogger<TelegramProvider> _logger;

    public TelegramProvider(HttpClient httpClient, IConfiguration config, ILogger<TelegramProvider> logger)
    {
        _httpClient = httpClient;
        _botToken = config["TelegramSettings:BotToken"] ?? "";
        _chatId = config["TelegramSettings:ChatId"] ?? "";
        _logger = logger;
    }

    public async Task SendNotificationAsync(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_botToken) || string.IsNullOrEmpty(_chatId))
            return; // Skip kalau config kosong

        try
        {
            var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";
            var payload = new
            {
                chat_id = _chatId,
                text = message,
                parse_mode = "HTML"
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            await _httpClient.PostAsync(url, content, cancellationToken);
        }
        catch (Exception ex)
        {
            // buat log kalau gagal kirim pesan di tele
            _logger.LogWarning(ex, "[TELEGRAM] Gagal mengirim pesan notifikasi.");
        }
    }
}