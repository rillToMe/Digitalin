using be_digitalin.Config;
using be_digitalin.Application.Ports;
using be_digitalin.DTOs;
using be_digitalin.Domain.Entities;
using be_digitalin.Constants;

using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using be_digitalin.Services.Auth;
using be_digitalin.Services.Orders;
using be_digitalin.Services.Security;

namespace be_digitalin.Services.Customer;

public class CustomerService : ICustomerService
{
    private readonly IApplicationDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IOrderService _orderService;
    private readonly IRecaptchaService _recaptchaService;
    private readonly RecaptchaSettings _recaptchaSettings;
    private readonly IEmailProvider _emailProvider;
    private readonly IRedisService _redis;

    public CustomerService(
        IApplicationDbContext db,
        ITokenService tokenService,
        IOrderService orderService,
        IRecaptchaService recaptchaService,
        IOptions<RecaptchaSettings> recaptchaSettings,
        IEmailProvider emailProvider,
        IRedisService redis)
    {
        _db = db;
        _tokenService = tokenService;
        _orderService = orderService;
        _recaptchaService = recaptchaService;
        _recaptchaSettings = recaptchaSettings.Value;
        _emailProvider = emailProvider;
        _redis = redis;
    }
    
    /// <summary>Key Redis OTP registrasi — email dipakai apa adanya sesuai flow existing.</summary>
    private static string OtpKey(string email) => $"digitalin:otp:registration:{email}";

    public async Task<string?> RegisterAsync(
        string email,
        string password,
        string fullName,
        string otpCode,
        CancellationToken cancellationToken = default)
    {
        var savedOtp = await _redis.GetAsync(OtpKey(email));
        if (savedOtp is null || savedOtp != otpCode)
        {
            return null;
        }

        var emailAlreadyExists = await _db.Customers.AnyAsync(
            u => u.Email == email,
            cancellationToken);

        if (emailAlreadyExists)
        {
            return null;
        }

        var customer = new Domain.Entities.Customer
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FullName = fullName
        };

        _db.Customers.Add(customer);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Kalah race melawan registrasi paralel dengan email sama:
            // unique index IX_Customers_Email menolak insert kedua. Perlakuan
            // sama dengan OTP salah / email sudah ada → null (400 kontrak),
            // bukan 500. (Pola yang sama dengan GoogleAuthService.)
            return null;
        }

        await _redis.DeleteAsync(OtpKey(email));

        return _tokenService.CreateCustomerToken(customer.Id);
    }
    
    public async Task<(bool Success, string Message)> RequestRegistrationOtpAsync(
        string email,
        string captchaToken,
        string? remoteIp = null,
        CancellationToken cancellationToken = default)
    {
        var captchaValid = await _recaptchaService.VerifyAsync(
            captchaToken,
            AppConstants.RecaptchaActions.RegisterRequestOtp,
            _recaptchaSettings.RegisterMinimumScore,
            remoteIp,
            cancellationToken);

        if (!captchaValid)
        {
            return (
                false,
                "Verifikasi keamanan gagal. Silakan coba lagi."
            );
        }

        var emailAlreadyExists = await CheckEmailExistsAsync(email);

        if (emailAlreadyExists)
        {
            return (
                false,
                "Email sudah terdaftar. Silakan login."
            );
        }

        var otpNumber = RandomNumberGenerator.GetInt32(
            100_000,
            1_000_000);

        var otpCode = otpNumber.ToString();

        await _redis.SetAsync(
            OtpKey(email),
            otpCode,
            TimeSpan.FromMinutes(5));

        await _emailProvider.SendOtpEmailAsync(
            email,
            otpCode);

        return (
            true,
            "Kode OTP berhasil dikirim ke email Anda. Berlaku 5 menit."
        );
    }

    public async Task<string?> LoginAsync(
        string email,
        string password,
        string captchaToken,
        string? remoteIp = null,
        CancellationToken cancellationToken = default)
    {
        var captchaValid = await _recaptchaService.VerifyAsync(
            captchaToken,
            AppConstants.RecaptchaActions.Login,
            _recaptchaSettings.LoginMinimumScore,
            remoteIp,
            cancellationToken);

        if (!captchaValid)
        {
            return null;
        }

        var customer = await _db.Customers
            .FirstOrDefaultAsync(
                u => u.Email == email,
                cancellationToken);

        // Akun Google-only tidak punya password hash — coba login lokal = gagal
        // kredensial (null → 401), bukan exception BCrypt pada hash kosong.
        if (customer == null ||
            string.IsNullOrEmpty(customer.PasswordHash) ||
            !BCrypt.Net.BCrypt.Verify(password, customer.PasswordHash))
        {
            return null;
        }

        return _tokenService.CreateCustomerToken(customer.Id);
    }

    //check email yang sudah di pakai
    public async Task<bool> CheckEmailExistsAsync(string email)
    {
        return await _db.Customers.AnyAsync(u => u.Email == email);
    }
    
    public async Task<Domain.Entities.Customer?> GetProfileAsync(int id)
    {
        // Read-only: profile tidak dimodifikasi setelah retrieval
        // (update lewat UpdateProfileAsync yang FindAsync terpisah).
        return await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<bool> UpdateProfileAsync(int id, string fullName, string? newPassword)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer == null) return false;

        customer.FullName = fullName;

        // Jika user ngisi password baru, hash lagi
        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            customer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        }

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<OrderResponse>> GetCustomerHistoryAsync(int customerId, int page = 1, int limit = 20)
    {
        return await _orderService.GetOrdersAsync(customerId, page, limit);
    }
    
    
}
