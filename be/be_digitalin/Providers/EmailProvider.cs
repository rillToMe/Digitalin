using be_digitalin.Application.Ports;
using be_digitalin.Config;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace be_digitalin.Providers;

public class EmailProvider : IEmailProvider
{
    private readonly EmailSettings _settings;

    public EmailProvider(IOptions<EmailSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendOtpEmailAsync(string targetEmail, string otpCode, CancellationToken cancellationToken = default)
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
            await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls, cancellationToken);
            await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
            
            // Kirim Email
            await client.SendAsync(message, cancellationToken);
        }
        finally
        {
            await client.DisconnectAsync(true, cancellationToken);
        }
    }

    /// <summary>
    /// Sends a dedicated password-reset OTP email with a subject and body
    /// that clearly communicate the reset-password context.
    /// </summary>
    public async Task SendPasswordResetOtpEmailAsync(string targetEmail, string otpCode, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
        message.To.Add(new MailboxAddress("Customer", targetEmail));
        message.Subject = "Reset Password — Kode OTP Digitalin";

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px;'>
                    <h2>Permintaan Reset Password</h2>
                    <p>Kami menerima permintaan untuk mereset password akun Digitalin kamu.</p>
                    <p>Gunakan kode OTP berikut untuk melanjutkan proses reset password:</p>
                    <h1 style='color: #e74c3c; letter-spacing: 5px;'>{otpCode}</h1>
                    <p>Kode ini hanya berlaku selama <strong>5 menit</strong>.</p>
                    <p style='color: #888;'>Jika kamu tidak meminta reset password, abaikan email ini. Password kamu tidak akan berubah.</p>
                    <p style='color: #888;'>Jangan berikan kode ini kepada siapapun.</p>
                </div>"
        };
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls, cancellationToken);
            await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
        }
        finally
        {
            await client.DisconnectAsync(true, cancellationToken);
        }
    }
}
