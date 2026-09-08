namespace be_digitalin.DTOs;

public record CreateOrderRequest(
    int ProductId,
    Dictionary<string, string?> TargetValues,
    string? VoucherCode = null,
    /// <summary>
    /// Metode pembayaran Midtrans Core API: "qris" | "gopay" | "bank_transfer"
    /// (case-insensitive). Wajib — transaksi selalu dibuat via Core API.
    /// </summary>
    string? PaymentMethod = null,
    /// <summary>Bank VA — wajib bila PaymentMethod = "bank_transfer"
    /// (bca/bni/bri/cimb/permata/mandiri/bsi). Diabaikan untuk metode lain.</summary>
    string? PaymentBank = null,
    /// <summary>Toko convenience store — wajib bila PaymentMethod = "cstore"
    /// (indomaret/alfamart). Diabaikan untuk metode lain.</summary>
    string? PaymentStore = null
);