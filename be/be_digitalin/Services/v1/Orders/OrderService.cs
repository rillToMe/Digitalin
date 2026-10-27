using be_digitalin.Application.Ports;
using be_digitalin.DTOs;
using be_digitalin.Domain.Entities;

using be_digitalin.Constants;
using be_digitalin.Domain;
using be_digitalin.Domain.Orders;
using be_digitalin.Application.Serialization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Transactions;
using be_digitalin.Services.Commerce;
using be_digitalin.Services.Settings;
using be_digitalin.Config;
using Microsoft.Extensions.Options;

namespace be_digitalin.Services.Orders;

public class OrderService : IOrderService
{
    private readonly IApplicationDbContext _db;
    private readonly IMidtransProvider _midtransProvider;
    private readonly IDigiflazzProvider _digiflazzProvider;
    private readonly ITelegramProvider _telegramProvider;
    private readonly IVoucherService _voucherService;
    private readonly IAppSettingsService _appSettings;
    private readonly ILogger<OrderService> _logger;
    private readonly OrderPublicHashSettings _publicHashSettings;

    public OrderService(
        IApplicationDbContext db,
        IMidtransProvider midtransProvider,
        IDigiflazzProvider digiflazzProvider,
        ITelegramProvider telegramProvider,
        IVoucherService voucherService,
        IAppSettingsService appSettings,
        ILogger<OrderService> logger,
        IOptions<OrderPublicHashSettings> publicHashSettings)
    {
        _db = db;
        _midtransProvider = midtransProvider;
        _digiflazzProvider = digiflazzProvider;
        _telegramProvider = telegramProvider;
        _voucherService = voucherService;
        _appSettings = appSettings;
        _logger = logger;
        _publicHashSettings = publicHashSettings.Value;
    }

    public async Task<OrderResponse> PlaceOrderAsync(
        CreateOrderRequest request,
        int? customerId = null)
    {
        // Validasi metode pembayaran paling awal — request tidak sah harus
        // ditolak sebelum menyentuh DB (paling murah, paling awal).
        // Transaksi selalu dibuat via Core API; paymentMethod wajib.
        string? paymentMethod = request.PaymentMethod?.Trim().ToLowerInvariant();
        string? paymentBank = request.PaymentBank?.Trim().ToLowerInvariant();
        string? paymentStore = request.PaymentStore?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(paymentMethod))
        {
            throw new InvalidOperationException(
                "Metode pembayaran wajib dipilih: qris, gopay, atau bank_transfer.");
        }

        bool knownMethod =
            paymentMethod == AppConstants.MidtransPaymentMethods.Qris ||
            paymentMethod == AppConstants.MidtransPaymentMethods.GoPay ||
            paymentMethod == AppConstants.MidtransPaymentMethods.ShopeePay ||
            paymentMethod == AppConstants.MidtransPaymentMethods.BankTransfer ||
            paymentMethod == AppConstants.MidtransPaymentMethods.Cstore;

        if (!knownMethod)
        {
            throw new InvalidOperationException(
                "Metode pembayaran tidak didukung. Gunakan: qris, gopay, atau bank_transfer.");
        }

        if (paymentMethod == AppConstants.MidtransPaymentMethods.BankTransfer)
        {
            if (string.IsNullOrEmpty(paymentBank))
                throw new InvalidOperationException(
                    "Pembayaran bank_transfer wajib memilih bank VA (bca/bni/bri/cimb/permata/mandiri/bsi).");

            if (!AppConstants.MidtransPaymentMethods.VaBanks.Contains(paymentBank))
                throw new InvalidOperationException(
                    $"Bank VA tidak didukung: {paymentBank}. Pilih bca/bni/bri/cimb/permata/mandiri/bsi.");
        }
        else if (!string.IsNullOrEmpty(paymentBank))
        {
            throw new InvalidOperationException(
                "paymentBank hanya berlaku untuk paymentMethod bank_transfer.");
        }

        if (paymentMethod == AppConstants.MidtransPaymentMethods.Cstore)
        {
            if (string.IsNullOrEmpty(paymentStore))
                throw new InvalidOperationException(
                    "Pembayaran cstore wajib memilih toko (indomaret/alfamart).");

            if (!AppConstants.MidtransPaymentMethods.CstoreStores.Contains(paymentStore))
                throw new InvalidOperationException(
                    $"Toko tidak didukung: {paymentStore}. Pilih indomaret/alfamart.");
        }
        else if (!string.IsNullOrEmpty(paymentStore))
        {
            throw new InvalidOperationException(
                "paymentStore hanya berlaku untuk paymentMethod cstore.");
        }

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

        // ── Konfigurasi checkout (SSOT tabel PaymentMethods): metode/bank
        // harus aktif — admin dapat menonaktifkan tanpa deploy. Kemampuan
        // provider tetap di-whitelist AppConstants (validasi awal di atas).
        var methodConfig = await _db.PaymentMethods
            .AsNoTracking()
            .AnyAsync(m => m.Code == paymentMethod && m.Enabled);

        if (!methodConfig)
            throw new InvalidOperationException(
                "Metode pembayaran sedang tidak tersedia.");

        if (paymentMethod == AppConstants.MidtransPaymentMethods.BankTransfer)
        {
            var bankEnabled = await _db.PaymentBanks
                .AsNoTracking()
                .AnyAsync(b =>
                    b.PaymentMethodCode == paymentMethod &&
                    b.Code == paymentBank &&
                    b.Enabled);

            if (!bankEnabled)
                throw new InvalidOperationException(
                    "Bank VA sedang tidak tersedia.");
        }

        // Sub-kanal cstore (toko) juga lewat tabel PaymentBanks — struktur
        // pilihan sub-kanal identik (code/nama/ikon/aktif/urutan).
        if (paymentMethod == AppConstants.MidtransPaymentMethods.Cstore)
        {
            var storeEnabled = await _db.PaymentBanks
                .AsNoTracking()
                .AnyAsync(b =>
                    b.PaymentMethodCode == paymentMethod &&
                    b.Code == paymentStore &&
                    b.Enabled);

            if (!storeEnabled)
                throw new InvalidOperationException(
                    "Toko sedang tidak tersedia.");
        }

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
        // Modal = harga jual − margin (basis supplier, tidak terpengaruh diskon).
        // Profit dihitung dari harga AKHIR setelah diskon — voucher dipotong dari
        // penerimaan penjual, bukan dari modal. Rumus lama (profit = margin)
        // mengabaikan diskon sehingga dashboard membaca profit terlampau besar.
        decimal costPrice = product.Price - Pricing.MarginOf(product.CustomMargin);
        decimal profitAmount = finalPrice - costPrice;

        // Suffix 10 hex char dari Guid (16^10 ≈ 10^12 kombinasi/hari) —
        // ruang cukup luas untuk harian, dan unique index di DB menutup
        // sisa risiko tabrakan (insert duplikat ditolak, bukan diam-diam
        // salah sambung ke pembayaran order lain).
        string generatedOrderNo = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..10].ToUpper()}";

        // PublicHash dihitung atomik bersama OrderNo — order baru langsung
        // punya URL status hash; kolom unique index menutup tabrakan.
        string publicHash = OrderPublicHash.Compute(
            generatedOrderNo, _publicHashSettings.SecretKey);

        var order = new TopupOrder
        {
            OrderNo = generatedOrderNo,
            PublicHash = publicHash,
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
            BrandSlug = metadata.BrandSlug,
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

        // Charge Midtrans Core API dibuat SETELAH order ter-commit. Kalau
        // Midtrans gagal, order PENDING tanpa transaksi akan yatim: Midtrans
        // tidak pernah mengirim webhook untuk transaksi yang tidak tercipta,
        // jadi tidak ada yang melepaskan kuota voucher. Kompensasi: tandai
        // EXPIRED + lepas kuota, lalu lempar error ke client.
        OrderPaymentInstructions? paymentInstructions;
        try
        {
            var charge = await _midtransProvider.CreateChargeAsync(new MidtransChargeRequest(
                order.OrderNo, finalPrice, product.Id.ToString(), product.Name, fullPlayerId,
                paymentMethod!,
                paymentMethod == AppConstants.MidtransPaymentMethods.BankTransfer ? paymentBank : null,
                paymentMethod == AppConstants.MidtransPaymentMethods.Cstore ? paymentStore : null));

            order.PaymentType = charge.PaymentType;
            order.MidtransTransactionId = charge.TransactionId;
            paymentInstructions = MapPaymentInstructions(charge);
            order.PaymentInstructionsJson = OrderPaymentInstructionsCodec.Serialize(paymentInstructions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ORDER] Midtrans gagal membuat transaksi untuk {OrderNo}. Order dikompensasi ke EXPIRED.",
                order.OrderNo);

            if (TryTransition(order, AppConstants.OrderStatus.Expired) &&
                await SafeSaveChangesAsync(order.OrderNo))
            {
                await ReleaseVoucherIfPresentAsync(order);
            }

            throw;
        }

        await _db.SaveChangesAsync();

        return new OrderResponse(
            order.Id, order.OrderNo, order.PlayerId, order.Status,
            order.ProductName, order.OriginalPrice,
            order.OrderDate, order.Sn, order.ImageUrl,
            order.DiscountAmount, order.TotalPaid,
            order.PaymentType, paymentInstructions,
            order.PublicHash
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

        // Baca dari snapshot — tanpa join Product. Instruksi pembayaran
        // didekode di memori: codec tidak bisa diterjemahkan EF ke SQL.
        var rows = await query
            .OrderByDescending(o => o.OrderDate)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(o => new
            {
                o.Id, o.OrderNo, o.PublicHash, o.PlayerId, o.Status, o.ProductName,
                o.OriginalPrice, o.OrderDate, o.Sn,
                o.ImageUrl, o.DiscountAmount, o.TotalPaid,
                o.PaymentType, o.PaymentInstructionsJson
            })
            .ToListAsync();

        var data = rows
            .Select(o => new OrderResponse(
                o.Id, o.OrderNo ?? "-", o.PlayerId, o.Status, o.ProductName,
                o.OriginalPrice, o.OrderDate, o.Sn,
                o.ImageUrl, o.DiscountAmount, o.TotalPaid,
                o.PaymentType, OrderPaymentInstructionsCodec.Deserialize(o.PaymentInstructionsJson),
                o.PublicHash
            ))
            .ToList();

        return new PagedResult<OrderResponse>(data, totalItems, totalPages, page);
    }

    public async Task<PagedResult<OrderResponse>> GetAllOrdersAsync(int page = 1, int limit = 20, string? status = null)
    {
        return await GetOrdersAsync(null, page, limit, status); 
    }

    /// <summary>
    /// Terapkan notifikasi pembayaran Midtrans. Mengembalikan <c>false</c> HANYA
    /// untuk signature tidak valid (controller memetakan ke 401); false ≠ error
    /// internal — kegagalan pemrosesan tetap melempar exception.
    /// </summary>
    public async Task<bool> HandleNotificationAsync(MidtransNotificationRequest notification)
    {
        // memvalidasi signature midtrans
        if (!_midtransProvider.ValidateSignature(
                notification.order_id,
                notification.status_code,
                notification.gross_amount,
                notification.signature_key))
        {
            _logger.LogWarning(
                "[WEBHOOK] Signature Midtrans tidak valid untuk Order {OrderNo}. Ditolak.",
                notification.order_id);
            return false;
        }

        // ambil pesanan — tanpa join Product (gunakan snapshot)
        var order = await _db.TopupOrders
            .FirstOrDefaultAsync(o => o.OrderNo == notification.order_id);

        if (order == null) return true;

        if (order.Status != AppConstants.OrderStatus.Pending)
        {
            _logger.LogWarning(
                "[WEBHOOK] Duplicate/late webhook ignored for Order {OrderNo}. Current status: {Status}",
                order.OrderNo, order.Status);
            return true;
        }

        // ── Enrichment: catat metode + id transaksi aktual dari notifikasi ──
        // Order yang dibuat sebelum migrasi Core API mungkin tanpa PaymentType
        // (kolom baru, backfill dari notifikasi pertama); order Core sudah
        // terisi saat charge — jangan timpa. Tersimpan bersama transisi status
        // di tiap cabang.
        if (string.IsNullOrEmpty(order.PaymentType) &&
            !string.IsNullOrEmpty(notification.payment_type))
        {
            order.PaymentType = notification.payment_type;
        }
        if (string.IsNullOrEmpty(order.MidtransTransactionId) &&
            !string.IsNullOrEmpty(notification.transaction_id))
        {
            order.MidtransTransactionId = notification.transaction_id;
        }

        // Route by Midtrans transaction_status 
        if (notification.transaction_status == AppConstants.MidtransStatus.Expire)
        {
            order.Status = AppConstants.OrderStatus.Expired;
            if (await SafeSaveChangesAsync(order.OrderNo))
                await ReleaseVoucherIfPresentAsync(order);
            return true;
        }

        if (notification.transaction_status == AppConstants.MidtransStatus.Cancel ||
            notification.transaction_status == AppConstants.MidtransStatus.Deny)
        {
            order.Status = AppConstants.OrderStatus.Failed;
            if (await SafeSaveChangesAsync(order.OrderNo))
                await ReleaseVoucherIfPresentAsync(order);
            return true;
        }

        // Successful payment - settlement / capture + fraud accept
        if (notification.transaction_status != AppConstants.MidtransStatus.Settlement &&
            notification.transaction_status != AppConstants.MidtransStatus.Capture)
        {
            return true; // Unrecognised status — do nothing
        }

        // fraud_status tidak selalu hadir — notifikasi settlement cstore
        // terverifikasi TANPA field ini (sandbox E2E 2026-09-08). Null ≠
        // ditolak: keputusan fraud tetap dipegang konfirmasi status API di
        // bawah, yang responsnya selalu memuat fraud_status.
        if (notification.fraud_status is not null &&
            notification.fraud_status != AppConstants.MidtransFraud.Accept)
            return true;

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
            return true;
        }

        // ── Defense-in-depth: konfirmasi ulang ke Midtrans sebelum commit PAID ──
        // Signature membuktikan keaslian pengirim, bukan kebenaran isi — tanya
        // sumbernya sekali lagi (GET /v2/{order_id}/status). Fail-open saat
        // status API tidak terbaca (throw/404): notifikasi bertanda tangan tetap
        // otoritatif, jangan tahan pembayaran karena infrastruktur verifikasi
        // kita down. Hanya kontradiksi definitif (status terminal negatif /
        // nominal berbeda / fraud deny) yang membatalkan transisi PAID —
        // snapshot "pending" dibiarkan lolos karena notifikasi event bisa
        // lebih baru dari snapshot.
        try
        {
            var providerStatus = await _midtransProvider.GetStatusAsync(order.OrderNo);
            if (providerStatus != null)
            {
                bool statusNegative =
                    providerStatus.TransactionStatus == AppConstants.MidtransStatus.Expire ||
                    providerStatus.TransactionStatus == AppConstants.MidtransStatus.Cancel ||
                    providerStatus.TransactionStatus == AppConstants.MidtransStatus.Deny;
                bool statusFraudDenied =
                    providerStatus.FraudStatus != null &&
                    providerStatus.FraudStatus != AppConstants.MidtransFraud.Accept;
                bool statusAmountMismatch =
                    !decimal.TryParse(
                        providerStatus.GrossAmount,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var statusAmount) ||
                    (int)statusAmount != expectedAmount;

                if (statusNegative || statusFraudDenied || statusAmountMismatch)
                {
                    _logger.LogCritical(
                        "[WEBHOOK] Konfirmasi status Midtrans BERTENTANGAN untuk Order {OrderNo}. " +
                        "Notifikasi: {NotifStatus}/{NotifFraud}/{NotifAmount} — Status API: {ApiStatus}/{ApiFraud}/{ApiAmount}. Status tidak diubah.",
                        order.OrderNo,
                        notification.transaction_status, notification.fraud_status, notification.gross_amount,
                        providerStatus.TransactionStatus, providerStatus.FraudStatus, providerStatus.GrossAmount);
                    return true;
                }

                // Konfirmasi lolos — pastikan transaction_id tercatat dari sumber.
                if (string.IsNullOrEmpty(order.MidtransTransactionId) &&
                    !string.IsNullOrEmpty(providerStatus.TransactionId))
                {
                    order.MidtransTransactionId = providerStatus.TransactionId;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[WEBHOOK] Gagal konfirmasi status Midtrans untuk Order {OrderNo}. Lanjut dengan notifikasi bertanda tangan (fail-open).",
                order.OrderNo);
        }

        // ── Transaksi SANGAT singkat: hanya PENDING → PAID ──
        // Tidak ada external call di dalam transaction supaya row lock tidak
        // bertahan selama panggilan HTTP Digiflazz (hardening P1).
        using (var dbTransaction = await _db.Database.BeginTransactionAsync())
        {
            try
            {
                order.Status = AppConstants.OrderStatus.Paid;
                order.ProcessedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                await dbTransaction.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogWarning(
                    "[WEBHOOK] Konflik konkurensi pada Pesanan {OrderNo}. Proses lain sudah menanganinya",
                    order.OrderNo);
                return true;
            }
        }

        // ── Fulfillment DI LUAR transaction ──
        // Kegagalan Digiflazz/Telegram tidak pernah rollback status pembayaran:
        // PAID adalah fakta yang sudah di-commit; kegagalan fulfillment menjadi
        // NEEDS_ACTION dan direkonsiliasi sweep/retry.
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            string? sn = await _digiflazzProvider.TopupAsync(
                order.BuyerSkuCode,
                order.PlayerId,
                order.OrderNo,
                cts.Token);

            // Digiflazz responded - advance to PROCESSING
            if (!TryTransition(order, AppConstants.OrderStatus.Processing)) return true;

            // Instan SN
            if (!string.IsNullOrEmpty(sn))
            {
                order.Sn = sn;
                order.Status = AppConstants.OrderStatus.Completed;
                _logger.LogInformation("[WEBHOOK] SN sudah di terima {OrderNo}: {SN}", order.OrderNo, sn);
            }

            await SafeSaveChangesAsync(order.OrderNo);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "[WEBHOOK] Digiflazz TIMEOUT saat memesan {OrderNo}. Pesanan tetap DIBAYAR, menunggu bot jalan.",
                order.OrderNo);

            if (!TryTransition(order, AppConstants.OrderStatus.NeedsAction)) return true;
            if (await SafeSaveChangesAsync(order.OrderNo))
                await SendStuckOrderAlertAsync(order, "Digiflazz API timeout (30s).");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex,
                "[WEBHOOK] Digiflazz gagal memesan {OrderNo}", order.OrderNo);

            if (!TryTransition(order, AppConstants.OrderStatus.NeedsAction)) return true;
            if (await SafeSaveChangesAsync(order.OrderNo))
                await SendStuckOrderAlertAsync(order, ex.Message);
        }

        return true;
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
        // PAID = commit pembayaran sukses tapi fulfillment belum selesai
        // (crash/restart antara commit dan panggilan Digiflazz) — wajib di-resume,
        // kalau tidak order uang-masuk terlantar tanpa rekonsiliasi.
        //
        // PENDING = order ter-commit tapi tidak terbayar dalam jendela expiry
        // Midtrans (+buffer). Transaksi Midtrans sudah hangus di sisi Midtrans —
        // atau tidak pernah tercipta — dan webhook Expire tidak dijamin datang.
        // Sweep
        // menutup celah order PENDING yatim (termasuk kuota voucher yang
        // terlanjur direservasi). Buffer memberi ruang bagi webhook Expire
        // Midtrans agar tetap yang menangani kasus normal.
        int midtransExpiryMinutes = await _appSettings.GetMidtransExpiryMinutesAsync();
        // Cutoff per metode (OrderExpiryPolicy): cstore 24 jam — Midtrans
        // mengabaikan expiry custom di kanal itu, mematikan lebih awal
        // berarti order EXPIRED padahal kode bayar di kasir masih hidup
        // (= uang masuk, order mati).
        int cstoreExpiryMinutes = OrderExpiryPolicy.ExpiryMinutes(
            AppConstants.MidtransPaymentMethods.Cstore, midtransExpiryMinutes);
        var pendingCutoff = DateTime.UtcNow.AddMinutes(-(midtransExpiryMinutes + PendingSweepBufferMinutes));
        var cstorePendingCutoff = DateTime.UtcNow.AddMinutes(-(cstoreExpiryMinutes + PendingSweepBufferMinutes));
        var stuckOrders = await _db.TopupOrders
            .Where(o =>
                (o.Status == AppConstants.OrderStatus.Processing && o.OrderDate <= timeThreshold) ||
                (o.Status == AppConstants.OrderStatus.Paid && o.ProcessedAt <= timeThreshold) ||
                (o.Status == AppConstants.OrderStatus.Pending &&
                    ((o.PaymentType != AppConstants.MidtransPaymentMethods.Cstore && o.OrderDate <= pendingCutoff) ||
                     (o.PaymentType == AppConstants.MidtransPaymentMethods.Cstore && o.OrderDate <= cstorePendingCutoff))))
            .ToListAsync(cancellationToken);

        if (stuckOrders.Count == 0) return; // tidak ada yang nyangkut

        _logger.LogInformation(
            "[SWEEP] Menemukan {Count} pesanan nyangkut. Memulai interogasi ke Digiflazz...",
            stuckOrders.Count);

        foreach (var order in stuckOrders)
        {
            try
            {
                // PAID: commit pembayaran sukses tapi fulfillment belum selesai
                // (crash/restart antara commit dan panggilan Digiflazz) — resume.
                if (order.Status == AppConstants.OrderStatus.Paid)
                {
                    await ResumeFulfillmentAsync(order, cancellationToken);
                    continue;
                }

                // PENDING: jendela pembayaran habis tanpa settlement —
                // EXPIRED + lepas kuota voucher.
                if (order.Status == AppConstants.OrderStatus.Pending)
                {
                    if (!TryTransition(order, AppConstants.OrderStatus.Expired)) continue;

                    if (await SafeSaveChangesAsync(order.OrderNo))
                        await ReleaseVoucherIfPresentAsync(order);

                    _logger.LogInformation(
                        "[SWEEP] Order {OrderNo} PENDING melewati expiry Midtrans ({Expiry}+{Buffer} menit) → EXPIRED.",
                        order.OrderNo, midtransExpiryMinutes, PendingSweepBufferMinutes);
                    continue;
                }

                // Minta penjelasan status ke Digiflazz — gunakan snapshot BuyerSkuCode
                var (status, sn, message) = await _digiflazzProvider.CheckStatusAsync(
            order.BuyerSkuCode, order.PlayerId, order.OrderNo, cancellationToken);

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
                    await SendSweepFailedAlertAsync(order, message, cancellationToken);
                }
                else
                {
                    // Status dari pusat masih "Pending" — cek apakah sudah terlalu lama (>15 menit)
                    var maxToleransi = DateTime.UtcNow.AddMinutes(-15);

                    if (order.OrderDate <= maxToleransi)
                    {
                        if (!TryTransition(order, AppConstants.OrderStatus.NeedsAction)) continue;
                        _logger.LogWarning("[SWEEP] Order {OrderNo} kelamaan Pending di Digiflazz (>15 menit). Force Needs Action!", order.OrderNo);
                        await SendSweepTimeoutAlertAsync(order, cancellationToken);
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

    private async Task SendSweepFailedAlertAsync(TopupOrder order, string message, CancellationToken cancellationToken)
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
            await _telegramProvider.SendNotificationAsync(alertMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Telegram] Gagal mengirim peringatan sweep-gagal untuk {OrderNo}", order.OrderNo);
        }
    }

    private async Task SendSweepTimeoutAlertAsync(TopupOrder order, CancellationToken cancellationToken)
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
            await _telegramProvider.SendNotificationAsync(timeoutMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Telegram] Gagal mengirim peringatan sweep-timeout untuk {OrderNo}", order.OrderNo);
        }
    }

    // Petakan hasil charge Core API ke instruksi kontrak API. Permata VA
    // dikirim Midtrans di luar va_numbers — dinormalisasi ke VaBank/VaNumber
    // supaya FE punya satu bentuk render untuk semua VA.
    internal static OrderPaymentInstructions? MapPaymentInstructions(MidtransChargeResult? charge)
    {
        if (charge is null) return null;

        var va = charge.VaNumbers.FirstOrDefault();
        string? vaBank = va?.Bank;
        string? vaNumber = va?.VaNumber;

        if (vaBank is null && vaNumber is null && !string.IsNullOrEmpty(charge.PermataVaNumber))
        {
            vaBank = "permata";
            vaNumber = charge.PermataVaNumber;
        }

        return new OrderPaymentInstructions(
            charge.QrString,
            charge.Actions.Select(a => new OrderPaymentAction(a.Name, a.Url)).ToList(),
            vaBank,
            vaNumber,
            charge.BillerCode,
            charge.BillKey,
            charge.Store,
            charge.PaymentCode,
            charge.MerchantId);
    }

    internal static string BuildTargetValue(
        GameMetadata metadata,
        Dictionary<string, string?> targetValues)    {
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

        OrderTargetRenderer.ValidateTargetValues(
            inputFields,
            targetValues);

        return OrderTargetRenderer.RenderTargetTemplate(
            metadata.TargetTemplate,
            inputFields,
            targetValues);
    }
    
    // Buffer ekstra (menit) setelah expiry Midtrans sebelum sweep menandai
    // order PENDING sebagai EXPIRED — memberi jalan bagi webhook Expire
    // Midtrans menangani kasus normal terlebih dahulu.
    private const int PendingSweepBufferMinutes = 5;

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

    /// <summary>
    /// Resume fulfillment untuk order PAID yang topup-nya belum selesai
    /// (crash/restart antara commit PAID dan panggilan Digiflazz).
    /// Pola sama dengan RetryOrderAsync: CheckStatus dulu, baru topup ulang.
    /// Semua transisi memakai SSOT OrderStateTransition (PAID → PROCESSING /
    /// COMPLETED / NEEDS_ACTION sudah legal) — tidak ada aturan baru.
    /// </summary>
    private async Task ResumeFulfillmentAsync(TopupOrder order, CancellationToken cancellationToken)
    {
        // 1. Cek status aktual di provider — topup mungkin sudah dieksekusi
        //    sebelum restart sebelumnya (jangan langsung kirim ulang).
        try
        {
            var (status, sn, message) = await _digiflazzProvider.CheckStatusAsync(
                order.BuyerSkuCode, order.PlayerId, order.OrderNo, cancellationToken);

            if (status == AppConstants.DigiflazzStatus.Sukses)
            {
                if (!TryTransition(order, AppConstants.OrderStatus.Completed)) return;
                order.Sn = sn;
                await _db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation(
                    "[SWEEP] Order {OrderNo} PAID terselesaikan — SN ditemukan di provider.", order.OrderNo);
                return;
            }

            if (status == AppConstants.DigiflazzStatus.Pending)
            {
                // Topup sudah diterima provider dan masih diproses — tick
                // berikutnya akan mengecek ulang.
                _logger.LogInformation(
                    "[SWEEP] Order {OrderNo} masih Pending di Digiflazz.", order.OrderNo);
                return;
            }

            // Gagal di provider → butuh aksi manual admin.
            if (!TryTransition(order, AppConstants.OrderStatus.NeedsAction)) return;
            await _db.SaveChangesAsync(cancellationToken);
            await SendSweepFailedAlertAsync(order, message, cancellationToken);
            return;
        }
        catch
        {
            // Balasan CheckStatus tidak bisa dibaca — indikasi kuat topup belum
            // pernah tercatat provider; lanjut kirim topup (resume) di bawah.
        }

        // 2. Kirim topup (resume fulfillment awal).
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            string? sn = await _digiflazzProvider.TopupAsync(
                order.BuyerSkuCode, order.PlayerId, order.OrderNo, cts.Token);

            if (!TryTransition(order, AppConstants.OrderStatus.Processing))
                return;

            if (!string.IsNullOrEmpty(sn))
            {
                if (!TryTransition(order, AppConstants.OrderStatus.Completed)) return;
                order.Sn = sn;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!TryTransition(order, AppConstants.OrderStatus.NeedsAction)) return;
            await _db.SaveChangesAsync(cancellationToken);
            await SendStuckOrderAlertAsync(order, "Digiflazz API timeout (30s).");
            _logger.LogWarning("[SWEEP] Resume {OrderNo} timeout — masuk NEEDS_ACTION.", order.OrderNo);
        }
        catch (Exception ex)
        {
            if (!TryTransition(order, AppConstants.OrderStatus.NeedsAction)) return;
            await _db.SaveChangesAsync(cancellationToken);
            await SendStuckOrderAlertAsync(order, ex.Message);
            _logger.LogError(ex, "[SWEEP] Gagal resume fulfillment {OrderNo}", order.OrderNo);
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

