using System.ComponentModel.DataAnnotations;

namespace be_digitalin.Config;

public class MidtransSettings
{
    [Required]
    public string ServerKey {get; set;} = string.Empty;

    // Root Core API TANPA "/v2" — endpoint lengkap disusun provider.
    // Sandbox: https://api.sandbox.midtrans.com — produksi: https://api.midtrans.com.
    [Required]
    public string CoreApiBaseUrl {get; set;} = string.Empty;

    // Base URL frontend (mis. http://localhost:5173) — dipakai menyusun
    // callback_url charge ShopeePay: {FrontendBaseUrl}/order/status/{orderNo}.
    // Wajib: ShopeePay aktif di seed default, tanpa callback charge-nya 400.
    [Required]
    public string FrontendBaseUrl {get; set;} = string.Empty;
}
