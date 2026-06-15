using System.Data;

namespace be_digitalin.Models;

public class Customer
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatetAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<TopupOrder> Orders { get; set; } = new List<TopupOrder>();
}