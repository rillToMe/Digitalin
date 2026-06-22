using be_digitalin.Data;
using be_digitalin.DTOs;
using be_digitalin.Models;
using be_digitalin.Providers;
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
    private readonly IVoucherService _voucherService;
    private readonly ILogger<OrderService> _logger;
    
    public OrderService(
        DigitalinDbContext db,
        MidtransProvider midtransProvider,
        DigiflazzProvider digiflazzProvider,
        TelegramProvider telegramProvider,
        IVoucherService voucherService,
        ILogger<OrderService> logger) 
    {
        _db = db;
        _midtransProvider = midtransProvider;
        _digiflazzProvider = digiflazzProvider;
        _telegramProvider = telegramProvider;
        _voucherService = voucherService;
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

        // Voucher validasi & Diskon validasi (menggunakan shared logic dari VoucherService)
        decimal discountAmount = 0m;
        Voucher? voucher = null;

        if (!string.IsNullOrWhiteSpace(request.VoucherCode))
        {
            var (foundVoucher, error) = await _voucherService.ValidateVoucherAsync(
                request.VoucherCode, product.Price);

            if (error != null)
                throw new InvalidOperationException(error);

            voucher = foundVoucher!;
            discountAmount = _voucherService.CalculateDiscount(voucher, product.Price);
        }

        decimal finalPrice = product.Price - discountAmount;
        
        if (finalPrice <= 0)
            throw new InvalidOperationException("Harga akhir setelah diskon tidak boleh nol atau negatif.");

        // Hitung snapshot modal & profit
        decimal profitAmount = product.CustomMargin ?? 0m;
        decimal costPrice = product.Price - profitAmount;

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
            DiscountAmount = discountAmount,

            // Transaction Snapshot — immutable setelah order dibuat
            ProductName = product.Name,
            BuyerSkuCode = product.BuyerSkuCode,
            BrandName = product.Brand,
            CategoryName = product.Category,
            ImageUrl = product.ImageUrl,
            OriginalPrice = product.Price,
            TotalPaid = finalPrice,
            CostPrice = costPrice,
            ProfitAmount = profitAmount
        };

        _db.TopupOrders.Add(order);
        await _db.SaveChangesAsync();

        // Reserve voucher quota AFTER order is persisted
        if (voucher != null)
        {
            await _voucherService.ReserveVoucherAsync(voucher);
        }
        
        var (token, redirectUrl) = await _midtransProvider.CreateSnapTransactionAsync(
            order.OrderNo, finalPrice, product.Id.ToString(), product.Name, fullPlayerId);

        order.SnapToken = token;
        order.RedirectUrl = redirectUrl;
        await _db.SaveChangesAsync();

        return new OrderResponse(
            order.Id, order.OrderNo, order.PlayerId, order.Status,
            order.ProductName, order.OriginalPrice, 
            order.SnapToken, order.RedirectUrl, order.OrderDate, order.Sn, order.ImageUrl,
            order.DiscountAmount, order.TotalPaid
        );
    }

    // ── Admin: lihat history seluruh transaksi ──────────────────
    public async Task<PagedResult<OrderResponse>> GetOrdersAsync(int? customerId = null, int page = 1, int limit = 20, string? status = null)
    {
        var query = _db.TopupOrders.AsNoTracking().AsQueryable();

        // Filter berdasarkan Customer
        if (customerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == customerId.Value);
        }

        // Filter berdasarkan Status
        if (!string.IsNullOrWhiteSpace(status))
        {
            string upperStatus = status.ToUpper();
            query = query.Where(o => o.Status == upperStatus);
        }

        // Hitung total data SEBELUM dipotong pagination
        int totalItems = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalItems / (double)limit);

        // Baca dari snapshot — tanpa join Product
        var data = await query
            .OrderByDescending(o => o.OrderDate)
            .Skip((page - 1) * limit) 
            .Take(limit)              
            .Select(o => new OrderResponse(
                o.Id, o.OrderNo ?? "-", o.PlayerId, o.Status, o.ProductName, 
                o.OriginalPrice, o.SnapToken, o.RedirectUrl, o.OrderDate, o.Sn,
                o.ImageUrl, o.DiscountAmount, o.TotalPaid))
            .ToListAsync();

        return new PagedResult<OrderResponse>(data, totalItems, totalPages, page);
    }

    public async Task<PagedResult<OrderResponse>> GetAllOrdersAsync(int page = 1, int limit = 20, string? status = null)
    {
        return await GetOrdersAsync(null, page, limit, status); 
    }

    public async Task HandleNotificationAsync(MidtransNotificationRequest notification)
    {
        // memvalidasi signature midtrans
        if (!_midtransProvider.ValidateSignature(
                notification.order_id,
                notification.status_code,
                notification.gross_amount,
                notification.signature_key))
        {
            throw new Exception("Signature key tidak valid! Data dicurigai palsu.");
        }

        // ambil pesanan — tanpa join Product (gunakan snapshot)
        var order = await _db.TopupOrders
            .FirstOrDefaultAsync(o => o.OrderNo == notification.order_id);

        if (order == null) return;

        if (order.Status != AppConstants.OrderStatus.Pending)
        {
            _logger.LogWarning(
                "[WEBHOOK] Duplicate/late webhook ignored for Order {OrderNo}. Current status: {Status}",
                order.OrderNo, order.Status);
            return;
        }

        // Route by Midtrans transaction_status 
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

        // Successful payment - settlement / capture + fraud accept
        if (notification.transaction_status != AppConstants.MidtransStatus.Settlement &&
            notification.transaction_status != AppConstants.MidtransStatus.Capture)
        {
            return; // Unrecognised status — do nothing
        }

        if (notification.fraud_status != AppConstants.MidtransFraud.Accept) return;

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            order.Status = AppConstants.OrderStatus.Paid;
            order.ProcessedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                string? sn = await _digiflazzProvider.TopupAsync(
                    order.BuyerSkuCode,
                    order.PlayerId,
                    order.OrderNo,
                    cts.Token);

                // Digiflazz responded - advance to PROCESSING
                order.Status = AppConstants.OrderStatus.Processing;

                // Instan SN
                if (!string.IsNullOrEmpty(sn))
                {
                    order.Sn = sn;
                    order.Status = AppConstants.OrderStatus.Completed;
                    _logger.LogInformation("[WEBHOOK] SN sudah di terima {OrderNo}: {SN}", order.OrderNo, sn);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "[WEBHOOK] Digiflazz TIMEOUT saat memesan {OrderNo}. Pesanan tetap DIBAYAR, menunggu bot jalan.",
                    order.OrderNo);
                order.Status = AppConstants.OrderStatus.NeedsAction;

                await SendStuckOrderAlertAsync(order, "Digiflazz API timeout (30s).");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex,
                    "[WEBHOOK] Digiflazz gagal memesan {OrderNo}", order.OrderNo);
                order.Status = AppConstants.OrderStatus.NeedsAction;
                await SendStuckOrderAlertAsync(order, ex.Message);
            }

            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogWarning(
                "[WEBHOOK] Konflik konkurensi pada Pesanan {OrderNo}. Proses lain sudah menanganinya",
                order.OrderNo);
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "[WEBHOOK] Kesalahan tidak terduga saat memproses Pesanan {OrderNo}", order.OrderNo);
            throw;
        }
    }

    // Private Helpers 

    private async Task SafeSaveChangesAsync(string orderNo)
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning(
                "[SaveChanges] Konflik konkurensi pada Pesanan {OrderNo}. Perubahan dibatalkan.",
                orderNo);
        }
    }

    private async Task SendStuckOrderAlertAsync(TopupOrder order, string reason)
    {
        string alertMessage = $@"⚠️ <b>PESANAN TERTUNDA (Uang Sudah Masuk)</b> ⚠️

Halo Admin, ada pesanan yang pembayarannya sudah berhasil diterima, tapi pengiriman item ke pembeli tertunda karena kendala di sistem pusat (Digiflazz).

<b>📝 Detail Pesanan:</b>
• <b>Invoice:</b> {order.OrderNo}
• <b>Produk:</b> {order.ProductName}
• <b>ID Tujuan:</b> {order.PlayerId}
• <b>Keterangan:</b> {reason}

<b>🔧 Apa yang harus dilakukan?</b>
1. Pastikan saldo Digiflazz Anda cukup.
2. Buka Admin Panel, cari pesanan ini, lalu klik tombol <b>Retry</b>.
3. Jika produk memang sedang kosong/gangguan, hubungi pembeli untuk melakukan <b>Refund</b> (Pengembalian Uang).";

        try
        {
            await _telegramProvider.SendNotificationAsync(alertMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Telegram] Gagal mengirim peringatan pesanan tertahan untuk {OrderNo}", order.OrderNo);
        }
    }
}
