namespace be_digitalin.Models;

public class Voucher
{
    public int Id { get; set; }
    
    // Contoh: "MAINBARENGD2C"
    public string Code { get; set; } = string.Empty; 
    
    // Contoh: 1 (untuk 1%)
    public decimal DiscountPercentage { get; set; } 
    
    // Contoh: 25000
    public decimal MinTransactionAmount { get; set; } 
    
    // Contoh: null (kalau tidak terbatas) atau 10000 (maksimal diskon 10rb)
    public decimal? MaxDiscountAmount { get; set; } 
    
    public DateTime ValidUntil { get; set; }
    
    // Opsional tapi penting: Buat ngebatesin cuma bisa dipakai 100 orang
    public int Quota { get; set; } = 100; 
    
    public bool IsActive { get; set; } = true;
}