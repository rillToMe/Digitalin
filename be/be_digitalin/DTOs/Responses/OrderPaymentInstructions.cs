namespace be_digitalin.DTOs;

/// <summary>
/// Instruksi pembayaran hasil charge Midtrans Core API — bentuk kontrak API
/// sekaligus format persist (TopupOrder.PaymentInstructionsJson, camelCase).
/// QRIS: <see cref="QrString"/>/<see cref="Actions"/>; VA:
/// <see cref="VaBank"/>+<see cref="VaNumber"/> (permata dinormalisasi ke
/// sini); e-wallet: <see cref="Actions"/> (deeplink/QR); Mandiri Bill
/// (echannel): <see cref="BillerCode"/>+<see cref="BillKey"/>; cstore:
/// <see cref="Store"/>+<see cref="PaymentCode"/>+<see cref="MerchantId"/>
/// (merchant id dibutuhkan simulator sandbox Indomaret & sebagian alur kasir).
/// </summary>
public sealed record OrderPaymentInstructions(
    string? QrString,
    IReadOnlyList<OrderPaymentAction> Actions,
    string? VaBank,
    string? VaNumber,
    string? BillerCode = null,
    string? BillKey = null,
    string? Store = null,
    string? PaymentCode = null,
    string? MerchantId = null);

/// <summary>Satu aksi pembayaran Midtrans (mis. generate-qr-code, deeplink-redirect).</summary>
public sealed record OrderPaymentAction(string Name, string Url);
