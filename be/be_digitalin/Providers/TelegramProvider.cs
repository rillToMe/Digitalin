using System.Text;
using System.Text.Json;

namespace be_digitalin.Providers;

public class TelegramSettings
{
    public string BotToken { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
}

public class TelegramProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _botToken;
    private readonly string _chatId;

    public TelegramProvider(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _botToken = config["TelegramSettings:BotToken"] ?? "";
        _chatId = config["TelegramSettings:ChatId"] ?? "";
    }

    public async Task SendNotificationAsync(string message)
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
            await _httpClient.PostAsync(url, content);
        }
        catch (Exception ex)
        {
            // buat log kalau gagal kirim pesan di tele
            Console.WriteLine($"[TELEGRAM ERROR] Gagal mengirim pesan: {ex.Message}");
        }
    }
}