namespace be_digitalin.DTOs;

// SSOT request DTO area produk — sebelumnya didefinisikan inline di
// Endpoints/ProductEndpoints.cs. Dipindah ke sini saat migrasi
// Minimal API → Controllers.

public record UpdateCustomMarginRequest(decimal? Margin);

public record UpdatePopularGamesRequest(IReadOnlyList<string>? Slugs);

public record UpdateExpiryRequest(int Expiry);