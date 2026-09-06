using be_digitalin.Application.Ports;
using be_digitalin.DTOs;
using be_digitalin.DTOs.Requests;
using be_digitalin.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services.Content;

/// <summary>
/// Use-case FAQ: filter/search publik, daftar unggulan (maks 6), CRUD admin.
/// Query selalu lewat IApplicationDbContext (port) — tidak menyentuh
/// DigitalinDbContext concrete, tidak ada raw SQL.
/// </summary>
public class FaqService : IFaqService
{
    /// <summary>Homepage membutuhkan ruang — lebih dari 6 FAQ unggulan jadi galeri.</summary>
    public const int MaxFeatured = 6;

    private readonly IApplicationDbContext _db;

    public FaqService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<FaqResponse>> GetFaqsAsync(
        string? category = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Faqs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(f => f.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Cocokkan ke question ATAU answer, case-insensitive (sama seperti
            // perilaku FE lama). ToLower() eksplisit supaya deterministik di
            // semua provider (LIKE MySQL CI, tapi test InMemory ordinal).
            var term = search.Trim().ToLower();
            query = query.Where(f =>
                f.Question.ToLower().Contains(term) ||
                f.Answer.ToLower().Contains(term));
        }

        return await query
            .OrderBy(f => f.Id)
            .Select(f => ToResponse(f))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FaqResponse>> GetFeaturedFaqsAsync(
        CancellationToken cancellationToken = default)
    {
        // Urut Id: deterministik bila sumber data menandai >6 unggulan
        // (seed/admin manual) — yang diambil selama 6 yang sama, Id terkecil.
        return await _db.Faqs
            .AsNoTracking()
            .Where(f => f.Featured)
            .OrderBy(f => f.Id)
            .Take(MaxFeatured)
            .Select(f => ToResponse(f))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.Faqs
            .AsNoTracking()
            .Select(f => f.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);
    }

    public async Task<FaqResponse?> GetFaqByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var faq = await _db.Faqs
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        return faq is null ? null : ToResponse(faq);
    }

    public async Task<FaqResponse> CreateFaqAsync(
        CreateFaqRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request.Question, request.Answer, request.Category);

        if (request.Featured && await CountFeaturedAsync(cancellationToken) >= MaxFeatured)
        {
            throw new InvalidOperationException(
                $"Maksimal {MaxFeatured} FAQ unggulan. Matikan salah satu terlebih dahulu.");
        }

        var faq = new Faq
        {
            Question = request.Question.Trim(),
            Answer = request.Answer.Trim(),
            Category = request.Category.Trim(),
            Featured = request.Featured
        };

        _db.Faqs.Add(faq);
        await _db.SaveChangesAsync(cancellationToken);

        return ToResponse(faq);
    }

    public async Task<FaqResponse?> UpdateFaqAsync(
        int id,
        UpdateFaqRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request.Question, request.Answer, request.Category);

        var faq = await _db.Faqs.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (faq is null) return null;

        // Baru menghitung batas bila update MENYALAKAN unggulan (dari false
        // → true). FAQ yang sudah unggulan tetap unggulan tidak menambah jumlah.
        if (request.Featured && !faq.Featured &&
            await CountFeaturedAsync(cancellationToken, excludeId: id) >= MaxFeatured)
        {
            throw new InvalidOperationException(
                $"Maksimal {MaxFeatured} FAQ unggulan. Matikan salah satu terlebih dahulu.");
        }

        faq.Question = request.Question.Trim();
        faq.Answer = request.Answer.Trim();
        faq.Category = request.Category.Trim();
        faq.Featured = request.Featured;

        await _db.SaveChangesAsync(cancellationToken);

        return ToResponse(faq);
    }

    public async Task<bool> DeleteFaqAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var faq = await _db.Faqs.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (faq is null) return false;

        _db.Faqs.Remove(faq);
        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private Task<int> CountFeaturedAsync(
        CancellationToken cancellationToken,
        int? excludeId = null)
    {
        var query = _db.Faqs.AsNoTracking().Where(f => f.Featured);
        if (excludeId.HasValue)
        {
            // Hitung unggulan selain record yang sedang di-update.
            query = query.Where(f => f.Id != excludeId.Value);
        }

        return query.CountAsync(cancellationToken);
    }

    private static void Validate(string question, string answer, string category)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new InvalidOperationException("Pertanyaan tidak boleh kosong.");
        if (string.IsNullOrWhiteSpace(answer))
            throw new InvalidOperationException("Jawaban tidak boleh kosong.");
        if (string.IsNullOrWhiteSpace(category))
            throw new InvalidOperationException("Kategori tidak boleh kosong.");
    }

    private static FaqResponse ToResponse(Faq faq) =>
        new(faq.Id, faq.Question, faq.Answer, faq.Category, faq.Featured);
}
