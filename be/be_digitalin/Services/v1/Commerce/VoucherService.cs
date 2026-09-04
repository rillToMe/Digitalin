using be_digitalin.Data;
using be_digitalin.DTOs.Requests;
using be_digitalin.DTOs.Responses;
using be_digitalin.Models;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services.Commerce;

public class VoucherService : IVoucherService
{
    private readonly DigitalinDbContext _db;

    public VoucherService(DigitalinDbContext db)
    {
        _db = db;
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static string ComputeStatus(Voucher v)
    {
        if (!v.IsActive) return "Disabled";
        if (v.ExpiredAt <= DateTime.UtcNow) return "Expired";
        if (v.UsedCount >= v.UsageLimit) return "QuotaExceeded";
        return "Active";
    }

    private static AdminVoucherItem ToDto(Voucher v) => new(
        v.Id, v.Code, v.DiscountType, v.DiscountValue,
        v.MinimumTransaction, v.MaximumDiscount,
        v.UsageLimit, v.UsedCount, v.ExpiredAt,
        v.IsActive, ComputeStatus(v), v.CreatedAt, v.UpdatedAt);

    private static void ValidatePayload(string code, string discountType,
        decimal discountValue, int usageLimit, DateTime expiredAt, int? existingUsedCount = null)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length < 3 || code.Length > 50)
            throw new InvalidOperationException("Kode voucher harus antara 3 dan 50 karakter.");

        if (discountType != "Percentage" && discountType != "FixedAmount")
            throw new InvalidOperationException("Tipe diskon tidak valid. Pilih: Percentage atau FixedAmount.");

        if (discountValue <= 0)
            throw new InvalidOperationException("Nilai diskon harus lebih besar dari 0.");

        if (discountType == "Percentage" && discountValue > 100)
            throw new InvalidOperationException("Diskon persentase tidak boleh lebih dari 100%.");

        if (expiredAt <= DateTime.UtcNow)
            throw new InvalidOperationException("Tanggal kedaluwarsa harus di masa depan.");

        if (usageLimit < 1)
            throw new InvalidOperationException("Batas penggunaan harus minimal 1.");

        if (existingUsedCount.HasValue && usageLimit < existingUsedCount.Value)
            throw new InvalidOperationException(
                $"Batas penggunaan tidak boleh kurang dari jumlah yang sudah digunakan ({existingUsedCount.Value}).");
    }

    // ── GET /admin/vouchers ─────────────────────────────────────

    public async Task<AdminVoucherListResponse> GetVouchersAsync(
        string? search, string? status, string? sortBy, string? sortOrder,
        int page, int limit)
    {
        var query = _db.Vouchers.AsNoTracking().AsQueryable();

        // Search by code — Code selalu tersimpan uppercase dan input
        // dinormalisasi uppercase, jadi equality/Contains tanpa UPPER().
        if (!string.IsNullOrWhiteSpace(search))
        {
            string upperSearch = search.ToUpper();
            query = query.Where(v => v.Code.Contains(upperSearch));
        }

        // Computed status diekspresikan sebagai predicate DB-side. Urutan
        // kondisi mereplikasi ComputeStatus: Disabled > Expired > QuotaExceeded.
        if (!string.IsNullOrWhiteSpace(status))
        {
            var now = DateTime.UtcNow;
            query = status.ToLowerInvariant() switch
            {
                "active" => query.Where(v => v.IsActive && v.ExpiredAt > now && v.UsedCount < v.UsageLimit),
                "disabled" => query.Where(v => !v.IsActive),
                "expired" => query.Where(v => v.IsActive && v.ExpiredAt <= now),
                "quotaexceeded" => query.Where(v => v.IsActive && v.ExpiredAt > now && v.UsedCount >= v.UsageLimit),
                _ => query
            };
        }

        // Sort
        bool ascending = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);
        query = (sortBy?.ToLower()) switch
        {
            "code" => ascending
                ? query.OrderBy(v => v.Code)
                : query.OrderByDescending(v => v.Code),
            "expiredat" => ascending
                ? query.OrderBy(v => v.ExpiredAt)
                : query.OrderByDescending(v => v.ExpiredAt),
            "discountvalue" => ascending
                ? query.OrderBy(v => v.DiscountValue)
                : query.OrderByDescending(v => v.DiscountValue),
            _ => ascending // default: createdAt
                ? query.OrderBy(v => v.CreatedAt)
                : query.OrderByDescending(v => v.CreatedAt)
        };

        int totalCount = await query.CountAsync();
        int totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)limit);
        int currentPage = page;

        var items = (await query
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync())
            .Select(ToDto)
            .ToList();

        return new AdminVoucherListResponse(items, totalCount, currentPage, totalPages);
    }

    // ── GET /admin/vouchers/{id} ────────────────────────────────

    public async Task<AdminVoucherItem?> GetVoucherByIdAsync(int id)
    {
        var voucher = await _db.Vouchers.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id);
        return voucher == null ? null : ToDto(voucher);
    }

    // ── POST /admin/vouchers ────────────────────────────────────

    public async Task<AdminVoucherItem> CreateVoucherAsync(CreateVoucherRequest request)
    {
        ValidatePayload(request.Code, request.DiscountType, request.DiscountValue,
            request.UsageLimit, request.ExpiredAt);

        // Check uniqueness (case-insensitive)
        string upperCode = request.Code.ToUpper();
        bool exists = await _db.Vouchers.AnyAsync(v => v.Code == upperCode);
        if (exists)
            throw new InvalidOperationException("Kode voucher sudah digunakan.");

        var voucher = new Voucher
        {
            Code = request.Code.Trim().ToUpper(),
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            MinimumTransaction = request.MinimumTransaction,
            MaximumDiscount = request.MaximumDiscount,
            UsageLimit = request.UsageLimit,
            UsedCount = 0,
            ExpiredAt = request.ExpiredAt,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Vouchers.Add(voucher);
        await _db.SaveChangesAsync();

        return ToDto(voucher);
    }

    // ── PUT /admin/vouchers/{id} ────────────────────────────────

    public async Task<AdminVoucherItem?> UpdateVoucherAsync(int id, UpdateVoucherRequest request)
    {
        var voucher = await _db.Vouchers.FindAsync(id);
        if (voucher == null) return null;

        ValidatePayload(request.Code, request.DiscountType, request.DiscountValue,
            request.UsageLimit, request.ExpiredAt, voucher.UsedCount);

        // Check code uniqueness (excluding self)
        string upperCode = request.Code.ToUpper();
        bool codeTaken = await _db.Vouchers.AnyAsync(v => v.Id != id && v.Code == upperCode);
        if (codeTaken)
            throw new InvalidOperationException("Kode voucher sudah digunakan.");

        voucher.Code = request.Code.Trim().ToUpper();
        voucher.DiscountType = request.DiscountType;
        voucher.DiscountValue = request.DiscountValue;
        voucher.MinimumTransaction = request.MinimumTransaction;
        voucher.MaximumDiscount = request.MaximumDiscount;
        voucher.UsageLimit = request.UsageLimit;
        voucher.ExpiredAt = request.ExpiredAt;
        voucher.IsActive = request.IsActive;
        voucher.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return ToDto(voucher);
    }

    // ── PATCH /admin/vouchers/{id}/status ───────────────────────

    public async Task<bool?> UpdateVoucherStatusAsync(int id, UpdateVoucherStatusRequest request)
    {
        var voucher = await _db.Vouchers.FindAsync(id);
        if (voucher == null) return null;

        voucher.IsActive = request.IsActive;
        voucher.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return voucher.IsActive;
    }

    // ── DELETE /admin/vouchers/{id} ─────────────────────────────

    public async Task<(bool Success, string? ErrorMessage)> DeleteVoucherAsync(int id)
    {
        var voucher = await _db.Vouchers.FindAsync(id);
        if (voucher == null)
            return (false, "Voucher tidak ditemukan.");

        if (voucher.UsedCount > 0)
            return (false,
                $"Voucher tidak dapat dihapus karena sudah pernah digunakan ({voucher.UsedCount} transaksi). Nonaktifkan voucher sebagai gantinya.");

        _db.Vouchers.Remove(voucher);
        await _db.SaveChangesAsync();

        return (true, null);
    }

    // ── GET /admin/vouchers/{id}/usage ──────────────────────────

    public async Task<VoucherUsageResponse> GetVoucherUsageAsync(int id, int page, int limit)
    {
        bool voucherExists = await _db.Vouchers.AsNoTracking().AnyAsync(v => v.Id == id);
        if (!voucherExists)
            throw new InvalidOperationException("Voucher tidak ditemukan.");

        // Baca dari snapshot — tanpa join Product
        var query = _db.TopupOrders
            .AsNoTracking()
            .Include(o => o.Customer)
        .Where(o => o.VoucherCode != null && o.VoucherCode ==
            _db.Vouchers.AsNoTracking().Where(v => v.Id == id).Select(v => v.Code).FirstOrDefault())
            .OrderByDescending(o => o.OrderDate);

        int totalCount = await query.CountAsync();
        int totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)limit);

        var items = await query
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(o => new VoucherUsageInfo(
                o.Id,
                o.OrderNo,
                o.Customer != null ? o.Customer.FullName : null,
                o.ProductName,
                o.OriginalPrice,
                o.DiscountAmount,
                o.TotalPaid,
                o.OrderDate,
                o.Status
            ))
            .ToListAsync();

        return new VoucherUsageResponse(items, totalCount, page, totalPages);
    }

    // ── Shared Voucher Logic (used by Preview & Create Order) ─────

    public async Task<(Voucher? Voucher, string? Error)> ValidateVoucherAsync(string code, decimal transactionAmount)
    {
        var voucher = await _db.Vouchers
            .FirstOrDefaultAsync(v => v.Code == code.ToUpper());

        if (voucher == null)
            return (null, "Kode voucher tidak ditemukan.");

        if (!voucher.IsActive)
            return (null, "Voucher ini sudah tidak aktif.");

        if (DateTime.UtcNow > voucher.ExpiredAt)
            return (null, "Voucher ini sudah kedaluwarsa.");

        if (voucher.UsedCount >= voucher.UsageLimit)
            return (null, "Kuota voucher ini sudah habis.");

        if (transactionAmount < voucher.MinimumTransaction)
            return (null, $"Voucher ini membutuhkan minimal transaksi sebesar Rp{voucher.MinimumTransaction:N0}.");

        return (voucher, null);
    }

    public decimal CalculateDiscount(Voucher voucher, decimal productPrice)
    {
        decimal discountAmount;

        if (voucher.DiscountType == "Percentage")
        {
            discountAmount = productPrice * (voucher.DiscountValue / 100m);
            if (voucher.MaximumDiscount.HasValue && discountAmount > voucher.MaximumDiscount.Value)
            {
                discountAmount = voucher.MaximumDiscount.Value;
            }
        }
        else // FixedAmount
        {
            discountAmount = voucher.DiscountValue;
        }

        // Discount cannot exceed product price
        if (discountAmount > productPrice)
            discountAmount = productPrice;

        return discountAmount;
    }

    public async Task ReserveVoucherAsync(Voucher voucher)
    {
        // Increment atomik berkondisi: kuota hanya naik bila masih tersedia.
        // Predikat UsedCount < UsageLimit dievaluasi di DB, sehingga dua request
        // paralel tidak bisa saling menimpa (no lost update / oversubscription).
        var affected = await _db.Vouchers
            .Where(v => v.Id == voucher.Id && v.UsedCount < v.UsageLimit)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(v => v.UsedCount, v => v.UsedCount + 1)
                .SetProperty(v => v.UpdatedAt, DateTime.UtcNow));

        if (affected == 0)
            throw new InvalidOperationException("Kuota voucher ini sudah habis (persaingan bersamaan).");
    }

    public async Task ReleaseVoucherAsync(string voucherCode)
    {
        // Decrement atomik berkondisi: kuota hanya turun bila UsedCount > 0,
        // sehingga aman dari underflow bila terpanggil lebih dari sekali.
        // Matching case-insensitive by Code, konsisten dengan ValidateVoucherAsync.
        string upperCode = voucherCode.ToUpper();

        await _db.Vouchers
            .Where(v => v.Code == upperCode && v.UsedCount > 0)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(v => v.UsedCount, v => v.UsedCount - 1)
                .SetProperty(v => v.UpdatedAt, DateTime.UtcNow));
    }
}
