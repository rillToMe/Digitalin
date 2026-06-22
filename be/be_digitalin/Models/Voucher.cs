namespace be_digitalin.Models;

public class Voucher
{
    public int Id { get; set; }
    
    // Contoh: "HEMAT20", "NEWUSER50" — unik, case-insensitive
    public string Code { get; set; } = string.Empty;
    
    // "Percentage" atau "FixedAmount"
    public string DiscountType { get; set; } = "Percentage";
    
    // Untuk Percentage: 10 = 10%. Untuk FixedAmount: 5000 = Rp5.000
    public decimal DiscountValue { get; set; }
    
    // Minimal harga order untuk bisa pakai voucher ini
    public decimal MinimumTransaction { get; set; }
    
    // Cap diskon (khusus Percentage). null = tanpa batas
    public decimal? MaximumDiscount { get; set; }
    
    // Maksimal total pemakaian voucher
    public int UsageLimit { get; set; } = 100;
    
    // Berapa kali sudah dipakai (incremented by system)
    public int UsedCount { get; set; }
    
    // Tanggal kedaluwarsa
    public DateTime ExpiredAt { get; set; }
    
    // Status aktif (bisa dinonaktifkan tanpa dihapus)
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}