using be_digitalin.Models;
using be_digitalin.DTOs;
using be_digitalin.Services.Orders;

namespace be_digitalin.Services.Customer;

public interface ICustomerService
{
    Task<string?> RegisterAsync(
        string email,
        string password,
        string fullName,
        string otpCode,
        CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> RequestRegistrationOtpAsync(
        string email,
        string captchaToken,
        string? remoteIp = null,
        CancellationToken cancellationToken = default);
    Task<string?> LoginAsync(string email, string password, string captchaToken, string? remoteIp = null, CancellationToken cancellationToken = default);
    Task<PagedResult<OrderResponse>> GetCustomerHistoryAsync(int customerId, int page = 1, int limit = 20);
    Task<Models.Customer?> GetProfileAsync(int id);
    Task<bool> UpdateProfileAsync(int id, string fullName, string? newPassword);
    Task<bool> CheckEmailExistsAsync(string email);
}