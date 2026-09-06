using be_digitalin.DTOs;
using be_digitalin.DTOs.Requests;

namespace be_digitalin.Services.Content;

public interface IFaqService
{
    /// <summary>FAQ publik dengan filter opsional kategori + search (question/answer).</summary>
    Task<IReadOnlyList<FaqResponse>> GetFaqsAsync(
        string? category = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>FAQ unggulan untuk homepage — maks 6, deterministik (urut Id).</summary>
    Task<IReadOnlyList<FaqResponse>> GetFeaturedFaqsAsync(CancellationToken cancellationToken = default);

    /// <summary>Daftar kategori distinct — FE tidak boleh hardcode kategori.</summary>
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<FaqResponse?> GetFaqByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Validasi field + batas maksimal FAQ unggulan → InvalidOperationException.</summary>
    Task<FaqResponse> CreateFaqAsync(CreateFaqRequest request, CancellationToken cancellationToken = default);

    /// <summary>Validasi field + batas maksimal FAQ unggulan → InvalidOperationException.
    /// Return null bila id tidak ditemukan.</summary>
    Task<FaqResponse?> UpdateFaqAsync(int id, UpdateFaqRequest request, CancellationToken cancellationToken = default);

    /// <summary>Hard delete. False bila id tidak ditemukan.</summary>
    Task<bool> DeleteFaqAsync(int id, CancellationToken cancellationToken = default);
}
