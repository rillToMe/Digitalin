using be_digitalin.Config;
using be_digitalin.Data;
using be_digitalin.DTOs;
using be_digitalin.Models;
using be_digitalin.Constants;
using be_digitalin.Providers;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using be_digitalin.Services.Auth;
using be_digitalin.Services.Orders;
using be_digitalin.Services.Security;

namespace be_digitalin.Services.Customer;

public class CustomerService : ICustomerService
{
    private readonly DigitalinDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IOrderService _orderService;
    private readonly IRecaptchaService _recaptchaService;
    private readonly RecaptchaSettings _recaptchaSettings;
    private readonly EmailProvider _emailProvider;
    private readonly IMemoryCache _cache;

    public CustomerService(
        DigitalinDbContext db,
        ITokenService tokenService,
        IOrderService orderService,
        IRecaptchaService recaptchaService,
        IOptions<RecaptchaSettings> recaptchaSettings,
        EmailProvider emailProvider,
        IMemoryCache cache)
    {
        _db = db;
        _tokenService = tokenService;
        _orderService = orderService;
        _recaptchaService = recaptchaService;
        _recaptchaSettings = recaptchaSettings.Value;
        _emailProvider = emailProvider;
        _cache = cache;
    }
    
    public async Task<string?> RegisterAsync(
        string email,
        string password,
        string fullName,
        string otpCode,
        CancellationToken cancellationToken = default)
    {
        if (!_cache.TryGetValue(
                $"OTP_{email}",
                out string? savedOtp) ||
            savedOtp != otpCode)
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

        var customer = new Models.Customer
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FullName = fullName
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(cancellationToken);

        _cache.Remove($"OTP_{email}");

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

        _cache.Set(
            $"OTP_{email}",
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

        if (customer == null ||
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
    
    public async Task<Models.Customer?> GetProfileAsync(int id)
    {
        return await _db.Customers.FindAsync(id);
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
