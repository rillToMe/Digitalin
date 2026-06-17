using be_digitalin.Models;
using be_digitalin.DTOs;

namespace be_digitalin.Services;

public interface ICustomerService
{
    Task<Customer?> RegisterAsync(string email, string password, string fullName);
    Task<string?> LoginAsync(string email, string password);
    Task<PagedResult<OrderResponse>> GetCustomerHistoryAsync(int customerId, int page = 1, int limit = 20);
    
    Task<Customer?> GetProfileAsync(int id);
    Task<bool> UpdateProfileAsync(int id, string fullName, string? newPassword);
    
    Task<bool> CheckEmailExistsAsync(string email);
}