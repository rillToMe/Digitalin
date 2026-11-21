using be_digitalin.Application.Ports;
using be_digitalin.Application.Validation;
using be_digitalin.Data;
using be_digitalin.DTOs;
using be_digitalin.Services.Payments;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace be_digitalin.Tests.Services;

/// <summary>
/// Unit test PaymentMethodService dengan EF InMemory (seed HasData ikut
/// terpasang oleh EnsureCreated): respons publik hanya item aktif + urut
/// SortOrder, respons admin memuat semua state, update parsial persist,
/// validasi URL/nama/urutan, upload ikon R2 (validasi magic bytes, replace
/// hapus file lama), dan code tak dikenal → null (→ 404 controller).
/// </summary>
public class PaymentMethodServiceTests
{
    /// <summary>Fake R2 — catat upload/delete, URL publik sintetis.</summary>
    private sealed class FakeR2Provider : ICloudflareR2Provider
    {
        public List<(string Folder, string FileName)> Uploads { get; } = [];
        public List<string> DeletedUrls { get; } = [];

        public Task<string> UploadFileAsync(
            ImageFileData file, string folder, CancellationToken cancellationToken = default)
        {
            var fileName = $"{folder}/{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            Uploads.Add((folder, file.FileName));
            return Task.FromResult($"https://r2.test/{fileName}");
        }

        public Task<bool> DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default)
        {
            DeletedUrls.Add(fileUrl);
            return Task.FromResult(true);
        }
    }

    /// <summary>PNG 1x1 valid (magic bytes asli) untuk tes upload.</summary>
    private static byte[] PngBytes =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52
    ];

    private static async Task<DigitalinDbContext> CreateDbAsync()
    {
        var options = new DbContextOptionsBuilder<DigitalinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new DigitalinDbContext(options);
        await db.Database.EnsureCreatedAsync(); // seed HasData ikut
        return db;
    }

    private static PaymentMethodService CreateService(DigitalinDbContext db, FakeR2Provider? r2 = null) =>
        new(db, r2 ?? new FakeR2Provider());

    // ── Respons publik (checkout) ──

    [Fact]
    public async Task GetEnabled_ReturnsOnlyEnabled_OrderedBySortOrder()
    {
        await using var db = await CreateDbAsync();

        var result = await CreateService(db).GetEnabledForCheckoutAsync();

        // E-wallet non-Midtrans (dana/linkaja/ovo) nonaktif — tersembunyi.
        Assert.Equal(new[] { "qris", "gopay", "shopeepay", "bank_transfer", "cstore" },
            result.Select(m => m.Code).ToArray());
        var transfer = result.Single(m => m.Code == "bank_transfer");
        // bsi di-seed nonaktif (hanya BYOND app, tidak interbank) — tersembunyi.
        Assert.Equal(new[] { "bca", "bni", "bri", "cimb", "permata", "mandiri" },
            transfer.Banks.Select(b => b.Code).ToArray());
        // Sub-kanal toko cstore.
        var cstore = result.Single(m => m.Code == "cstore");
        Assert.Equal(new[] { "indomaret", "alfamart" },
            cstore.Banks.Select(b => b.Code).ToArray());
    }

    [Fact]
    public async Task GetEnabled_ExcludesDisabledMethodAndItsBanks()
    {
        await using var db = await CreateDbAsync();
        var gopay = await db.PaymentMethods.SingleAsync(m => m.Code == "gopay");
        gopay.Enabled = false;
        var bca = await db.PaymentBanks.SingleAsync(b => b.Code == "bca");
        bca.Enabled = false;
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetEnabledForCheckoutAsync();

        Assert.DoesNotContain(result, m => m.Code == "gopay");
        Assert.DoesNotContain(
            result.Single(m => m.Code == "bank_transfer").Banks,
            b => b.Code == "bca");
    }

    [Fact]
    public async Task GetEnabled_ResponseContainsNoAdminOnlyFields()
    {
        await using var db = await CreateDbAsync();

        var result = await CreateService(db).GetEnabledForCheckoutAsync();

        // Bentuk publik hanya code/name/description/iconUrl/banks —
        // enabled/sortOrder tidak bocor.
        var methodProps = typeof(PaymentMethodResponse).GetProperties().Select(p => p.Name).OrderBy(n => n);
        Assert.Equal(new[] { "Banks", "Code", "Description", "IconUrl", "Name" }, methodProps);
        var bankProps = typeof(PaymentBankResponse).GetProperties().Select(p => p.Name).OrderBy(n => n);
        Assert.Equal(new[] { "Code", "IconUrl", "Name" }, bankProps);
    }

    // ── Respons admin ──

    [Fact]
    public async Task GetAllForAdmin_IncludesDisabled()
    {
        await using var db = await CreateDbAsync();

        var result = await CreateService(db).GetAllForAdminAsync();

        var transfer = result.Single(m => m.Code == "bank_transfer");
        Assert.Equal(7, transfer.Banks.Count); // semua bank termasuk mandiri (aktif) + bsi (nonaktif)
        Assert.True(transfer.Banks.Single(b => b.Code == "mandiri").Enabled);
        Assert.False(transfer.Banks.Single(b => b.Code == "bsi").Enabled);
        // E-wallet non-Midtrans tetap nonaktif — hanya terlihat admin.
        var dana = result.Single(m => m.Code == "dana");
        Assert.False(dana.Enabled);
    }

    // ── Update metode ──

    [Fact]
    public async Task UpdateMethod_PersistsIconUrlAndEnabled()
    {
        await using var db = await CreateDbAsync();
        var service = CreateService(db);

        var updated = await service.UpdateMethodAsync("qris",
            new UpdatePaymentMethodRequest(
                IconUrl: "https://digitalin.kyuzenstudio.it.com/payment/qris.png",
                Enabled: true));

        Assert.NotNull(updated);
        Assert.Equal("https://digitalin.kyuzenstudio.it.com/payment/qris.png", updated!.IconUrl);
        Assert.True(updated.Enabled);

        // Persist — bukan hanya respons.
        var row = await db.PaymentMethods.AsNoTracking().SingleAsync(m => m.Code == "qris");
        Assert.Equal("https://digitalin.kyuzenstudio.it.com/payment/qris.png", row.IconUrl);
    }

    [Fact]
    public async Task UpdateMethod_Disable_ExcludesFromCheckout()
    {
        await using var db = await CreateDbAsync();
        var service = CreateService(db);

        await service.UpdateMethodAsync("gopay", new UpdatePaymentMethodRequest(Enabled: false));

        var checkout = await service.GetEnabledForCheckoutAsync();
        Assert.DoesNotContain(checkout, m => m.Code == "gopay");
    }

    [Fact]
    public async Task UpdateMethod_InvalidUrl_Rejected()
    {
        await using var db = await CreateDbAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(db).UpdateMethodAsync(
                "qris", new UpdatePaymentMethodRequest(IconUrl: "ftp://bukan-http.png")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(db).UpdateMethodAsync(
                "qris", new UpdatePaymentMethodRequest(IconUrl: "bukan-url")));
    }

    [Fact]
    public async Task UpdateMethod_EmptyName_Rejected()
    {
        await using var db = await CreateDbAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(db).UpdateMethodAsync(
                "qris", new UpdatePaymentMethodRequest(DisplayName: "  ")));
    }

    [Fact]
    public async Task UpdateMethod_NegativeSortOrder_Rejected()
    {
        await using var db = await CreateDbAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(db).UpdateMethodAsync(
                "qris", new UpdatePaymentMethodRequest(SortOrder: -1)));
    }

    [Fact]
    public async Task UpdateMethod_UnknownCode_ReturnsNull()
    {
        await using var db = await CreateDbAsync();

        // "ovo" ada di katalog (nonaktif) — code tak dikenal pakai yang lain.
        var result = await CreateService(db).UpdateMethodAsync(
            "kredivo", new UpdatePaymentMethodRequest(Enabled: true));

        Assert.Null(result);
    }

    // ── Update bank ──

    [Fact]
    public async Task UpdateBank_PersistsIconUrlAndEnablesMandiri()
    {
        await using var db = await CreateDbAsync();
        var service = CreateService(db);

        var updated = await service.UpdateBankAsync("bank_transfer", "mandiri",
            new UpdatePaymentBankRequest(
                IconUrl: "https://digitalin.kyuzenstudio.it.com/payment/mandiri.png",
                Enabled: true));

        Assert.NotNull(updated);
        Assert.True(updated!.Enabled);
        Assert.Equal("https://digitalin.kyuzenstudio.it.com/payment/mandiri.png", updated.IconUrl);
    }

    [Fact]
    public async Task UpdateBank_UnknownMethodOrBank_ReturnsNull()
    {
        await using var db = await CreateDbAsync();
        var service = CreateService(db);

        Assert.Null(await service.UpdateBankAsync(
            "ovo", "bca", new UpdatePaymentBankRequest(Enabled: true)));
        Assert.Null(await service.UpdateBankAsync(
            "bank_transfer", "bukopin", new UpdatePaymentBankRequest(Enabled: true)));
    }

    [Fact]
    public async Task UpdateBank_InvalidUrl_Rejected()
    {
        await using var db = await CreateDbAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(db).UpdateBankAsync(
                "bank_transfer", "bca", new UpdatePaymentBankRequest(IconUrl: "javascript:alert(1)")));
    }

    // ── Upload ikon (Cloudflare R2) ──

    [Fact]
    public async Task UploadMethodIcon_PersistsPublicUrl()
    {
        await using var db = await CreateDbAsync();
        var r2 = new FakeR2Provider();
        var file = new ImageFileData("qris.png", "image/png", new MemoryStream(PngBytes));

        var url = await CreateService(db, r2).UploadMethodIconAsync("qris", file);

        Assert.NotNull(url);
        Assert.StartsWith("https://r2.test/payment-icons/", url);
        var row = await db.PaymentMethods.AsNoTracking().SingleAsync(m => m.Code == "qris");
        Assert.Equal(url, row.IconUrl);
    }

    [Fact]
    public async Task UploadMethodIcon_ReplacesOldIcon_DeletesFromR2()
    {
        await using var db = await CreateDbAsync();
        var r2 = new FakeR2Provider();
        var service = CreateService(db, r2);

        var first = await service.UploadMethodIconAsync(
            "qris", new ImageFileData("a.png", "image/png", new MemoryStream(PngBytes)));
        var second = await service.UploadMethodIconAsync(
            "qris", new ImageFileData("b.png", "image/png", new MemoryStream(PngBytes)));

        Assert.NotEqual(first, second);
        Assert.Equal(2, r2.Uploads.Count);
        // Ikon lama wajib dihapus dari R2 — bukan jadi orphan.
        Assert.Contains(first!, r2.DeletedUrls);
    }

    [Fact]
    public async Task UploadBankIcon_PersistsPublicUrl()
    {
        await using var db = await CreateDbAsync();
        var file = new ImageFileData("bca.png", "image/png", new MemoryStream(PngBytes));

        var url = await CreateService(db).UploadBankIconAsync("bank_transfer", "bca", file);

        Assert.NotNull(url);
        var row = await db.PaymentBanks.AsNoTracking()
            .SingleAsync(b => b.PaymentMethodCode == "bank_transfer" && b.Code == "bca");
        Assert.Equal(url, row.IconUrl);
    }

    [Fact]
    public async Task UploadIcon_FakeImageContent_Rejected()
    {
        await using var db = await CreateDbAsync();
        // Ekstensi .png tapi isi bukan gambar — signature byte wajib menolak.
        var file = new ImageFileData("fake.png", "image/png",
            new MemoryStream("ini bukan gambar"u8.ToArray()));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(db).UploadMethodIconAsync("qris", file));

        var row = await db.PaymentMethods.AsNoTracking().SingleAsync(m => m.Code == "qris");
        Assert.Null(row.IconUrl); // tidak ada perubahan tersimpan
    }

    [Fact]
    public async Task UploadIcon_UnknownCode_ReturnsNull()
    {
        await using var db = await CreateDbAsync();
        var file = new ImageFileData("qris.png", "image/png", new MemoryStream(PngBytes));

        Assert.Null(await CreateService(db).UploadMethodIconAsync("kredivo", file));
        Assert.Null(await CreateService(db).UploadBankIconAsync("bank_transfer", "bukopin", file));
    }
}
