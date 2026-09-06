namespace be_digitalin.Application.Ports;

/// <summary>
/// Satu baris price-list Digiflazz, hasil parse milik <c>IDigiflazzProvider</c>.
/// Kontrak data port — menggantikan <c>JsonElement.ArrayEnumerator</c> yang
/// (a) memaksa caller memegang <c>JsonDocument</c> induk agar tidak ke dispose,
/// dan (b) membocorkan bentuk JSON provider ke layer Application.
/// </summary>
public sealed record DigiflazzPriceItem(
    string? Category,
    string BuyerSkuCode,
    string ProductName,
    decimal Price,
    string Brand,
    bool BuyerProductStatus,
    bool SellerProductStatus,
    string? Desc);
