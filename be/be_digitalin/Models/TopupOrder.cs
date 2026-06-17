using System.ComponentModel.DataAnnotations;
using be_digitalin.Constants;

namespace be_digitalin.Models;

public class TopupOrder
{
    public int Id { get; set; }
    
    public string OrderNo { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public string Status { get; set; } = AppConstants.OrderStatus.Pending;
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    
    //token dari midtrans
    public string? SnapToken { get; set; }
    
    //direct kalau berhasil membayar
    public string? RedirectUrl { get; set; }
    
    public int GameProductId { get; set; }
    public GameProduct? GameProduct { get; set; }
    public string? Sn { get; set; }
    
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    
    public string? VoucherCode { get; set; }
    public decimal DiscountAmount { get; set; } // Simpan nominal diskon yang didapat
    
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public DateTime? ProcessedAt { get; set; }
}