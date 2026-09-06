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
        await SendAsync(
            targetEmail,
            "Kode Verifikasi (OTP) Digitalin",
            BuildOtpEmail(
                title: "Verifikasi Akun Anda",
                intro: "Halo,<br><br>" +
                       "Kamu baru saja memulai pendaftaran di <strong>Digitalin</strong>. " +
                       "Masukkan kode verifikasi (OTP) di bawah ini untuk melanjutkan - " +
                       "dan langsung gas top-up game favoritmu.",
                otpCode: otpCode,
                securityNote: "Kode ini berlaku selama <strong>5 menit</strong>. " +
                              "Jangan bagikan kode ini kepada siapa pun, termasuk pihak yang " +
                              "mengaku sebagai staf kami.",
                closingNote: "Jika kamu tidak merasa melakukan pendaftaran ini, abaikan saja " +
                             "email ini atau hubungi tim dukungan kami jika merasa ada aktivitas " +
                             "mencurigakan."));
    }

    /// <summary>
    /// Sends a dedicated password-reset OTP email with a subject and body
    /// that clearly communicate the reset-password context.
    /// </summary>
    public async Task SendPasswordResetOtpEmailAsync(string targetEmail, string otpCode, CancellationToken cancellationToken = default)
    {
        await SendAsync(
            targetEmail,
            "Reset Password - Kode OTP Digitalin",
            BuildOtpEmail(
                title: "Reset Password Akun Anda",
                intro: "Halo,<br><br>" +
                       "Kami menerima permintaan reset password untuk akun " +
                       "<strong>Digitalin</strong> kamu. Gunakan kode verifikasi (OTP) " +
                       "di bawah ini untuk melanjutkan proses reset password.",
                otpCode: otpCode,
                securityNote: "Kode ini berlaku selama <strong>5 menit</strong>. " +
                              "Jangan bagikan kode ini kepada siapa pun.",
                closingNote: "Jika kamu tidak meminta reset password, abaikan saja email " +
                             "ini - password kamu tidak akan berubah. Hubungi tim dukungan " +
                             "kami jika merasa ada aktivitas mencurigakan."));
    }

    // ── Helpers ──────────────────────────────────────────────────

    /// <summary>
    /// SMTP connect → authenticate → send → disconnect. Kedua email OTP
    /// berbagi jalur kirim yang sama; kegagalan tetap dilempar ke caller
    /// (flow OTP yang menentukan respons API).
    /// </summary>
    private async Task SendAsync(string targetEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
        message.To.Add(new MailboxAddress("Customer", targetEmail));
        message.Subject = subject;

        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

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

    /// <summary>
    /// Template email OTP mengikuti desain referensi (docs: index.html):
    /// latar abu terang, kartu putih, header biru langit, box OTP
    /// border dashed. Responsif (mobile/tablet/desktop):
    /// kartu fluid width:100% + max-width:600px, media query meringkas
    /// padding & ukuran font di layar kecil. Inline style tetap membawa
    /// nilai desktop sebagai fallback untuk client yang membuang &lt;style&gt;.
    /// </summary>
    private static string BuildOtpEmail(
        string title,
        string intro,
        string otpCode,
        string securityNote,
        string closingNote)
    {
        const string skyBlue = "#0ea5e9";
        const string otpBg = "#f0f9ff";
        const string otpBorder = "#bae6fd";

        return $@"
<!DOCTYPE html>
<html lang=""id"">
<head>
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <style>
    /* Progressive enhancement — client tanpa dukungan <style> tetap
       render benar lewat inline style (fluid width). */
    @media only screen and (max-width: 620px) {{
      .email-card {{ width: 100% !important; }}
      .header-pad {{ padding: 24px 20px !important; }}
      .content-pad {{ padding: 28px 20px !important; }}
      .footer-pad {{ padding: 16px 20px !important; }}
      .content-pad h1 {{ font-size: 18px !important; }}
      .otp-code {{ font-size: 26px !important; letter-spacing: 4px !important; }}
      .brand-name {{ font-size: 20px !important; letter-spacing: 3px !important; }}
    }}
  </style>
</head>
<body bgcolor=""#f4f7f9"" style=""margin:0;padding:0;background-color:#f4f7f9;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;-webkit-font-smoothing:antialiased;"">
  <table width=""100%"" bgcolor=""#f4f7f9"" cellpadding=""0"" cellspacing=""0"" border=""0"" role=""presentation"" style=""table-layout:fixed;padding:40px 0;"">
    <tr>
      <td align=""center"">
        <table width=""600"" class=""email-card"" cellpadding=""0"" cellspacing=""0"" border=""0"" role=""presentation"" style=""width:100%;max-width:600px;background-color:#ffffff;border-radius:8px;overflow:hidden;box-shadow:0 4px 12px rgba(0,0,0,0.05);"">

          <!-- Header brand -->
          <tr>
            <td align=""center"" bgcolor=""{skyBlue}"" class=""header-pad"" style=""background-color:{skyBlue};padding:30px;"">
              <span class=""brand-name"" style=""font-size:24px;font-weight:bold;color:#ffffff;letter-spacing:4px;"">DIGITALIN</span><br>
              <span style=""font-size:11px;color:#e0f2fe;letter-spacing:3px;"">GAME TOP-UP STORE</span>
            </td>
          </tr>

          <!-- Konten utama -->
          <tr>
            <td class=""content-pad"" style=""padding:40px 30px;color:#333333;line-height:1.6;font-size:15px;"">
              <h1 style=""font-size:22px;margin:0 0 16px;color:#0f172a;"">{title}</h1>
              <p style=""margin:0 0 16px;"">{intro}</p>

              <!-- Box OTP -->
              <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" role=""presentation"">
                <tr>
                  <td align=""center"" style=""padding:20px 12px;background-color:{otpBg};border:1px dashed {otpBorder};border-radius:6px;"">
                    <span style=""font-size:11px;color:#64748b;letter-spacing:3px;"">KODE OTP</span>
                    <div class=""otp-code"" style=""font-size:32px;font-weight:bold;color:{skyBlue};letter-spacing:6px;padding-top:6px;"">{otpCode}</div>
                  </td>
                </tr>
              </table>

              <p style=""font-size:14px;color:#64748b;margin:24px 0 16px;text-align:center;"">{securityNote}</p>

              <p style=""margin:0 0 24px;"">{closingNote}</p>

              <p style=""margin:0;"">Salam hangat,<br><strong>Tim Dukungan Digitalin</strong></p>
            </td>
          </tr>

          <!-- Footer -->
          <tr>
            <td bgcolor=""#f8fafc"" class=""footer-pad"" style=""background-color:#f8fafc;padding:20px 30px;text-align:center;font-size:12px;color:#94a3b8;border-top:1px solid #e2e8f0;"">
              <p style=""margin:0 0 4px;"">&copy; 2026 Digitalin. Semua Hak Dilindungi.</p>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
    }
}
