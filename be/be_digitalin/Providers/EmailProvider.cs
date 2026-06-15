using be_digitalin.Config;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace be_digitalin.Providers;

public class EmailProvider
{
    private readonly EmailSettings _settings;

    public EmailProvider(IOptions<EmailSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendOtpEmailAsync(string targetEmail, string otpCode)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
        message.To.Add(new MailboxAddress("Customer", targetEmail));
        message.Subject = "Kode Verifikasi (OTP) Digitalin";

        // Desain isi email-nya
        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px;'>
                    <h2>Verifikasi Email Kamu</h2>
                    <p>Gunakan kode OTP di bawah ini untuk menyelesaikan pendaftaran di Digitalin.</p>
                    <h1 style='color: #007bff; letter-spacing: 5px;'>{otpCode}</h1>
                    <p>Kode ini hanya berlaku selama 5 menit. Jangan berikan kode ini kepada siapapun.</p>
                </div>"
        };
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            // Hubungkan ke Mailtrap
            await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.Username, _settings.Password);
            
            // Kirim Email
            await client.SendAsync(message);
        }
        finally
        {
            await client.DisconnectAsync(true);
        }
    }
}