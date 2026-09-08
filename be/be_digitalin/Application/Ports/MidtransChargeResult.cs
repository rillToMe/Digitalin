namespace be_digitalin.Application.Ports;

/// <summary>
/// Hasil charge Midtrans Core API: envelope transaksi generik + instruksi
/// pembayaran per keluarga metode — QRIS: <see cref="QrString"/>/<see cref="Actions"/>,
/// VA: <see cref="VaNumbers"/>/<see cref="PermataVaNumber"/>/<see cref="BillKey"/>+<see cref="BillerCode"/>,
/// e-wallet: <see cref="Actions"/> (deeplink/QR), cstore: <see cref="PaymentCode"/>+<see cref="Store"/>
/// (+<see cref="MerchantId"/> — dibutuhkan simulator sandbox Indomaret dan
/// sebagian alur kasir).
/// </summary>
public sealed record MidtransChargeResult(
    string? TransactionId,
    string? PaymentType,
    string? TransactionStatus,
    string? FraudStatus,
    string? StatusCode,
    string? StatusMessage,
    string? QrString,
    IReadOnlyList<MidtransAction> Actions,
    IReadOnlyList<MidtransVaNumber> VaNumbers,
    string? PermataVaNumber,
    string? BillKey,
    string? BillerCode,
    string? PaymentCode = null,
    string? Store = null,
    string? MerchantId = null);

/// <summary>Satu aksi pembayaran dari Midtrans (mis. generate-qr-code, deeplink-redirect).</summary>
public sealed record MidtransAction(string Name, string Url);

/// <summary>Nomor Virtual Account per bank hasil charge bank_transfer.</summary>
public sealed record MidtransVaNumber(string Bank, string VaNumber);
