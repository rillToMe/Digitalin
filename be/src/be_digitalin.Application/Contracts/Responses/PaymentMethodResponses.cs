namespace be_digitalin.DTOs;

// ── Publik (checkout) — hanya item aktif, tanpa field admin ──

public record PaymentMethodResponse(
    string Code,
    string Name,
    string? Description,
    string? IconUrl,
    IReadOnlyList<PaymentBankResponse> Banks);

public record PaymentBankResponse(
    string Code,
    string Name,
    string? IconUrl);

// ── Admin — semua state (termasuk nonaktif) ──

public record AdminPaymentMethodResponse(
    string Code,
    string Name,
    string? Description,
    string? IconUrl,
    bool Enabled,
    int SortOrder,
    IReadOnlyList<AdminPaymentBankResponse> Banks);

public record AdminPaymentBankResponse(
    string Code,
    string Name,
    string? IconUrl,
    bool Enabled,
    int SortOrder);
