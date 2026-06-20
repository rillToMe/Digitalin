using System.Data;

namespace be_digitalin.Models;

public class Customer
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatetAt { get; set; } = DateTime.UtcNow;
    
    // Google OAuth fields
    public string? GoogleId { get; set; }
    public string AuthProvider { get; set; } = "LOCAL";
    
    public ICollection<TopupOrder> Orders { get; set; } = new List<TopupOrder>();
}