using be_digitalin.Data;
using be_digitalin.DTOs;
using be_digitalin.Models;
using be_digitalin.Providers;
using be_digitalin.Constants;
using be_digitalin.Domain;
using be_digitalin.Domain.Orders;
using be_digitalin.Infrastructure.Serialization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
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

    public async Task<OrderResponse> PlaceOrderAsync(
        CreateOrderRequest request,
        int? customerId = null)
    {
        var product = await _db.GameProducts
            .AsNoTracking()
            .FirstOrDefaultAsync(product =>
                product.Id == request.ProductId &&
                product.IsActive);

        if (product == null)
            throw new InvalidOperationException(
                "Produk tidak ditemukan atau sudah tidak aktif.");

        var metadata = await _db.GameMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(metadata =>
                metadata.Brand == product.Brand);

        if (metadata == null)
            throw new InvalidOperationException(
                "Konfigurasi input game tidak ditemukan.");

        var fullPlayerId = BuildTargetValue(
            metadata,
            request.TargetValues);

        // Jejak customer_no vs template & nilai mentah — agar drift schema/template
        // (mis. field ter-abaikan saat render) langsung terlihat di log tanpa reproduksi ulang.
        _logger.LogInformation(
            "[ORDER] customer_no dirender untuk brand {Brand}: customer_no={CustomerNo}, template={Template}, values={@TargetValues}",
            product.Brand,
            fullPlayerId,
            metadata.TargetTemplate,
            request.TargetValues);

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
        decimal profitAmount = Pricing.MarginOf(product.CustomMargin);
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

        // Order + reservasi kuota voucher harus atomik: bila reservasi gagal
        // (mis. kuota habis), order ikut rollback sehingga tidak tersisa order
        // PENDING yatim yang terlanjur mencatat diskon.
        using (var voucherTx = await _db.Database.BeginTransactionAsync())
        {
            _db.TopupOrders.Add(order);
            await _db.SaveChangesAsync();

            // Reserve voucher quota AFTER order is persisted
            if (voucher != null)
            {
                await _voucherService.ReserveVoucherAsync(voucher);
            }

            await voucherTx.CommitAsync();
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
            if (await SafeSaveChangesAsync(order.OrderNo))
                await ReleaseVoucherIfPresentAsync(order);
            return;
        }

        if (notification.transaction_status == AppConstants.MidtransStatus.Cancel ||
            notification.transaction_status == AppConstants.MidtransStatus.Deny)
        {
            order.Status = AppConstants.OrderStatus.Failed;
            if (await SafeSaveChangesAsync(order.OrderNo))
                await ReleaseVoucherIfPresentAsync(order);
            return;
        }

        // Successful payment - settlement / capture + fraud accept
        if (notification.transaction_status != AppConstants.MidtransStatus.Settlement &&
            notification.transaction_status != AppConstants.MidtransStatus.Capture)
        {
            return; // Unrecognised status — do nothing
        }

        if (notification.fraud_status != AppConstants.MidtransFraud.Accept) return;

        // ── Guard nominal: nominal yang dibayar wajib sama dengan yang ditagih ──
        // Signature valid hanya membuktikan payload asli dari Midtrans, bukan bahwa
        // nominalnya benar. Transaksi Midtrans dibuat dengan (int)finalPrice
        // (lihat MidtransProvider.CreateSnapTransactionAsync), jadi nominal yang
        // diketahui Midtrans adalah bilangan bulat dari TotalPaid.
        var expectedAmount = (int)order.TotalPaid;
        if (!decimal.TryParse(
                notification.gross_amount,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var paidAmount)
            || (int)paidAmount != expectedAmount)
        {
            _logger.LogWarning(
                "[WEBHOOK] Nominal tidak cocok untuk Order {OrderNo}. Diharapkan {Expected}, diterima {Received}. Status tidak diubah.",
                order.OrderNo, expectedAmount, notification.gross_amount);
            return;
        }

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

    // ── Admin: retry pesanan yang nyangkut (NEEDS_ACTION) ─────────
    public async Task<OrderActionResult> RetryOrderAsync(string orderNo)
    {
        // Cari data order — tanpa join Product (gunakan snapshot)
        var order = await _db.TopupOrders
            .FirstOrDefaultAsync(o => o.OrderNo == orderNo);

        if (order == null)
            return new OrderActionResult(OrderActionOutcome.NotFound, "Pesanan tidak ditemukan");

        // Hanya boleh di-retry jika statusnya NEEDS_ACTION (nyangkut)
        if (order.Status != AppConstants.OrderStatus.NeedsAction)
            return new OrderActionResult(
                OrderActionOutcome.InvalidState,
                $"Tidak bisa retry. Status pesanan saat ini adalah: {order.Status}");

        try
        {
            try
            {
                var (upstreamStatus, upstreamSn, _) = await _digiflazzProvider.CheckStatusAsync(
                    order.BuyerSkuCode, order.PlayerId, order.OrderNo);

                if (upstreamStatus == AppConstants.DigiflazzStatus.Sukses)
                {
                    order.Status = AppConstants.OrderStatus.Completed;
                    order.Sn = upstreamSn;
                    await _db.SaveChangesAsync();
                    return new OrderActionResult(
                        OrderActionOutcome.Success,
                        "Order sudah selesai di Digiflazz sebelumnya. SN berhasil disimpan.",
                        order.Status);
                }

                if (upstreamStatus == AppConstants.DigiflazzStatus.Pending)
                {
                    return new OrderActionResult(
                        OrderActionOutcome.InvalidState,
                        "Digiflazz masih memproses pesanan ini. Harap tunggu sebelum mencoba retry.");
                }
            }
            catch
            {
                _logger.LogWarning("[RETRY] CheckStatus failed for Order {OrderNo}. Proceeding with retry.", order.OrderNo);
            }

            // Ubah status ke PROCESSING dulu
            order.Status = AppConstants.OrderStatus.Processing;
            await _db.SaveChangesAsync();

            // Tembak ulang API Digiflazz — gunakan snapshot BuyerSkuCode
            string? sn = await _digiflazzProvider.TopupAsync(order.BuyerSkuCode, order.PlayerId, order.OrderNo);

            // Jika entah kenapa Digiflazz ngasih SN secara instan saat itu juga
            if (!string.IsNullOrEmpty(sn))
            {
                order.Sn = sn;
                order.Status = AppConstants.OrderStatus.Completed;
            }

            await _db.SaveChangesAsync();
            return new OrderActionResult(
                OrderActionOutcome.Success,
                "Retry berhasil dikirim ke Digiflazz! Silakan tunggu SN masuk.",
                order.Status);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("[RETRY] Concurrency conflict on Order {OrderNo}. Retry was aborted.", order.OrderNo);
            return new OrderActionResult(
                OrderActionOutcome.Conflict,
                "Pesanan sedang diproses oleh sistem lain. Coba lagi beberapa saat.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RETRY] Unexpected error on Order {OrderNo}", order.OrderNo);
            order.Status = AppConstants.OrderStatus.NeedsAction;
            try { await _db.SaveChangesAsync(); } catch (DbUpdateConcurrencyException) { /* already handled elsewhere */ }

            return new OrderActionResult(
                OrderActionOutcome.Error,
                "Retry ke Digiflazz gagal. Silakan coba lagi atau hubungi tim teknis.");
        }
    }

    // ── Admin: refund manual (NEEDS_ACTION → REFUNDED) ────────────
    public async Task<OrderActionResult> RefundOrderAsync(string orderNo)
    {
        _logger.LogInformation("[REFUND REQUEST] Meminta pengembalian dana manual untuk Order: {OrderNo}", orderNo);

        try
        {
            var order = await _db.TopupOrders.FirstOrDefaultAsync(o => o.OrderNo == orderNo);

            if (order == null)
            {
                _logger.LogWarning("[REFUND WARNING] Gagal: Order {OrderNo} tidak ditemukan di database.", orderNo);
                return new OrderActionResult(OrderActionOutcome.NotFound, "Pesanan tidak ditemukan");
            }

            // Cuma boleh nge-refund pesanan yang memang bermasalah / nyangkut
            if (order.Status != AppConstants.OrderStatus.NeedsAction)
            {
                _logger.LogWarning(
                    "[REFUND WARNING] Ditolak: Order {OrderNo} berstatus '{Status}'. Hanya status '{Required}' yang bisa di-refund.",
                    orderNo, order.Status, AppConstants.OrderStatus.NeedsAction);
                return new OrderActionResult(
                    OrderActionOutcome.InvalidState,
                    $"Tidak bisa di-refund. Status pesanan saat ini: {order.Status}");
            }

            // Ubah statusnya
            order.Status = AppConstants.OrderStatus.Refunded;
            await _db.SaveChangesAsync();

            // Lepas kembali kuota voucher — refund adalah terminal tanpa fulfillment.
            await ReleaseVoucherIfPresentAsync(order);

            _logger.LogInformation("[REFUND SUCCESS] Order {OrderNo} berhasil diubah menjadi REFUNDED.", orderNo);

            return new OrderActionResult(
                OrderActionOutcome.Success,
                "Pesanan berhasil ditandai sebagai REFUNDED. Segera hubungi pembeli untuk transfer pengembalian dana secara manual via WA.",
                order.Status);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("[REFUND CONCURRENCY] Order {OrderNo} diubah oleh proses lain sebelum refund disimpan.", orderNo);
            return new OrderActionResult(
                OrderActionOutcome.Conflict,
                "Pesanan sedang diproses oleh sistem lain. Coba lagi beberapa saat.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[REFUND CRITICAL ERROR] Sistem gagal mengeksekusi refund untuk Order {OrderNo}", orderNo);
            return new OrderActionResult(
                OrderActionOutcome.ServerError,
                "Terjadi kesalahan internal saat memproses refund.");
        }
    }

    // ── Webhook: terapkan update status dari Digiflazz ────────────
    public async Task<string> HandleDigiflazzWebhookAsync(DigiflazzWebhookData data)
    {
        _logger.LogInformation("[WEBHOOK] Pesanan: {RefId} | Status: {Status} | SN: {SN}",
            data.ref_id, data.status, data.sn);

        var order = await _db.TopupOrders.FirstOrDefaultAsync(o => o.OrderNo == data.ref_id);
        if (order == null)
            return "Webhook Digiflazz diterima";

        // Tentukan target status berdasarkan balasan Digiflazz.
        string? targetStatus = data.status switch
        {
            AppConstants.DigiflazzStatus.Sukses => AppConstants.OrderStatus.Completed,
            AppConstants.DigiflazzStatus.Gagal  => AppConstants.OrderStatus.NeedsAction,
            _ => null // status lain (mis. Pending) — tidak mengubah apa pun
        };

        if (targetStatus == null)
            return "Webhook Digiflazz diterima";

        // Idempotensi: webhook duplikat untuk status yang sudah tercapai.
        if (order.Status == targetStatus)
        {
            _logger.LogWarning(
                "[WEBHOOK] Duplicate {Status} webhook untuk {OrderNo}. Diabaikan.",
                targetStatus, order.OrderNo);
            return "Webhook Digiflazz received (already processed)";
        }

        // Guard state machine (SSOT): tolak transisi ilegal, mis. COMPLETED/REFUNDED → NEEDS_ACTION.
        if (!OrderStateTransition.CanTransition(order.Status, targetStatus))
        {
            _logger.LogWarning(
                "[WEBHOOK] Transisi status ditolak untuk {OrderNo}: {From} → {To}. Webhook diabaikan.",
                order.OrderNo, order.Status, targetStatus);
            return "Webhook Digiflazz diterima";
        }

        if (targetStatus == AppConstants.OrderStatus.Completed)
        {
            order.Sn = data.sn;
            order.Status = AppConstants.OrderStatus.Completed;
            _logger.LogInformation("[WEBHOOK] SUCCESS! SN berhasil ditanam ke database");
        }
        else // NEEDS_ACTION
        {
            // INI YANG PALING PENTING! Uang udah di tangan kita, tapi Digiflazz gagal proses
            order.Status = AppConstants.OrderStatus.NeedsAction;
            _logger.LogWarning("[WEBHOOK CRITICAL] DIGIFLAZZ GAGAL! Order: {OrderNo} butuh penanganan manual.", order.OrderNo);
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning(
                "[WEBHOOK] Concurrency conflict pada Order {OrderNo}. Sudah di-update oleh proses lain.",
                order.OrderNo);
        }

        return "Webhook Digiflazz diterima";
    }

    // ── Bot Patroli: rekonsiliasi pesanan yang nyangkut di PROCESSING ──
    public async Task SweepStuckOrdersAsync(CancellationToken cancellationToken = default)
    {
        // Cari pesanan yang statusnya PROCESSING dan sudah lebih dari 3 menit
        var timeThreshold = DateTime.UtcNow.AddMinutes(-3);
        var stuckOrders = await _db.TopupOrders
            .Where(o => o.Status == AppConstants.OrderStatus.Processing && o.OrderDate <= timeThreshold)
            .ToListAsync(cancellationToken);

        if (stuckOrders.Count == 0) return; // tidak ada yang nyangkut

        _logger.LogInformation(
            "[SWEEP] Menemukan {Count} pesanan nyangkut. Memulai interogasi ke Digiflazz...",
            stuckOrders.Count);

        foreach (var order in stuckOrders)
        {
            try
            {
                // Minta penjelasan status ke Digiflazz — gunakan snapshot BuyerSkuCode
                var (status, sn, message) = await _digiflazzProvider.CheckStatusAsync(
                    order.BuyerSkuCode, order.PlayerId, order.OrderNo);

                if (status == AppConstants.DigiflazzStatus.Sukses)
                {
                    if (!TryTransition(order, AppConstants.OrderStatus.Completed)) continue;
                    order.Sn = sn;
                    _logger.LogInformation("[SWEEP] Order {OrderNo} ternyata SUKSES. Data SN diperbarui.", order.OrderNo);
                }
                else if (status == AppConstants.DigiflazzStatus.Gagal)
                {
                    if (!TryTransition(order, AppConstants.OrderStatus.NeedsAction)) continue;
                    _logger.LogWarning("[SWEEP] Order {OrderNo} dipastikan GAGAL dari pusat. Mengunci pesanan...", order.OrderNo);
                    await SendSweepFailedAlertAsync(order, message);
                }
                else
                {
                    // Status dari pusat masih "Pending" — cek apakah sudah terlalu lama (>15 menit)
                    var maxToleransi = DateTime.UtcNow.AddMinutes(-15);

                    if (order.OrderDate <= maxToleransi)
                    {
                        if (!TryTransition(order, AppConstants.OrderStatus.NeedsAction)) continue;
                        _logger.LogWarning("[SWEEP] Order {OrderNo} kelamaan Pending di Digiflazz (>15 menit). Force Needs Action!", order.OrderNo);
                        await SendSweepTimeoutAlertAsync(order);
                    }
                    else
                    {
                        _logger.LogInformation("[SWEEP] Order {OrderNo} masih Pending dari pusat. Sabar, tunggu putaran selanjutnya.", order.OrderNo);
                        // tidak ada yang berubah, langsung skip
                        continue;
                    }
                }

                try
                {
                    await _db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    _logger.LogWarning(
                        "[SWEEP] Concurrency conflict on Order {OrderNo}. Another process already updated it — skipping.",
                        order.OrderNo);
                    await _db.Entry(order).ReloadAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("[SWEEP] Gagal mengecek order {OrderNo}: {Message}", order.OrderNo, ex.Message);
            }
        }
    }

    // Private Helpers

    /// <summary>
    /// Terapkan transisi status lewat state machine SSOT. Mengembalikan false
    /// (dan melewati order) bila transisi tidak sah — mis. order sudah pindah
    /// dari PROCESSING oleh proses lain di antara query dan pemrosesan.
    /// </summary>
    private bool TryTransition(TopupOrder order, string targetStatus)
    {
        if (!OrderStateTransition.CanTransition(order.Status, targetStatus))
        {
            _logger.LogWarning(
                "[SWEEP] Transisi status ditolak untuk {OrderNo}: {From} → {To}. Dilewati.",
                order.OrderNo, order.Status, targetStatus);
            return false;
        }

        order.Status = targetStatus;
        return true;
    }

    private async Task SendSweepFailedAlertAsync(TopupOrder order, string message)
    {
        string alertMessage = $@"⚠️ <b>PESANAN TERTUNDA (Hasil Sapuan Sistem)</b> ⚠️

Halo Admin, sistem menemukan pesanan lama yang gagal diproses oleh Digiflazz.

<b>📝 Detail Pesanan:</b>
• <b>Invoice:</b> {order.OrderNo}
• <b>Produk:</b> {order.ProductName}
• <b>ID Tujuan:</b> {order.PlayerId}
• <b>Keterangan:</b> {message}

Silakan buka Admin Panel untuk klik Retry atau hubungi pembeli untuk Refund.";

        try
        {
            await _telegramProvider.SendNotificationAsync(alertMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Telegram] Gagal mengirim peringatan sweep-gagal untuk {OrderNo}", order.OrderNo);
        }
    }

    private async Task SendSweepTimeoutAlertAsync(TopupOrder order)
    {
        string timeoutMessage = $@"⚠️ <b>PESANAN MENGGANTUNG / TIMEOUT</b> ⚠️

Halo Admin, sistem menemukan pesanan yang sudah lebih dari 15 menit dibiarkan 'Pending' oleh Digiflazz tanpa kejelasan.

<b>📝 Detail Pesanan:</b>
• <b>Invoice:</b> {order.OrderNo}
• <b>Produk:</b> {order.ProductName}
• <b>ID Tujuan:</b> {order.PlayerId}

Silakan cek manual di dashboard Digiflazz. Jika pesanan tidak masuk, segera proses <b>Refund</b> ke pembeli agar tidak dikomplain.";

        try
        {
            await _telegramProvider.SendNotificationAsync(timeoutMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Telegram] Gagal mengirim peringatan sweep-timeout untuk {OrderNo}", order.OrderNo);
        }
    }

    internal static string BuildTargetValue(
        GameMetadata metadata,
        Dictionary<string, string?> targetValues)
    {
        if (targetValues == null || targetValues.Count == 0)
        {
            throw new InvalidOperationException(
                "Data tujuan wajib diisi.");
        }

        var inputFields = GameInputSchemaSerializer.Deserialize(
            metadata.InputSchemaJson);

        var allowedKeys = inputFields
            .Select(field => field.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unknownKey = targetValues.Keys
            .FirstOrDefault(key => !allowedKeys.Contains(key));

        if (unknownKey != null)
        {
            throw new InvalidOperationException(
                $"Field input '{unknownKey}' tidak dikenali.");
        }

        ValidateTargetValues(
            inputFields,
            targetValues);

        return RenderTargetTemplate(
            metadata.TargetTemplate,
            inputFields,
            targetValues);
    }
    
    internal static void ValidateTargetValues(
        IReadOnlyList<GameInputField> inputFields,
        Dictionary<string, string?> targetValues)
    {
        if (inputFields.Count == 0)
        {
            throw new InvalidOperationException(
                "Game belum memiliki konfigurasi input.");
        }

        foreach (var field in inputFields)
        {
            targetValues.TryGetValue(
                field.Key,
                out var rawValue);

            var value = rawValue?.Trim();

            if (field.Required &&
                string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"{field.Label} wajib diisi.");
            }

            if (string.IsNullOrWhiteSpace(value))
                continue;
            
            if (field.Type == AppConstants.GameInputFieldTypes.Number &&
                value.Any(character => !char.IsDigit(character)))
            {
                throw new InvalidOperationException(
                    $"{field.Label} hanya boleh berisi angka.");
            }

            if (field.Type == AppConstants.GameInputFieldTypes.Select)
            {
                var isValidOption = field.Options.Any(option =>
                    string.Equals(
                        option,
                        value,
                        StringComparison.OrdinalIgnoreCase));

                if (!isValidOption)
                {
                    throw new InvalidOperationException(
                        $"Pilihan {field.Label} tidak valid.");
                }
            }
        }
    }
    
    internal static string RenderTargetTemplate(
        string targetTemplate,
        IReadOnlyList<GameInputField> inputFields,
        Dictionary<string, string?> targetValues)
    {
        if (string.IsNullOrWhiteSpace(targetTemplate))
        {
            throw new InvalidOperationException(
                "Template tujuan game belum dikonfigurasi.");
        }

        var result = targetTemplate;

        foreach (var field in inputFields)
        {
            targetValues.TryGetValue(
                field.Key,
                out var rawValue);

            var value = rawValue?.Trim() ?? string.Empty;

            result = result.Replace(
                $"{{{field.Key}}}",
                value,
                StringComparison.OrdinalIgnoreCase);
        }

        if (result.Contains('{') || result.Contains('}'))
        {
            throw new InvalidOperationException(
                "Template tujuan game memiliki field yang tidak valid.");
        }

        if (string.IsNullOrWhiteSpace(result))
        {
            throw new InvalidOperationException(
                "Data tujuan tidak valid.");
        }

        return result;
    }

    private async Task<bool> SafeSaveChangesAsync(string orderNo)
    {
        try
        {
            await _db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning(
                "[SaveChanges] Konflik konkurensi pada Pesanan {OrderNo}. Perubahan dibatalkan.",
                orderNo);
            return false;
        }
    }

    // ── Release kuota voucher untuk order yang berakhir tanpa fulfillment ──
    // Idempoten: hanya dipanggil setelah transisi status TERSIMPAN (guard status +
    // RowVersion memastikan webhook/retry duplikat tidak sampai ke sini dua kali),
    // dan ReleaseVoucherAsync sendiri hanya menurunkan UsedCount bila > 0.
    private async Task ReleaseVoucherIfPresentAsync(TopupOrder order)
    {
        if (string.IsNullOrWhiteSpace(order.VoucherCode))
            return;

        await _voucherService.ReleaseVoucherAsync(order.VoucherCode);
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
