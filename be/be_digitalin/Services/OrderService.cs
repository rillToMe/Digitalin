using be_digitalin.Data;
using be_digitalin.DTOs;
using be_digitalin.Models;
using be_digitalin.Providers; // <-- Panggil folder Providers
using be_digitalin.Constants;
using Microsoft.EntityFrameworkCore;
using System.Transactions;

namespace be_digitalin.Services;

public class OrderService : IOrderService
{
    private readonly DigitalinDbContext _db;
    private readonly MidtransProvider _midtransProvider;
    private readonly DigiflazzProvider _digiflazzProvider;
    private readonly TelegramProvider _telegramProvider;
    private readonly ILogger<OrderService> _logger;

    // Suntikkan Provider baru kita!
    public OrderService(
        DigitalinDbContext db,
        MidtransProvider midtransProvider,
        DigiflazzProvider digiflazzProvider,
        TelegramProvider telegramProvider,
        ILogger<OrderService> logger) // <-- Tambahkan ini
    {
        _db = db;
        _midtransProvider = midtransProvider;
        _digiflazzProvider = digiflazzProvider;
        _telegramProvider = telegramProvider; // <-- Tambahkan ini
        _logger = logger;
    }

    public async Task<OrderResponse> PlaceOrderAsync(CreateOrderRequest request, int? customerId = null)
    {
        string fullPlayerId = request.TargetId;
        if (!string.IsNullOrWhiteSpace(request.ZoneId))
        {
            fullPlayerId = $"{request.TargetId}{request.ZoneId}"; 
        }

        var product = await _db.GameProducts.FindAsync(request.ProductId);
        if (product == null) throw new InvalidOperationException("Produk tidak ditemukan!");

        // ── [M4] Voucher Validation & Discount Calculation ─────────────────
        decimal discountAmount = 0m;
        Voucher? voucher = null;

        if (!string.IsNullOrWhiteSpace(request.VoucherCode))
        {
            voucher = await ValidateAndReserveVoucherAsync(request.VoucherCode, product.Price);
            // Calculate discount: percentage-based, capped by MaxDiscountAmount
            discountAmount = product.Price * (voucher.DiscountPercentage / 100m);
            if (voucher.MaxDiscountAmount.HasValue && discountAmount > voucher.MaxDiscountAmount.Value)
            {
                discountAmount = voucher.MaxDiscountAmount.Value;
            }
            // Floor the discount to the product price (can't discount more than the price itself)
            if (discountAmount > product.Price)
            {
                discountAmount = product.Price;
            }
        }

        decimal finalPrice = product.Price - discountAmount;
        // Midtrans requires gross_amount > 0
        if (finalPrice <= 0)
            throw new InvalidOperationException("Harga akhir setelah diskon tidak boleh nol atau negatif.");

        string generatedOrderNo = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";

        var order = new TopupOrder
        {
            OrderNo = generatedOrderNo,
            PlayerId = fullPlayerId,
            GameProductId = request.ProductId,
            Status = AppConstants.OrderStatus.Pending,
            OrderDate = DateTime.UtcNow,
            CustomerId = customerId,
            VoucherCode = request.VoucherCode,
            DiscountAmount = discountAmount
        };

        _db.TopupOrders.Add(order);
        await _db.SaveChangesAsync();

        // Pass the DISCOUNTED price to Midtrans so the customer pays the correct amount
        var (token, redirectUrl) = await _midtransProvider.CreateSnapTransactionAsync(
            order.OrderNo, finalPrice, product.Id.ToString(), product.Name, fullPlayerId);

        order.SnapToken = token;
        order.RedirectUrl = redirectUrl;
        await _db.SaveChangesAsync();

        return new OrderResponse(
            order.Id, order.OrderNo, order.PlayerId, order.Status, product.Name, product.Price, 
            order.SnapToken, order.RedirectUrl, order.OrderDate, order.Sn, product.ImageUrl,
            discountAmount, finalPrice
        );
    }

    /// <summary>
    /// [M4] Validates a voucher code and reserves one quota unit atomically.
    /// Throws InvalidOperationException (returned as 400 by the endpoint) on any failure.
    /// Uses optimistic concurrency to prevent double-spend under race conditions.
    /// </summary>
    private async Task<Voucher> ValidateAndReserveVoucherAsync(string code, decimal transactionAmount)
    {
        var voucher = await _db.Vouchers
            .FirstOrDefaultAsync(v => v.Code.ToUpper() == code.ToUpper());

        if (voucher == null)
            throw new InvalidOperationException("Kode voucher tidak ditemukan.");

        if (!voucher.IsActive)
            throw new InvalidOperationException("Voucher ini sudah tidak aktif.");

        if (DateTime.UtcNow > voucher.ValidUntil)
            throw new InvalidOperationException("Voucher ini sudah kedaluwarsa.");

        if (voucher.Quota <= 0)
            throw new InvalidOperationException("Kuota voucher ini sudah habis.");

        if (transactionAmount < voucher.MinTransactionAmount)
            throw new InvalidOperationException(
                $"Voucher ini membutuhkan minimal transaksi sebesar Rp{voucher.MinTransactionAmount:N0}.");

        // ── Atomic quota decrement with optimistic concurrency ─────────────
        // Decrement Quota and save. If another request modified the same voucher
        // concurrently (e.g., two customers using the last code simultaneously),
        // EF Core will throw DbUpdateConcurrencyException.
        voucher.Quota -= 1;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Reload the latest voucher state and check if quota is still available
            await _db.Entry(voucher).ReloadAsync();
            if (voucher.Quota <= 0)
                throw new InvalidOperationException("Kuota voucher ini sudah habis (persaingan bersamaan).");

            // Retry the decrement once
            voucher.Quota -= 1;
            await _db.SaveChangesAsync();
        }

        return voucher;
    }

    // 1. Fungsi Serbaguna Baru (Bisa untuk Admin, bisa untuk riwayat Customer)
    public async Task<PagedResult<OrderResponse>> GetOrdersAsync(int? customerId = null, int page = 1, int limit = 20, string? status = null)
    {
        var query = _db.TopupOrders.Include(o => o.GameProduct).AsQueryable();

        // Filter berdasarkan Customer (Jika ada)
        if (customerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == customerId.Value);
        }

        // Filter berdasarkan Status (Jika Admin ingin melihat yang Nyangkut/Sukses saja)
        if (!string.IsNullOrWhiteSpace(status))
        {
            string upperStatus = status.ToUpper();
            query = query.Where(o => o.Status == upperStatus);
        }

        // Hitung total data SEBELUM dipotong pagination (untuk info Total Halaman)
        int totalItems = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalItems / (double)limit);

        // Eksekusi data dengan rumus Pagination (Skip & Take)
        var data = await query
            .OrderByDescending(o => o.OrderDate)
            .Skip((page - 1) * limit) // Lewati data di halaman sebelumnya
            .Take(limit)              // Ambil data untuk halaman ini
            .Select(o => new OrderResponse(
                o.Id, o.OrderNo ?? "-", o.PlayerId, o.Status, o.GameProduct!.Name, 
                o.GameProduct.Price, o.SnapToken, o.RedirectUrl, o.OrderDate, o.Sn,
                o.GameProduct.ImageUrl, o.DiscountAmount, o.GameProduct.Price - o.DiscountAmount))
            .ToListAsync();

        return new PagedResult<OrderResponse>(data, totalItems, totalPages, page);
    }

    // 2. Fungsi lama milik Admin tinggal numpang panggil aja
    public async Task<PagedResult<OrderResponse>> GetAllOrdersAsync(int page = 1, int limit = 20, string? status = null)
    {
        return await GetOrdersAsync(null, page, limit, status); 
    }

    /// <summary>
    /// Handles Midtrans payment webhooks with:
    /// [C1] Idempotency guard  — duplicate webhooks are ignored via ProcessedAt + status check.
    /// [C2] Optimistic concurrency — DbUpdateConcurrencyException caught and logged safely.
    /// [C3] Digiflazz timeout protection — explicit 30s timeout via CancellationTokenSource.
    /// </summary>
    public async Task HandleNotificationAsync(MidtransNotificationRequest notification)
    {
        // ── Step 1: Validate the Midtrans signature ──────────────────────────
        if (!_midtransProvider.ValidateSignature(
                notification.order_id,
                notification.status_code,
                notification.gross_amount,
                notification.signature_key))
        {
            throw new Exception("Signature key tidak valid! Data dicurigai palsu.");
        }

        // ── Step 2: Fetch the order ──────────────────────────────────────────
        var order = await _db.TopupOrders
            .Include(o => o.GameProduct)
            .FirstOrDefaultAsync(o => o.OrderNo == notification.order_id);

        if (order == null) return;

        // ── [C1] IDEMPOTENCY GUARD ──────────────────────────────────────────
        // If the order has already left PENDING state (either fully processed by
        // a previous webhook or already in a terminal state), exit immediately.
        // This prevents Midtrans retries from triggering double fulfillment.
        if (order.Status != AppConstants.OrderStatus.Pending)
        {
            _logger.LogWarning(
                "[WEBHOOK] Duplicate/late webhook ignored for Order {OrderNo}. Current status: {Status}",
                order.OrderNo, order.Status);
            return;
        }

        // ── Step 3: Route by Midtrans transaction_status ─────────────────────
        if (notification.transaction_status == AppConstants.MidtransStatus.Expire)
        {
            order.Status = AppConstants.OrderStatus.Expired;
            await SafeSaveChangesAsync(order.OrderNo);
            return;
        }

        if (notification.transaction_status == AppConstants.MidtransStatus.Cancel ||
            notification.transaction_status == AppConstants.MidtransStatus.Deny)
        {
            order.Status = AppConstants.OrderStatus.Failed;
            await SafeSaveChangesAsync(order.OrderNo);
            return;
        }

        // ── Step 4: Successful payment — settlement / capture + fraud accept ─
        if (notification.transaction_status != AppConstants.MidtransStatus.Settlement &&
            notification.transaction_status != AppConstants.MidtransStatus.Capture)
        {
            return; // Unrecognised status — do nothing
        }

        if (notification.fraud_status != AppConstants.MidtransFraud.Accept) return;

        // ── [C2] Wrap the critical payment→fulfillment pipeline in a
        //         database transaction to guarantee atomicity. ────────────────
        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // Mark order as PAID and stamp ProcessedAt as the idempotency marker.
            order.Status = AppConstants.OrderStatus.Paid;
            order.ProcessedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            try
            {
                // ── [C3] Digiflazz call with explicit 30-second timeout ─────
                // If Digiflazz does not respond in time, we catch the
                // OperationCanceledException and set NEEDS_ACTION instead of
                // leaving the order in a permanent PAID limbo.
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                string? sn = await _digiflazzProvider.TopupAsync(
                    order.GameProduct!.BuyerSkuCode,
                    order.PlayerId,
                    order.OrderNo,
                    cts.Token);

                // Digiflazz responded — advance to PROCESSING
                order.Status = AppConstants.OrderStatus.Processing;

                // Instant SN (rare but possible)
                if (!string.IsNullOrEmpty(sn))
                {
                    order.Sn = sn;
                    order.Status = AppConstants.OrderStatus.Completed;
                    _logger.LogInformation("[WEBHOOK] Instant SN received for Order {OrderNo}: {SN}", order.OrderNo, sn);
                }
            }
            catch (OperationCanceledException)
            {
                // [C3] Digiflazz timed out — order stays PAID; sweep worker or
                // admin retry will resolve it after verifying upstream status.
                _logger.LogWarning(
                    "[WEBHOOK] Digiflazz TIMEOUT for Order {OrderNo}. Order stays PAID, pending sweep.",
                    order.OrderNo);
                order.Status = AppConstants.OrderStatus.NeedsAction;

                await SendStuckOrderAlertAsync(order, "Digiflazz API timeout (30s).");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex,
                    "[WEBHOOK] Digiflazz call FAILED for Order {OrderNo}", order.OrderNo);
                order.Status = AppConstants.OrderStatus.NeedsAction;
                await SendStuckOrderAlertAsync(order, ex.Message);
            }

            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // [C2] Another concurrent process (sweep worker / admin retry /
            // duplicate webhook) already modified this row. Back off safely.
            await dbTransaction.RollbackAsync();
            _logger.LogWarning(
                "[WEBHOOK] Concurrency conflict on Order {OrderNo}. Another process already handled it.",
                order.OrderNo);
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "[WEBHOOK] Unexpected error processing Order {OrderNo}", order.OrderNo);
            throw;
        }
    }

    // ── Private Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Saves changes with optimistic concurrency protection.
    /// If another process modified the same row concurrently, the exception
    /// is caught and logged instead of crashing the caller.
    /// </summary>
    private async Task SafeSaveChangesAsync(string orderNo)
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning(
                "[SaveChanges] Concurrency conflict on Order {OrderNo}. Change was discarded.",
                orderNo);
        }
    }

    /// <summary>
    /// Sends a Telegram alert when a paid order cannot be fulfilled automatically.
    /// </summary>
    private async Task SendStuckOrderAlertAsync(TopupOrder order, string reason)
    {
        string alertMessage = $@"⚠️ <b>PESANAN TERTUNDA (Dana Sudah Masuk)</b> ⚠️

Halo Admin, ada pesanan yang pembayarannya sudah berhasil diterima, tapi pengiriman item ke pembeli tertunda karena kendala di sistem pusat (Digiflazz).

<b>📝 Detail Pesanan:</b>
• <b>Invoice:</b> {order.OrderNo}
• <b>Produk:</b> {order.GameProduct!.Name}
• <b>ID Tujuan:</b> {order.PlayerId}
• <b>Keterangan:</b> {reason}

<b>🔧 Apa yang harus dilakukan?</b>
1. Pastikan saldo Digiflazz Anda cukup.
2. Buka Admin Panel, cari pesanan ini, lalu klik tombol <b>Retry</b>.
3. Jika produk memang sedang kosong/gangguan, hubungi pembeli untuk melakukan <b>Refund</b> (Pengembalian Dana).";

        try
        {
            await _telegramProvider.SendNotificationAsync(alertMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Telegram] Failed to send stuck-order alert for {OrderNo}", order.OrderNo);
        }
    }
}
