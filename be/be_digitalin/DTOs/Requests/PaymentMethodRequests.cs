namespace be_digitalin.DTOs;

/// <summary>
/// Update parsial konfigurasi metode pembayaran — hanya field allow-list;
/// Code tidak dapat diubah (identitas). Field null = tidak diubah.
/// </summary>
public record UpdatePaymentMethodRequest(
    string? DisplayName = null,
    string? IconUrl = null,
    bool? Enabled = null,
    int? SortOrder = null);

/// <summary>Update parsial bank VA — semantik sama dengan method.</summary>
public record UpdatePaymentBankRequest(
    string? DisplayName = null,
    string? IconUrl = null,
    bool? Enabled = null,
    int? SortOrder = null);
