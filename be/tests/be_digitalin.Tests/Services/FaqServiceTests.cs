using be_digitalin.Application.Ports;
using be_digitalin.Data;
using be_digitalin.Domain.Entities;
using be_digitalin.DTOs.Requests;
using be_digitalin.Services.Content;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace be_digitalin.Tests.Services;

/// <summary>
/// Unit test FaqService dengan EF InMemory provider (tanpa database sungguhan).
/// Menutup: CRUD, filter kategori, search question/answer, daftar unggulan
/// maks 6 + deterministik, aturan unggulan ke-7 ditolak, dan validasi field.
/// </summary>
public class FaqServiceTests
{
    private static DigitalinDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<DigitalinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DigitalinDbContext(options);
    }

    private static Faq NewFaq(
        string question = "Q",
        string answer = "A",
        string category = "Top Up",
        bool featured = false) =>
        new() { Question = question, Answer = answer, Category = category, Featured = featured };

    /// <summary>Isi 6 FAQ unggulan + 2 biasa — dasar kasus batas unggulan.</summary>
    private static async Task<DigitalinDbContext> SeedDbAsync()
    {
        var db = CreateDb();
        db.Faqs.AddRange(
            NewFaq("Q1", "A1", featured: true),
            NewFaq("Q2", "A2", featured: true),
            NewFaq("Q3", "A3", featured: true),
            NewFaq("Q4", "A4", featured: true),
            NewFaq("Q5", "A5", featured: true),
            NewFaq("Q6", "A6", featured: true),
            NewFaq("Q7", "A7"),
            NewFaq("Q8", "A8"));
        await db.SaveChangesAsync();
        return db;
    }

    // ── Public: filter & search ──

    [Fact]
    public async Task GetFaqsAsync_ReturnsAllOrderedById()
    {
        await using var db = await SeedDbAsync();
        var service = new FaqService(db);

        var faqs = await service.GetFaqsAsync();

        Assert.Equal(8, faqs.Count);
        Assert.Equal(Enumerable.Range(1, 8), faqs.Select(f => f.Id));
    }

    [Fact]
    public async Task GetFaqsAsync_CategoryFilter()
    {
        await using var db = CreateDb();
        db.Faqs.AddRange(
            NewFaq("Q1", "A1", "Top Up"),
            NewFaq("Q2", "A2", "Pembayaran"));
        await db.SaveChangesAsync();
        var service = new FaqService(db);

        var faqs = await service.GetFaqsAsync(category: "Pembayaran");

        var faq = Assert.Single(faqs);
        Assert.Equal("Pembayaran", faq.Category);
    }

    [Theory]
    [InlineData("diamond", 1)]   // cocok di answer
    [InlineData("Metode", 1)]    // cocok di question
    [InlineData("tidak-ada", 0)]
    public async Task GetFaqsAsync_SearchMatchesQuestionOrAnswer(string term, int expected)
    {
        await using var db = CreateDb();
        db.Faqs.AddRange(
            NewFaq("Metode pembayaran apa saja?", "GoPay dan OVO"),
            NewFaq("Diamond belum masuk?", "Tunggu 5-15 menit"));
        await db.SaveChangesAsync();
        var service = new FaqService(db);

        var faqs = await service.GetFaqsAsync(search: term);

        Assert.Equal(expected, faqs.Count);
    }

    [Fact]
    public async Task GetFeaturedFaqsAsync_CapsAtSixDeterministicById()
    {
        await using var db = CreateDb();
        // 8 unggulan di sumber data — service tetap kembalikan 6 (Id terkecil).
        db.Faqs.AddRange(
            NewFaq("Q1", featured: true), NewFaq("Q2", featured: true),
            NewFaq("Q3", featured: true), NewFaq("Q4", featured: true),
            NewFaq("Q5", featured: true), NewFaq("Q6", featured: true),
            NewFaq("Q7", featured: true), NewFaq("Q8", featured: true));
        await db.SaveChangesAsync();
        var service = new FaqService(db);

        var featured = await service.GetFeaturedFaqsAsync();

        Assert.Equal(6, featured.Count);
        Assert.All(featured, f => Assert.True(f.Featured));
        Assert.Equal(Enumerable.Range(1, 6), featured.Select(f => f.Id));
    }

    [Fact]
    public async Task GetCategoriesAsync_DistinctSorted()
    {
        await using var db = CreateDb();
        db.Faqs.AddRange(
            NewFaq("Q1", "A1", "Voucher"),
            NewFaq("Q2", "A2", "Top Up"),
            NewFaq("Q3", "A3", "Top Up"));
        await db.SaveChangesAsync();
        var service = new FaqService(db);

        var categories = await service.GetCategoriesAsync();

        Assert.Equal(new[] { "Top Up", "Voucher" }, categories);
    }

    // ── Create ──

    [Fact]
    public async Task CreateFaqAsync_Success_TrimsFields()
    {
        await using var db = CreateDb();
        var service = new FaqService(db);

        var faq = await service.CreateFaqAsync(
            new CreateFaqRequest("  Pertanyaan?  ", "  Jawaban.  ", "  Akun  ", false));

        Assert.Equal("Pertanyaan?", faq.Question);
        Assert.Equal("Jawaban.", faq.Answer);
        Assert.Equal("Akun", faq.Category);
        Assert.False(faq.Featured);
        Assert.Equal(1, await db.Faqs.CountAsync());
    }

    [Fact]
    public async Task CreateFaqAsync_SeventhFeatured_Rejected()
    {
        await using var db = await SeedDbAsync(); // sudah 6 unggulan
        var service = new FaqService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateFaqAsync(new CreateFaqRequest("Q9", "A9", "Lainnya", true)));

        Assert.Contains("Maksimal 6 FAQ unggulan", ex.Message);
        // Record tidak tersimpan — tidak ada yang diam-diam di-un-featured.
        Assert.Equal(8, await db.Faqs.CountAsync());
    }

    [Fact]
    public async Task CreateFaqAsync_NonFeaturedBeyondLimit_Allowed()
    {
        await using var db = await SeedDbAsync();
        var service = new FaqService(db);

        var faq = await service.CreateFaqAsync(
            new CreateFaqRequest("Q9", "A9", "Lainnya", false));

        Assert.False(faq.Featured);
    }

    [Theory]
    [InlineData("", "A", "Top Up", "Pertanyaan")]
    [InlineData("   ", "A", "Top Up", "Pertanyaan")]
    [InlineData("Q", "", "Top Up", "Jawaban")]
    [InlineData("Q", "   ", "Top Up", "Jawaban")]
    [InlineData("Q", "A", "", "Kategori")]
    [InlineData("Q", "A", "   ", "Kategori")]
    public async Task CreateFaqAsync_EmptyFields_Rejected(
        string question, string answer, string category, string expectedFragment)
    {
        await using var db = CreateDb();
        var service = new FaqService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateFaqAsync(new CreateFaqRequest(question, answer, category, false)));

        Assert.Contains(expectedFragment, ex.Message);
        Assert.Empty(await db.Faqs.ToListAsync());
    }

    // ── Update ──

    [Fact]
    public async Task UpdateFaqAsync_NotFound_ReturnsNull()
    {
        await using var db = CreateDb();
        var service = new FaqService(db);

        var result = await service.UpdateFaqAsync(
            999, new UpdateFaqRequest("Q", "A", "Top Up", false));

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateFaqAsync_Success_PersistsChanges()
    {
        await using var db = CreateDb();
        db.Faqs.Add(NewFaq("Q lama", "A lama", "Top Up"));
        await db.SaveChangesAsync();
        var service = new FaqService(db);

        var updated = await service.UpdateFaqAsync(
            1, new UpdateFaqRequest("Q baru", "A baru", "Akun", true));

        Assert.NotNull(updated);
        Assert.Equal("Q baru", updated!.Question);
        Assert.Equal("Akun", updated.Category);
        Assert.True(updated.Featured);
    }

    [Fact]
    public async Task UpdateFaqAsync_TurningOnFeaturedWhenSixExist_Rejected()
    {
        await using var db = await SeedDbAsync(); // Q7 (id 7) tidak unggulan, 6 lain unggulan
        var service = new FaqService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateFaqAsync(7, new UpdateFaqRequest("Q7", "A7", "Top Up", true)));

        Assert.Contains("Maksimal 6 FAQ unggulan", ex.Message);
    }

    [Fact]
    public async Task UpdateFaqAsync_AlreadyFeatured_StaysAllowedWithoutCounting()
    {
        await using var db = await SeedDbAsync(); // id 1 unggulan; update tetap unggulan
        var service = new FaqService(db);

        var updated = await service.UpdateFaqAsync(
            1, new UpdateFaqRequest("Q1 baru", "A1 baru", "Top Up", true));

        Assert.NotNull(updated);
        Assert.True(updated!.Featured);
    }

    [Fact]
    public async Task UpdateFaqAsync_UnfeaturingFreesSlotForCreate()
    {
        await using var db = await SeedDbAsync();
        var service = new FaqService(db);

        // Matikan satu unggulan → slot terbuka.
        await service.UpdateFaqAsync(1, new UpdateFaqRequest("Q1", "A1", "Top Up", false));

        // Sekarang FAQ baru boleh jadi unggulan ke-6.
        var created = await service.CreateFaqAsync(
            new CreateFaqRequest("Q9", "A9", "Lainnya", true));

        Assert.True(created.Featured);
    }

    // ── Delete ──

    [Fact]
    public async Task DeleteFaqAsync_NotFound_ReturnsFalse()
    {
        await using var db = CreateDb();
        var service = new FaqService(db);

        Assert.False(await service.DeleteFaqAsync(999));
    }

    [Fact]
    public async Task DeleteFaqAsync_Success_HardDelete()
    {
        await using var db = CreateDb();
        db.Faqs.Add(NewFaq("Q1", "A1"));
        await db.SaveChangesAsync();
        var service = new FaqService(db);

        Assert.True(await service.DeleteFaqAsync(1));
        Assert.Empty(await db.Faqs.ToListAsync());
    }

    [Fact]
    public async Task DeleteFaqAsync_FeaturedRecord_FreesSlot()
    {
        await using var db = await SeedDbAsync(); // 6 unggulan
        var service = new FaqService(db);

        Assert.True(await service.DeleteFaqAsync(1)); // hapus unggulan

        var created = await service.CreateFaqAsync(
            new CreateFaqRequest("Q9", "A9", "Lainnya", true));
        Assert.True(created.Featured);
    }

    // ── Detail ──

    [Fact]
    public async Task GetFaqByIdAsync_MapsPublicFields()
    {
        await using var db = CreateDb();
        db.Faqs.Add(NewFaq("Pertanyaan?", "Jawaban.", "Voucher", true));
        await db.SaveChangesAsync();
        var service = new FaqService(db);

        var faq = await service.GetFaqByIdAsync(1);

        Assert.NotNull(faq);
        Assert.Equal(1, faq!.Id);
        Assert.Equal("Pertanyaan?", faq.Question);
        Assert.Equal("Jawaban.", faq.Answer);
        Assert.Equal("Voucher", faq.Category);
        Assert.True(faq.Featured);
    }
}
