using be_digitalin.Application.Ports;
using be_digitalin.Application.Validation;
using be_digitalin.Constants;
using be_digitalin.Domain.Entities;
using be_digitalin.DTOs;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services.Payments;

/// <summary>
/// Lihat IPaymentMethodService. Validasi: IconUrl wajib URL http(s) absolut
/// bila diisi; DisplayName tidak boleh kosong bila diisi; SortOrder >= 0.
/// Ikon dari upload disimpan di Cloudflare R2 (folder payment-icons) —
/// bukan binary di server.
/// </summary>
public class PaymentMethodService : IPaymentMethodService
{
    private readonly IApplicationDbContext _db;
    private readonly ICloudflareR2Provider _r2Provider;

    public PaymentMethodService(IApplicationDbContext db, ICloudflareR2Provider r2Provider)
    {
        _db = db;
        _r2Provider = r2Provider;
    }

    public async Task<IReadOnlyList<PaymentMethodResponse>> GetEnabledForCheckoutAsync(
        CancellationToken cancellationToken = default)
    {
        var methods = await _db.PaymentMethods
            .AsNoTracking()
            .Where(m => m.Enabled)
            .OrderBy(m => m.SortOrder)
            .Select(m => new
            {
                m.Code, Name = m.DisplayName, m.Description, m.IconUrl,
                Banks = m.Banks
                    .Where(b => b.Enabled)
                    .OrderBy(b => b.SortOrder)
                    .Select(b => new PaymentBankResponse(b.Code, b.DisplayName, b.IconUrl))
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return methods
            .Select(m => new PaymentMethodResponse(m.Code, m.Name, m.Description, m.IconUrl, m.Banks))
            .ToList();
    }

    public async Task<IReadOnlyList<AdminPaymentMethodResponse>> GetAllForAdminAsync(
        CancellationToken cancellationToken = default)
    {
        var methods = await _db.PaymentMethods
            .AsNoTracking()
            .OrderBy(m => m.SortOrder)
            .Select(m => new
            {
                m.Code, Name = m.DisplayName, m.Description, m.IconUrl, m.Enabled, m.SortOrder,
                Banks = m.Banks
                    .OrderBy(b => b.SortOrder)
                    .Select(b => new AdminPaymentBankResponse(
                        b.Code, b.DisplayName, b.IconUrl, b.Enabled, b.SortOrder))
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return methods
            .Select(m => new AdminPaymentMethodResponse(
                m.Code, m.Name, m.Description, m.IconUrl, m.Enabled, m.SortOrder, m.Banks))
            .ToList();
    }

    public async Task<AdminPaymentMethodResponse?> UpdateMethodAsync(
        string code,
        UpdatePaymentMethodRequest request,
        CancellationToken cancellationToken = default)
    {
        var method = await _db.PaymentMethods
            .FirstOrDefaultAsync(m => m.Code == code, cancellationToken);

        if (method is null) return null;

        if (request.DisplayName is not null)
        {
            if (string.IsNullOrWhiteSpace(request.DisplayName))
                throw new InvalidOperationException("Nama metode pembayaran tidak boleh kosong.");
            method.DisplayName = request.DisplayName.Trim();
        }

        if (request.IconUrl is not null)
            method.IconUrl = NormalizeIconUrl(request.IconUrl);

        if (request.Enabled.HasValue)
            method.Enabled = request.Enabled.Value;

        if (request.SortOrder.HasValue)
        {
            if (request.SortOrder.Value < 0)
                throw new InvalidOperationException("Urutan tidak boleh negatif.");
            method.SortOrder = request.SortOrder.Value;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var banks = await _db.PaymentBanks.AsNoTracking()
            .Where(b => b.PaymentMethodCode == method.Code)
            .OrderBy(b => b.SortOrder)
            .Select(b => new AdminPaymentBankResponse(
                b.Code, b.DisplayName, b.IconUrl, b.Enabled, b.SortOrder))
            .ToListAsync(cancellationToken);

        return new AdminPaymentMethodResponse(
            method.Code, method.DisplayName, method.Description,
            method.IconUrl, method.Enabled, method.SortOrder, banks);
    }

    public async Task<AdminPaymentBankResponse?> UpdateBankAsync(
        string methodCode,
        string bankCode,
        UpdatePaymentBankRequest request,
        CancellationToken cancellationToken = default)
    {
        var bank = await _db.PaymentBanks
            .FirstOrDefaultAsync(
                b => b.PaymentMethodCode == methodCode && b.Code == bankCode,
                cancellationToken);

        if (bank is null) return null;

        if (request.DisplayName is not null)
        {
            if (string.IsNullOrWhiteSpace(request.DisplayName))
                throw new InvalidOperationException("Nama bank tidak boleh kosong.");
            bank.DisplayName = request.DisplayName.Trim();
        }

        if (request.IconUrl is not null)
            bank.IconUrl = NormalizeIconUrl(request.IconUrl);

        if (request.Enabled.HasValue)
            bank.Enabled = request.Enabled.Value;

        if (request.SortOrder.HasValue)
        {
            if (request.SortOrder.Value < 0)
                throw new InvalidOperationException("Urutan tidak boleh negatif.");
            bank.SortOrder = request.SortOrder.Value;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new AdminPaymentBankResponse(
            bank.Code, bank.DisplayName, bank.IconUrl, bank.Enabled, bank.SortOrder);
    }

    // Ikon hanya boleh URL publik http(s) — bukan binary/relatif/file lokal.
    private static string? NormalizeIconUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "URL ikon tidak valid — wajib URL http(s) publik (mis. hasil upload Cloudflare R2).");
        }

        return trimmed;
    }

    // ── Upload ikon via Cloudflare R2 (pola yang sama dengan ikon produk) ──
    // Validasi signature byte → hapus ikon lama dari R2 → upload baru →
    // simpan URL publik. Binary tidak pernah disentuh server selain stream.

    public async Task<string?> UploadMethodIconAsync(
        string code,
        ImageFileData file,
        CancellationToken cancellationToken = default)
    {
        var method = await _db.PaymentMethods
            .FirstOrDefaultAsync(m => m.Code == code, cancellationToken);

        if (method is null) return null;

        await ImageFileValidator.EnsureValidImageAsync(file, cancellationToken);
        method.IconUrl = await ReplaceIconAsync(method.IconUrl, file, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return method.IconUrl;
    }

    public async Task<string?> UploadBankIconAsync(
        string methodCode,
        string bankCode,
        ImageFileData file,
        CancellationToken cancellationToken = default)
    {
        var bank = await _db.PaymentBanks
            .FirstOrDefaultAsync(
                b => b.PaymentMethodCode == methodCode && b.Code == bankCode,
                cancellationToken);

        if (bank is null) return null;

        await ImageFileValidator.EnsureValidImageAsync(file, cancellationToken);
        bank.IconUrl = await ReplaceIconAsync(bank.IconUrl, file, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return bank.IconUrl;
    }

    private async Task<string> ReplaceIconAsync(
        string? oldUrl,
        ImageFileData file,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(oldUrl))
        {
            await _r2Provider.DeleteFileAsync(oldUrl, cancellationToken);
        }

        return await _r2Provider.UploadFileAsync(
            file, AppConstants.StorageFolders.PaymentIcons, cancellationToken);
    }
}
