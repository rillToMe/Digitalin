using System.Security.Claims;
using System.Text;
using be_digitalin.Config;
using be_digitalin.Data;
using be_digitalin.DTOs;
using be_digitalin.Models;
using be_digitalin.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace be_digitalin.Services;

public class CustomerService : ICustomerService
{
    private readonly DigitalinDbContext _db;
    private readonly JwtSettings _jwtSettings;
    private readonly IOrderService _orderService;
    
    public CustomerService(DigitalinDbContext db, IOptions<JwtSettings> jwtSettings, IOrderService orderService)
    {
        _db = db;
        _jwtSettings = jwtSettings.Value;
        _orderService = orderService;
    }

    public async Task<Customer?> RegisterAsync(string email, string password, string fullName)
    {
        if (await _db.Customers.AnyAsync(u => u.Email == email)) return null;

        var customer = new Customer
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FullName = fullName
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return customer;
    }

    public async Task<string?> LoginAsync(string email, string password)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(u => u.Email == email);
        if (customer == null || !BCrypt.Net.BCrypt.Verify(password, customer.PasswordHash)) return null;

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            Subject = new ClaimsIdentity(new[] 
            { 
                new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString()),
                new Claim(ClaimTypes.Role, AppConstants.Roles.Customer) 
            }),
            Expires = DateTime.UtcNow.AddHours(24),
            SigningCredentials = credentials
        };

        return new JsonWebTokenHandler().CreateToken(tokenDescriptor);
    }
    
    //check email yang sudah di pakai
    public async Task<bool> CheckEmailExistsAsync(string email)
    {
        return await _db.Customers.AnyAsync(u => u.Email == email);
    }
    
    public async Task<Customer?> GetProfileAsync(int id)
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