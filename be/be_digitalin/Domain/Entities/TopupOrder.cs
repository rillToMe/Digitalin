using System.ComponentModel.DataAnnotations;
using be_digitalin.Constants;

namespace be_digitalin.Domain.Entities;

public class TopupOrder
{
    public int Id { get; set; }
    
    public string OrderNo { get; set; } = string.Empty;

    // HMAC-SHA256(SecretKey, OrderNo) — hex kecil 64 char. Hanya representasi
    // URL publik (/order/status/{publicHash}); OrderNo tetap identifier bisnis
    // (Midtrans order_id, Digiflazz ref_id). Unique index di DbContext.
    public string? PublicHash { get; set; }

    public string PlayerId { get; set; } = string.Empty;
    public string Status { get; set; } = AppConstants.OrderStatus.Pending;
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    // ── Midtrans Core API ──

    // payment_type Midtrans ("qris" / "gopay" / "bank_transfer")
    public string? PaymentType { get; set; }

    // transaction_id Midtrans — kunci rekonsiliasi selain OrderNo
    public string? MidtransTransactionId { get; set; }

    // JSON OrderPaymentInstructions (camelCase) — instruksi bayar QRIS/VA/e-wallet
    public string? PaymentInstructionsJson { get; set; }
    
    public int GameProductId { get; set; }
    public GameProduct? GameProduct { get; set; }
    public string? Sn { get; set; }
    
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    
    public string? VoucherCode { get; set; }
    public decimal DiscountAmount { get; set; }
    
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public DateTime? ProcessedAt { get; set; }

    // ── Transaction Snapshot (immutable setelah order dibuat) ─────

    // Snapshot Produk
    public string ProductName { get; set; } = string.Empty;
    public string BuyerSkuCode { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }

    // Slug katalog brand saat order dibuat (dari GameMetadata.BrandSlug) —
    // identifier PUBLIK (dipakai di URL /game/{slug}), aman diekspos ke
    // respons invoice publik untuk fitur "Terakhir Dibeli" guest. Null untuk
    // order lama pra-kolom ini.
    public string? BrandSlug { get; set; }

    // Snapshot Harga
    public decimal OriginalPrice { get; set; }
    public decimal TotalPaid { get; set; }

    // Snapshot Modal & Profit
    public decimal CostPrice { get; set; }
    public decimal ProfitAmount { get; set; }
}