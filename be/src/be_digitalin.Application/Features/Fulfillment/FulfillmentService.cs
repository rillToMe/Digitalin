using be_digitalin.Application.Ports;
using be_digitalin.Constants;
using be_digitalin.Domain;
using be_digitalin.Domain.Entities;
using be_digitalin.Domain.Orders;
using be_digitalin.DTOs;
using be_digitalin.Services.Commerce;
using be_digitalin.Services.Settings;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services.Orders;

/// <summary>
/// Use case pemenuhan pesanan (fulfillment) via Digiflazz, webhook callback Digiflazz,
/// manual retry/refund admin, eskalasi late-payment, serta rekonsiliasi background (sweep stuck orders).
/// </summary>
public sealed class FulfillmentService
{
    private const int PendingSweepBufferMinutes = 5;

    private readonly IApplicationDbContext _db;
    private readonly IMidtransProvider _midtransProvider;
    private readonly IDigiflazzProvider _digiflazzProvider;
    private readonly ITelegramProvider _telegramProvider;
    private readonly IVoucherService _voucherService;
    private readonly IAppSettingsService _appSettings;
    private readonly ILogger _logger;

    public FulfillmentService(
        IApplicationDbContext db,
        IMidtransProvider midtransProvider,
        IDigiflazzProvider digiflazzProvider,
        ITelegramProvider telegramProvider,
        IVoucherService voucherService,
        IAppSettingsService appSettings,
        ILogger logger)
    {
        _db = db;
        _midtransProvider = midtransProvider;
        _digiflazzProvider = digiflazzProvider;
        _telegramProvider = telegramProvider;
        _voucherService = voucherService;
        _appSettings = appSettings;
        _logger = logger;
    }

    /// <summary>
    /// Eksekusi panggilan top-up Digiflazz di luar transaksi database setelah status PAID ter-commit.
    /// Tidak pernah mengasumsikan sukses/PROCESSING dari nilai null, body kosong, atau ketiadaan exception.
    /// </summary>
    public async Task ExecutePostPaymentTopupAsync(TopupOrder order)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var outcome = await ClassifyTopupAttemptAsync(order, cts.Token);

        switch (outcome.Outcome)
        {
            case DigiflazzTransactionOutcome.Completed:
                if (!TryTransition(order, AppConstants.OrderStatus.Completed)) return;
                order.Sn = outcome.Sn;
                order.ProcessedAt = OrderFulfillmentState.CreateStandardTimestamp(order.ProcessedAt ?? DateTime.UtcNow);
                _logger.LogInformation("[WEBHOOK] SN sudah di terima {OrderNo}: {SN}", order.OrderNo, outcome.Sn);
                await SafeSaveChangesAsync(order.OrderNo);
                return;

            case DigiflazzTransactionOutcome.AcceptedPending:
                if (!TryTransition(order, AppConstants.OrderStatus.Processing)) return;
                order.ProcessedAt = OrderFulfillmentState.CreateStandardTimestamp(order.ProcessedAt ?? DateTime.UtcNow);
                await SafeSaveChangesAsync(order.OrderNo);
                return;

            case DigiflazzTransactionOutcome.TerminalFailure:
                _logger.LogCritical(
                    "[WEBHOOK] Digiflazz menolak (Gagal) pesanan {OrderNo}: {Message}",
                    order.OrderNo,
                    outcome.Message);
                if (!TryTransition(order, AppConstants.OrderStatus.NeedsAction)) return;
                order.ProcessedAt = OrderFulfillmentState.CreateConfirmedFailureTimestamp(order.ProcessedAt ?? DateTime.UtcNow);
                if (await SafeSaveChangesAsync(order.OrderNo))
                    await SendStuckOrderAlertAsync(order, outcome.Message);
                return;

            default:
                _logger.LogWarning(
                    "[WEBHOOK] Hasil panggilan Digiflazz tidak pasti untuk {OrderNo}: {Message}",
                    order.OrderNo,
                    outcome.Message);
                if (!TryTransition(order, AppConstants.OrderStatus.NeedsAction)) return;
                order.ProcessedAt = OrderFulfillmentState.CreateUncertainOutcomeTimestamp(order.ProcessedAt ?? DateTime.UtcNow);
                if (await SafeSaveChangesAsync(order.OrderNo))
                    await SendStuckOrderAlertAsync(order, outcome.Message);
                return;
        }
    }

    /// <summary>
    /// Eskalasi pembayaran terlambat (late payment) yang telah terverifikasi secara otoritatif
    /// pada pesanan berstatus EXPIRED atau FAILED ke NEEDS_ACTION tanpa auto-fulfillment,
    /// dilengkapi re-reservasi voucher atomik di dalam transaksi DB dan peringatan kritis ke operator.
    /// </summary>
    public async Task HandleVerifiedLatePaymentAsync(
        TopupOrder order,
        MidtransNotificationRequest? notification,
        MidtransTransactionStatus providerStatus,
        CancellationToken cancellationToken = default)
    {
        var previousStatus = order.Status;
        if (!OrderStateTransition.CanEscalateLatePayment(previousStatus, AppConstants.OrderStatus.NeedsAction))
        {
            return;
        }

        var resolvedPaymentType = !string.IsNullOrEmpty(order.PaymentType)
            ? order.PaymentType
            : (!string.IsNullOrEmpty(notification?.payment_type)
                ? notification.payment_type
                : providerStatus.PaymentType);

        var resolvedTransactionId = !string.IsNullOrEmpty(order.MidtransTransactionId)
            ? order.MidtransTransactionId
            : (!string.IsNullOrEmpty(notification?.transaction_id)
                ? notification.transaction_id
                : providerStatus.TransactionId);

        bool voucherReservedOrNotNeeded;
        string? voucherWarning;

        await using (var tx = await _db.Database.BeginTransactionAsync(cancellationToken))
        {
            (voucherReservedOrNotNeeded, voucherWarning) =
                await TryReReserveVoucherForLatePaymentAsync(order);

            DateTime? processedAt = voucherReservedOrNotNeeded
                ? OrderFulfillmentState.CreateNeverSubmittedTimestamp(DateTime.UtcNow)
                : null;

            var rowsAffected = await _db.TopupOrders
                .Where(o =>
                    o.Id == order.Id &&
                    (o.Status == AppConstants.OrderStatus.Expired || o.Status == AppConstants.OrderStatus.Failed))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(o => o.Status, AppConstants.OrderStatus.NeedsAction)
                        .SetProperty(o => o.ProcessedAt, processedAt)
                        .SetProperty(o => o.PaymentType, resolvedPaymentType)
                        .SetProperty(o => o.MidtransTransactionId, resolvedTransactionId),
                    cancellationToken);

            if (rowsAffected == 0)
            {
                await tx.RollbackAsync(cancellationToken);
                await _db.Entry(order).ReloadAsync(cancellationToken);
                return;
            }

            await tx.CommitAsync(cancellationToken);
        }

        await _db.Entry(order).ReloadAsync(cancellationToken);

        _logger.LogCritical(
            "[LATE PAYMENT] Pembayaran terlambat terverifikasi untuk Order {OrderNo}! " +
            "Status sebelumnya: {PreviousStatus} → NEEDS_ACTION. Nominal: Rp{TotalPaid:N0}, " +
            "MidtransStatus: {PaymentStatus}, TxId: {TransactionId}, VoucherNote: {VoucherNote}",
            order.OrderNo,
            previousStatus,
            order.TotalPaid,
            providerStatus.TransactionStatus,
            resolvedTransactionId,
            voucherWarning ?? "OK");

        await SendLatePaymentAlertAsync(
            order,
            previousStatus,
            providerStatus.TransactionStatus ?? notification?.transaction_status ?? AppConstants.MidtransStatus.Settlement,
            voucherWarning,
            cancellationToken);
    }

    public async Task<OrderActionResult> RetryOrderAsync(string orderNo)
    {
        var order = await _db.TopupOrders
            .FirstOrDefaultAsync(o => o.OrderNo == orderNo);

        if (order == null)
            return new OrderActionResult(OrderActionOutcome.NotFound, "Pesanan tidak ditemukan");

        if (order.Status != AppConstants.OrderStatus.NeedsAction)
            return new OrderActionResult(
                OrderActionOutcome.InvalidState,
                $"Tidak bisa retry. Status pesanan saat ini adalah: {order.Status}");

        if (OrderFulfillmentState.HasUnfulfilledVoucherRequirement(order))
        {
            _logger.LogWarning(
                "[RETRY] Ditolak untuk Order {OrderNo}: kuota voucher {VoucherCode} sudah dilepas saat EXPIRED/FAILED dan gagal di-reserve ulang.",
                order.OrderNo, order.VoucherCode);
            return new OrderActionResult(
                OrderActionOutcome.InvalidState,
                "Kuota voucher pada pesanan terlambat bayar ini sudah habis atau tidak lagi valid setelah pesanan kedaluwarsa. Silakan proses Refund manual.");
        }

        var supplierState = OrderFulfillmentState.GetSupplierState(order);

        if (supplierState == SupplierFulfillmentState.ConfirmedFailure)
        {
            _logger.LogWarning(
                "[RETRY] Ditolak untuk Order {OrderNo}: Digiflazz telah mengonfirmasi status Gagal untuk ref_id yang sama.",
                order.OrderNo);
            return new OrderActionResult(
                OrderActionOutcome.InvalidState,
                "Digiflazz telah mengonfirmasi status Gagal untuk ref_id pesanan ini. Pengiriman ulang dengan ref_id yang sama tidak dapat membuat transaksi baru; silakan proses Refund manual.");
        }

        if (supplierState == SupplierFulfillmentState.NeverSubmitted)
        {
            return await ExecuteSingleInitialSubmissionFromRetryAsync(order);
        }

        return await ReconcileUncertainSupplierOrderOnRetryAsync(order);
    }

    /// <summary>
    /// Untuk pesanan di <c>NEEDS_ACTION</c> yang belum pernah dikirim ke Digiflazz (mis. late payment
    /// yang berhasil melakukan re-reservasi voucher atau tidak memakai voucher): klaim pesanan ke
    /// <c>PROCESSING</c> secara atomik lalu panggil <c>TopupAsync</c> tepat satu kali tanpa memanggil
    /// <c>CheckStatusAsync</c> terlebih dahulu.
    /// </summary>
    private async Task<OrderActionResult> ExecuteSingleInitialSubmissionFromRetryAsync(TopupOrder order)
    {
        var claimedTimestamp = OrderFulfillmentState.CreateStandardTimestamp(DateTime.UtcNow);
        var claimedRows = await _db.TopupOrders
            .Where(o =>
                o.Id == order.Id &&
                o.Status == AppConstants.OrderStatus.NeedsAction &&
                o.ProcessedAt == order.ProcessedAt)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.Status, AppConstants.OrderStatus.Processing)
                .SetProperty(o => o.ProcessedAt, claimedTimestamp));

        if (claimedRows == 0)
        {
            await _db.Entry(order).ReloadAsync();
            return new OrderActionResult(
                OrderActionOutcome.Conflict,
                "Pesanan sedang diproses oleh sistem lain. Coba lagi beberapa saat.");
        }

        await _db.Entry(order).ReloadAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var outcome = await ClassifyTopupAttemptAsync(order, cts.Token);

        switch (outcome.Outcome)
        {
            case DigiflazzTransactionOutcome.Completed:
            {
                var completedTimestamp = OrderFulfillmentState.CreateStandardTimestamp(DateTime.UtcNow);
                await _db.TopupOrders
                    .Where(o => o.Id == order.Id && o.Status == AppConstants.OrderStatus.Processing)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(o => o.Status, AppConstants.OrderStatus.Completed)
                        .SetProperty(o => o.Sn, outcome.Sn)
                        .SetProperty(o => o.ProcessedAt, completedTimestamp));

                await _db.Entry(order).ReloadAsync();
                return new OrderActionResult(
                    OrderActionOutcome.Success,
                    "Retry berhasil dikirim ke Digiflazz dan SN berhasil diterima.",
                    order.Status);
            }

            case DigiflazzTransactionOutcome.AcceptedPending:
            {
                return new OrderActionResult(
                    OrderActionOutcome.Success,
                    "Retry berhasil dikirim ke Digiflazz! Pesanan sedang diproses (PROCESSING), silakan tunggu SN masuk.",
                    order.Status);
            }

            case DigiflazzTransactionOutcome.TerminalFailure:
            {
                var failedTimestamp = OrderFulfillmentState.CreateConfirmedFailureTimestamp(DateTime.UtcNow);
                await _db.TopupOrders
                    .Where(o => o.Id == order.Id && o.Status == AppConstants.OrderStatus.Processing)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(o => o.Status, AppConstants.OrderStatus.NeedsAction)
                        .SetProperty(o => o.ProcessedAt, failedTimestamp));

                await _db.Entry(order).ReloadAsync();
                return new OrderActionResult(
                    OrderActionOutcome.Error,
                    $"Pengiriman ke Digiflazz ditolak (Gagal): {outcome.Message}. Silakan proses Refund manual.");
            }

            default:
            {
                var uncertainTimestamp = OrderFulfillmentState.CreateUncertainOutcomeTimestamp(DateTime.UtcNow);
                await _db.TopupOrders
                    .Where(o => o.Id == order.Id && o.Status == AppConstants.OrderStatus.Processing)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(o => o.Status, AppConstants.OrderStatus.NeedsAction)
                        .SetProperty(o => o.ProcessedAt, uncertainTimestamp));

                await _db.Entry(order).ReloadAsync();
                return new OrderActionResult(
                    OrderActionOutcome.Error,
                    "Respons Digiflazz belum pasti (timeout/gangguan jaringan). Pesanan ditahan untuk rekonsiliasi; jangan refund sebelum status pasti.");
            }
        }
    }

    /// <summary>
    /// Untuk pesanan di <c>NEEDS_ACTION</c> yang pernah dikirim ke Digiflazz dengan hasil belum pasti
    /// (<see cref="SupplierFulfillmentState.UncertainOutcome"/>): panggil <c>CheckStatusAsync</c>
    /// maksimal satu kali untuk rekonsiliasi dan JANGAN pernah memanggil <c>TopupAsync</c> setelahnya.
    /// </summary>
    private async Task<OrderActionResult> ReconcileUncertainSupplierOrderOnRetryAsync(TopupOrder order)
    {
        string upstreamStatus;
        string upstreamSn;
        string upstreamMessage;
        try
        {
            (upstreamStatus, upstreamSn, upstreamMessage) = await _digiflazzProvider.CheckStatusAsync(
                order.BuyerSkuCode, order.PlayerId, order.OrderNo);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[RETRY] CheckStatus Digiflazz gagal untuk Order {OrderNo}. Retry dibatalkan (fail-closed) demi mencegah top-up ganda.",
                order.OrderNo);
            return new OrderActionResult(
                OrderActionOutcome.Error,
                "Gagal memeriksa status pesanan di Digiflazz. Retry dibatalkan untuk mencegah pengiriman ganda; silakan coba lagi beberapa saat.");
        }

        try
        {
            if (upstreamStatus == AppConstants.DigiflazzStatus.Sukses)
            {
                if (string.IsNullOrWhiteSpace(upstreamSn))
                {
                    _logger.LogWarning(
                        "[RETRY] CheckStatus Digiflazz mengembalikan Sukses tanpa SN untuk Order {OrderNo}. Tetap NEEDS_ACTION (fail-closed).",
                        order.OrderNo);
                    return new OrderActionResult(
                        OrderActionOutcome.Error,
                        "Digiflazz mengembalikan status Sukses tanpa nomor seri (SN). Rekonsiliasi ditahan; silakan cek lagi beberapa saat.");
                }

                var completedAt = OrderFulfillmentState.CreateStandardTimestamp(DateTime.UtcNow);
                var updated = await _db.TopupOrders
                    .Where(o => o.Id == order.Id && o.Status == AppConstants.OrderStatus.NeedsAction)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(o => o.Status, AppConstants.OrderStatus.Completed)
                        .SetProperty(o => o.Sn, upstreamSn.Trim())
                        .SetProperty(o => o.ProcessedAt, completedAt));

                if (updated == 0)
                {
                    return new OrderActionResult(
                        OrderActionOutcome.Conflict,
                        "Pesanan sedang diproses oleh sistem lain. Coba lagi beberapa saat.");
                }

                await _db.Entry(order).ReloadAsync();
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

            if (upstreamStatus == AppConstants.DigiflazzStatus.Gagal)
            {
                var confirmedFailureAt = OrderFulfillmentState.CreateConfirmedFailureTimestamp(DateTime.UtcNow);
                await _db.TopupOrders
                    .Where(o => o.Id == order.Id && o.Status == AppConstants.OrderStatus.NeedsAction)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(o => o.ProcessedAt, confirmedFailureAt));

                await _db.Entry(order).ReloadAsync();

                _logger.LogWarning(
                    "[RETRY] CheckStatus Digiflazz mengonfirmasi status Gagal untuk Order {OrderNo} ({Message}). TopupAsync tidak dipanggil ulang; pesanan siap di-refund manual.",
                    order.OrderNo, upstreamMessage);

                return new OrderActionResult(
                    OrderActionOutcome.Error,
                    "Digiflazz mengonfirmasi pesanan ini berstatus Gagal. Pengiriman ulang dengan ref_id yang sama tidak dapat dilakukan; silakan proses Refund manual.");
            }

            _logger.LogWarning(
                "[RETRY] CheckStatus Digiflazz mengembalikan status tidak dikenal '{UpstreamStatus}' untuk Order {OrderNo}. Retry dibatalkan (fail-closed).",
                upstreamStatus, order.OrderNo);
            return new OrderActionResult(
                OrderActionOutcome.Error,
                $"Status pesanan di Digiflazz belum pasti ({upstreamStatus}). Retry dibatalkan untuk mencegah pengiriman ganda.");
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
            return new OrderActionResult(
                OrderActionOutcome.Error,
                "Retry ke Digiflazz gagal. Silakan coba lagi atau hubungi tim teknis.");
        }
    }

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

            if (order.Status != AppConstants.OrderStatus.NeedsAction)
            {
                _logger.LogWarning(
                    "[REFUND WARNING] Ditolak: Order {OrderNo} berstatus '{Status}'. Hanya status '{Required}' yang bisa di-refund.",
                    orderNo, order.Status, AppConstants.OrderStatus.NeedsAction);
                return new OrderActionResult(
                    OrderActionOutcome.InvalidState,
                    $"Tidak bisa di-refund. Status pesanan saat ini: {order.Status}");
            }

            if (OrderFulfillmentState.IsSupplierOutcomeUncertain(order))
            {
                _logger.LogWarning(
                    "[REFUND WARNING] Ditolak: Order {OrderNo} memiliki status supplier yang belum pasti (UncertainOutcome). Rekonsiliasi diperlukan sebelum refund.",
                    orderNo);
                return new OrderActionResult(
                    OrderActionOutcome.InvalidState,
                    "Status transaksi di Digiflazz belum pasti (timeout/pending/respons ambigu). Gunakan Retry untuk rekonsiliasi status terlebih dahulu sebelum melakukan Refund.");
            }

            bool shouldReleaseVoucher = OrderFulfillmentState.HasActiveVoucherReservation(order);
            var expectedProcessedAt = order.ProcessedAt;

            await using (var tx = await _db.Database.BeginTransactionAsync())
            {
                var rowsAffected = await _db.TopupOrders
                    .Where(o =>
                        o.Id == order.Id &&
                        o.Status == AppConstants.OrderStatus.NeedsAction &&
                        o.ProcessedAt == expectedProcessedAt)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(o => o.Status, AppConstants.OrderStatus.Refunded)
                        .SetProperty(o => o.ProcessedAt, (DateTime?)null));

                if (rowsAffected == 0)
                {
                    await tx.RollbackAsync();
                    _logger.LogWarning("[REFUND CONCURRENCY] Order {OrderNo} diubah oleh proses lain sebelum refund disimpan.", orderNo);
                    return new OrderActionResult(
                        OrderActionOutcome.Conflict,
                        "Pesanan sedang diproses oleh sistem lain. Coba lagi beberapa saat.");
                }

                if (shouldReleaseVoucher)
                {
                    await ReleaseVoucherIfPresentAsync(order);
                }

                await tx.CommitAsync();
            }

            await _db.Entry(order).ReloadAsync();

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

    public async Task<string> HandleDigiflazzWebhookAsync(DigiflazzWebhookData data)
    {
        _logger.LogInformation("[WEBHOOK] Pesanan: {RefId} | Status: {Status} | SN: {SN}",
            data.ref_id, data.status, data.sn);

        var order = await _db.TopupOrders.FirstOrDefaultAsync(o => o.OrderNo == data.ref_id);
        if (order == null)
            return "Webhook Digiflazz diterima";

        string? targetStatus = data.status switch
        {
            AppConstants.DigiflazzStatus.Sukses => AppConstants.OrderStatus.Completed,
            AppConstants.DigiflazzStatus.Gagal => AppConstants.OrderStatus.NeedsAction,
            _ => null
        };

        if (targetStatus == null)
            return "Webhook Digiflazz diterima";

        if (order.Status == targetStatus)
        {
            _logger.LogWarning(
                "[WEBHOOK] Duplicate {Status} webhook untuk {OrderNo}. Diabaikan.",
                targetStatus, order.OrderNo);
            return "Webhook Digiflazz received (already processed)";
        }

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
            order.ProcessedAt = OrderFulfillmentState.CreateStandardTimestamp(order.ProcessedAt ?? DateTime.UtcNow);
            _logger.LogInformation("[WEBHOOK] SUCCESS! SN berhasil ditanam ke database");
        }
        else
        {
            order.Status = AppConstants.OrderStatus.NeedsAction;
            order.ProcessedAt = OrderFulfillmentState.CreateConfirmedFailureTimestamp(order.ProcessedAt ?? DateTime.UtcNow);
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

    public async Task SweepStuckOrdersAsync(CancellationToken cancellationToken = default)
    {
        var timeThreshold = DateTime.UtcNow.AddMinutes(-3);
        int midtransExpiryMinutes = await _appSettings.GetMidtransExpiryMinutesAsync();
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

        if (stuckOrders.Count == 0) return;

        _logger.LogInformation(
            "[SWEEP] Menemukan {Count} pesanan nyangkut. Memulai rekonsiliasi...",
            stuckOrders.Count);

        foreach (var order in stuckOrders)
        {
            try
            {
                if (order.Status == AppConstants.OrderStatus.Paid)
                {
                    await ResumeFulfillmentAsync(order, cancellationToken);
                    continue;
                }

                if (order.Status == AppConstants.OrderStatus.Pending)
                {
                    await SweepPendingOrderAsync(order, midtransExpiryMinutes, cancellationToken);
                    continue;
                }

                var (status, sn, message) = await _digiflazzProvider.CheckStatusAsync(
                    order.BuyerSkuCode, order.PlayerId, order.OrderNo, cancellationToken);

                if (status == AppConstants.DigiflazzStatus.Sukses)
                {
                    if (string.IsNullOrWhiteSpace(sn))
                    {
                        _logger.LogWarning(
                            "[SWEEP] Order {OrderNo} menerima status Sukses tanpa SN dari Digiflazz. Dilewati untuk siklus berikutnya.",
                            order.OrderNo);
                        continue;
                    }

                    if (!TryTransition(order, AppConstants.OrderStatus.Completed)) continue;
                    order.Sn = sn.Trim();
                    order.ProcessedAt = OrderFulfillmentState.CreateStandardTimestamp(order.ProcessedAt ?? DateTime.UtcNow);
                    _logger.LogInformation("[SWEEP] Order {OrderNo} ternyata SUKSES. Data SN diperbarui.", order.OrderNo);
                }
                else if (status == AppConstants.DigiflazzStatus.Gagal)
                {
                    if (!TryTransition(order, AppConstants.OrderStatus.NeedsAction)) continue;
                    order.ProcessedAt = OrderFulfillmentState.CreateConfirmedFailureTimestamp(order.ProcessedAt ?? DateTime.UtcNow);
                    _logger.LogWarning("[SWEEP] Order {OrderNo} dipastikan GAGAL dari pusat. Mengunci pesanan...", order.OrderNo);
                    await SendSweepFailedAlertAsync(order, message, cancellationToken);
                }
                else if (status == AppConstants.DigiflazzStatus.Pending)
                {
                    var maxToleransi = DateTime.UtcNow.AddMinutes(-15);

                    if (order.OrderDate <= maxToleransi)
                    {
                        if (!TryTransition(order, AppConstants.OrderStatus.NeedsAction)) continue;
                        order.ProcessedAt = OrderFulfillmentState.CreateUncertainOutcomeTimestamp(order.ProcessedAt ?? DateTime.UtcNow);
                        _logger.LogWarning("[SWEEP] Order {OrderNo} kelamaan Pending di Digiflazz (>15 menit). Force Needs Action!", order.OrderNo);
                        await SendSweepTimeoutAlertAsync(order, cancellationToken);
                    }
                    else
                    {
                        _logger.LogInformation("[SWEEP] Order {OrderNo} masih Pending dari pusat. Sabar, tunggu putaran selanjutnya.", order.OrderNo);
                        continue;
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "[SWEEP] Order {OrderNo} menerima status Digiflazz tidak dikenal '{Status}'. Dilewati.",
                        order.OrderNo, status);
                    continue;
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

    private async Task SweepPendingOrderAsync(
        TopupOrder order,
        int midtransExpiryMinutes,
        CancellationToken cancellationToken)
    {
        MidtransTransactionStatus? providerStatus;
        try
        {
            providerStatus = await _midtransProvider.GetStatusAsync(order.OrderNo, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[SWEEP] Gagal mengecek status Midtrans untuk Order {OrderNo} yang melewati cutoff. Lewati siklus ini (tetap PENDING).",
                order.OrderNo);
            return;
        }

        if (providerStatus == null)
        {
            await TryMarkPendingOrderTerminalAsync(
                order,
                AppConstants.OrderStatus.Expired,
                order.PaymentType,
                order.MidtransTransactionId,
                cancellationToken);

            _logger.LogInformation(
                "[SWEEP] Order {OrderNo} PENDING melewati expiry ({Expiry}+{Buffer} menit) dan tidak ditemukan di Midtrans (404) → EXPIRED.",
                order.OrderNo, midtransExpiryMinutes, PendingSweepBufferMinutes);
            return;
        }

        var apiStatus = providerStatus.TransactionStatus;

        if (string.Equals(apiStatus, AppConstants.MidtransStatus.Settlement, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(apiStatus, AppConstants.MidtransStatus.Capture, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(providerStatus.FraudStatus, AppConstants.MidtransFraud.Challenge, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "[SWEEP] Order {OrderNo} berstatus {ApiStatus} dengan fraud_status=challenge di Midtrans. Tetap PENDING menunggu hasil review.",
                    order.OrderNo, apiStatus);
                return;
            }

            bool validIdentity =
                string.Equals(providerStatus.OrderId, order.OrderNo, StringComparison.Ordinal) &&
                (string.IsNullOrWhiteSpace(order.MidtransTransactionId) ||
                 (!string.IsNullOrWhiteSpace(providerStatus.TransactionId) &&
                  string.Equals(order.MidtransTransactionId, providerStatus.TransactionId, StringComparison.Ordinal)));

            bool validPayment =
                validIdentity &&
                PaymentWebhookService.IsPositivePaymentAndFraudAcceptable(apiStatus, providerStatus.FraudStatus) &&
                Pricing.IsExactRupiahMatch(providerStatus.GrossAmount, order.TotalPaid);

            if (validPayment)
            {
                var resolvedPaymentType = !string.IsNullOrEmpty(order.PaymentType)
                    ? order.PaymentType
                    : providerStatus.PaymentType;
                var resolvedTransactionId = !string.IsNullOrEmpty(order.MidtransTransactionId)
                    ? order.MidtransTransactionId
                    : providerStatus.TransactionId;
                var processedAt = OrderFulfillmentState.CreateStandardTimestamp(DateTime.UtcNow);

                int rowsAffected;
                await using (var tx = await _db.Database.BeginTransactionAsync(cancellationToken))
                {
                    rowsAffected = await _db.TopupOrders
                        .Where(o => o.Id == order.Id && o.Status == AppConstants.OrderStatus.Pending)
                        .ExecuteUpdateAsync(
                            setters => setters
                                .SetProperty(o => o.Status, AppConstants.OrderStatus.Paid)
                                .SetProperty(o => o.ProcessedAt, processedAt)
                                .SetProperty(o => o.PaymentType, resolvedPaymentType)
                                .SetProperty(o => o.MidtransTransactionId, resolvedTransactionId),
                            cancellationToken);

                    await tx.CommitAsync(cancellationToken);
                }

                await _db.Entry(order).ReloadAsync(cancellationToken);

                if (rowsAffected == 1)
                {
                    _logger.LogInformation(
                        "[SWEEP] Order {OrderNo} ternyata sudah lunas ({ApiStatus}) di Midtrans. Melanjutkan ke fulfillment.",
                        order.OrderNo, apiStatus);
                    await ExecutePostPaymentTopupAsync(order);
                }
                return;
            }

            _logger.LogCritical(
                "[SWEEP] Order {OrderNo} berstatus {ApiStatus} di Midtrans namun gagal validasi otoritatif " +
                "(OrderId={ApiOrderId}, TxId={ApiTxId}, Fraud={ApiFraud}, Amount={ApiAmount}, Expected={Expected}). Eskalasi ke NEEDS_ACTION.",
                order.OrderNo, apiStatus, providerStatus.OrderId, providerStatus.TransactionId,
                providerStatus.FraudStatus, providerStatus.GrossAmount, order.TotalPaid);

            var neverSubmittedAt = OrderFulfillmentState.CreateNeverSubmittedTimestamp(DateTime.UtcNow);
            var escalatedRows = await _db.TopupOrders
                .Where(o => o.Id == order.Id && o.Status == AppConstants.OrderStatus.Pending)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(o => o.Status, AppConstants.OrderStatus.NeedsAction)
                        .SetProperty(o => o.ProcessedAt, neverSubmittedAt),
                    cancellationToken);

            await _db.Entry(order).ReloadAsync(cancellationToken);
            if (escalatedRows == 1)
            {
                await SendStuckOrderAlertAsync(
                    order,
                    $"Rekonsiliasi Midtrans mendeteksi ketidaksesuaian pada status {apiStatus} (Fraud={providerStatus.FraudStatus}, GrossAmount={providerStatus.GrossAmount}).");
            }
            return;
        }

        if (string.Equals(apiStatus, AppConstants.MidtransStatus.Pending, StringComparison.OrdinalIgnoreCase))
        {
            bool expiredUpstream;
            try
            {
                expiredUpstream = await _midtransProvider.ExpireAsync(order.OrderNo, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[SWEEP] Gagal memanggil Midtrans ExpireAsync untuk Order {OrderNo}. Lewati siklus ini (tetap PENDING).",
                    order.OrderNo);
                return;
            }

            if (!expiredUpstream)
            {
                _logger.LogWarning(
                    "[SWEEP] Midtrans menolak ExpireAsync untuk Order {OrderNo} (status upstream mungkin baru berubah). Tunggu siklus rekonsiliasi berikutnya.",
                    order.OrderNo);
                return;
            }

            await TryMarkPendingOrderTerminalAsync(
                order,
                AppConstants.OrderStatus.Expired,
                order.PaymentType ?? providerStatus.PaymentType,
                order.MidtransTransactionId ?? providerStatus.TransactionId,
                cancellationToken);

            _logger.LogInformation(
                "[SWEEP] Order {OrderNo} PENDING berhasil di-expire di Midtrans ({Expiry}+{Buffer} menit) → EXPIRED.",
                order.OrderNo, midtransExpiryMinutes, PendingSweepBufferMinutes);
            return;
        }

        if (string.Equals(apiStatus, AppConstants.MidtransStatus.Expire, StringComparison.OrdinalIgnoreCase))
        {
            await TryMarkPendingOrderTerminalAsync(
                order,
                AppConstants.OrderStatus.Expired,
                order.PaymentType ?? providerStatus.PaymentType,
                order.MidtransTransactionId ?? providerStatus.TransactionId,
                cancellationToken);

            _logger.LogInformation(
                "[SWEEP] Order {OrderNo} sudah berstatus expire di Midtrans → EXPIRED.",
                order.OrderNo);
            return;
        }

        if (string.Equals(apiStatus, AppConstants.MidtransStatus.Cancel, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(apiStatus, AppConstants.MidtransStatus.Deny, StringComparison.OrdinalIgnoreCase))
        {
            await TryMarkPendingOrderTerminalAsync(
                order,
                AppConstants.OrderStatus.Failed,
                order.PaymentType ?? providerStatus.PaymentType,
                order.MidtransTransactionId ?? providerStatus.TransactionId,
                cancellationToken);

            _logger.LogInformation(
                "[SWEEP] Order {OrderNo} berstatus {ApiStatus} di Midtrans → FAILED.",
                order.OrderNo, apiStatus);
            return;
        }

        _logger.LogWarning(
            "[SWEEP] Order {OrderNo} memiliki status Midtrans yang tidak dikenal '{ApiStatus}'. Tetap PENDING.",
            order.OrderNo, apiStatus);
    }

    internal async Task<bool> TryMarkPendingOrderTerminalAsync(
        TopupOrder order,
        string terminalStatus,
        string? paymentType,
        string? transactionId,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var rowsAffected = await _db.TopupOrders
            .Where(o => o.Id == order.Id && o.Status == AppConstants.OrderStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(o => o.Status, terminalStatus)
                    .SetProperty(o => o.ProcessedAt, (DateTime?)null)
                    .SetProperty(o => o.PaymentType, paymentType)
                    .SetProperty(o => o.MidtransTransactionId, transactionId),
                cancellationToken);

        if (rowsAffected == 0)
        {
            await tx.RollbackAsync(cancellationToken);
            await _db.Entry(order).ReloadAsync(cancellationToken);
            return false;
        }

        await ReleaseVoucherIfPresentAsync(order);

        await tx.CommitAsync(cancellationToken);
        await _db.Entry(order).ReloadAsync(cancellationToken);
        return true;
    }

    private async Task<(bool VoucherReservedOrNotNeeded, string? VoucherWarning)> TryReReserveVoucherForLatePaymentAsync(
        TopupOrder order)
    {
        if (string.IsNullOrWhiteSpace(order.VoucherCode))
            return (true, null);

        var (voucher, error) = await _voucherService.ValidateVoucherAsync(
            order.VoucherCode, order.OriginalPrice);
        if (error != null || voucher == null)
        {
            return (false, error ?? "Voucher tidak ditemukan saat re-reservasi late payment.");
        }

        try
        {
            await _voucherService.ReserveVoucherAsync(voucher);
            return (true, null);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "[LATE PAYMENT] Gagal re-reservasi voucher {VoucherCode} untuk Order {OrderNo}.",
                order.VoucherCode, order.OrderNo);
            return (false, ex.Message);
        }
    }

    private async Task<DigiflazzTransactionResult> ClassifyTopupAttemptAsync(
        TopupOrder order,
        CancellationToken cancellationToken)
    {
        try
        {
            string? sn = await _digiflazzProvider.TopupAsync(
                order.BuyerSkuCode,
                order.PlayerId,
                order.OrderNo,
                cancellationToken);

            // Fail-closed: JANGAN pernah menyimpulkan sukses atau PROCESSING dari nilai null.
            if (sn is null)
            {
                return DigiflazzTransactionResult.Uncertain(
                    "Digiflazz TopupAsync mengembalikan null tanpa konfirmasi status eksplisit.");
            }

            if (sn.Length == 0)
            {
                return DigiflazzTransactionResult.AcceptedPending();
            }

            if (!string.IsNullOrWhiteSpace(sn))
            {
                return DigiflazzTransactionResult.Completed(sn.Trim());
            }

            return DigiflazzTransactionResult.Uncertain(
                "Digiflazz TopupAsync mengembalikan nomor seri (SN) kosong/whitespace.");
        }
        catch (DigiflazzTerminalFailureException ex)
        {
            return DigiflazzTransactionResult.TerminalFailure(ex.Message);
        }
        catch (DigiflazzUncertainOutcomeException ex)
        {
            return DigiflazzTransactionResult.Uncertain(ex.Message);
        }
        catch (OperationCanceledException)
        {
            return DigiflazzTransactionResult.Uncertain("Digiflazz API timeout (30s).");
        }
        catch (Exception ex)
        {
            return DigiflazzTransactionResult.Uncertain(ex.Message);
        }
    }

    internal bool TryTransition(TopupOrder order, string targetStatus)
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

    internal async Task<bool> SafeSaveChangesAsync(string orderNo)
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

    internal async Task ReleaseVoucherIfPresentAsync(TopupOrder order)
    {
        if (string.IsNullOrWhiteSpace(order.VoucherCode))
            return;

        await _voucherService.ReleaseVoucherAsync(order.VoucherCode);
    }

    private async Task ResumeFulfillmentAsync(TopupOrder order, CancellationToken cancellationToken)
    {
        string status;
        string sn;
        string message;

        try
        {
            (status, sn, message) = await _digiflazzProvider.CheckStatusAsync(
                order.BuyerSkuCode, order.PlayerId, order.OrderNo, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[SWEEP] Gagal memverifikasi CheckStatus Digiflazz saat resume Order {OrderNo}. Eskalasi ke NEEDS_ACTION tanpa mengirim ulang TopupAsync (fail-closed).",
                order.OrderNo);

            if (!TryTransition(order, AppConstants.OrderStatus.NeedsAction)) return;
            order.ProcessedAt = OrderFulfillmentState.CreateUncertainOutcomeTimestamp(order.ProcessedAt ?? DateTime.UtcNow);
            if (await SafeSaveChangesAsync(order.OrderNo))
            {
                await SendStuckOrderAlertAsync(
                    order,
                    $"Gagal memeriksa status pesanan di Digiflazz ({ex.Message}). Pengiriman otomatis ditahan untuk mencegah top-up ganda.");
            }
            return;
        }

        if (status == AppConstants.DigiflazzStatus.Sukses && !string.IsNullOrWhiteSpace(sn))
        {
            if (!TryTransition(order, AppConstants.OrderStatus.Completed)) return;
            order.Sn = sn.Trim();
            order.ProcessedAt = OrderFulfillmentState.CreateStandardTimestamp(order.ProcessedAt ?? DateTime.UtcNow);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "[SWEEP] Order {OrderNo} PAID terselesaikan — SN ditemukan di provider.", order.OrderNo);
            return;
        }

        if (status == AppConstants.DigiflazzStatus.Pending)
        {
            if (!TryTransition(order, AppConstants.OrderStatus.Processing)) return;
            order.ProcessedAt = OrderFulfillmentState.CreateStandardTimestamp(order.ProcessedAt ?? DateTime.UtcNow);
            await SafeSaveChangesAsync(order.OrderNo);
            _logger.LogInformation(
                "[SWEEP] Order {OrderNo} sedang diproses (Pending) di Digiflazz → PROCESSING.", order.OrderNo);
            return;
        }

        if (status == AppConstants.DigiflazzStatus.Gagal)
        {
            if (!TryTransition(order, AppConstants.OrderStatus.NeedsAction)) return;
            order.ProcessedAt = OrderFulfillmentState.CreateConfirmedFailureTimestamp(order.ProcessedAt ?? DateTime.UtcNow);
            await _db.SaveChangesAsync(cancellationToken);
            await SendSweepFailedAlertAsync(order, message, cancellationToken);
            return;
        }

        _logger.LogWarning(
            "[SWEEP] CheckStatus Digiflazz mengembalikan status tidak dikenal '{Status}' untuk Order {OrderNo}. Eskalasi ke NEEDS_ACTION tanpa TopupAsync (fail-closed).",
            status, order.OrderNo);

        if (!TryTransition(order, AppConstants.OrderStatus.NeedsAction)) return;
        order.ProcessedAt = OrderFulfillmentState.CreateUncertainOutcomeTimestamp(order.ProcessedAt ?? DateTime.UtcNow);
        if (await SafeSaveChangesAsync(order.OrderNo))
        {
            await SendStuckOrderAlertAsync(
                order,
                $"Status Digiflazz tidak dikenal ({status}: {message}). Pengiriman otomatis ditahan untuk mencegah top-up ganda.");
        }
    }

    private async Task SendLatePaymentAlertAsync(
        TopupOrder order,
        string previousStatus,
        string paymentStatus,
        string? voucherWarning,
        CancellationToken cancellationToken)
    {
        var voucherSection = string.IsNullOrWhiteSpace(voucherWarning)
            ? string.Empty
            : $"\n• <b>Peringatan Voucher:</b> {voucherWarning} (Wajib Refund Manual)";

        string alertMessage = $@"🚨 <b>PEMBAYARAN TERLAMBAT (LATE PAYMENT)</b> 🚨

Halo Admin, sistem mendeteksi pembayaran Midtrans yang masuk <b>setelah</b> pesanan berstatus <b>{previousStatus}</b>. Pesanan telah dikunci ke <b>NEEDS_ACTION</b> (tidak dikirim otomatis).

<b>📝 Detail Pesanan:</b>
• <b>Invoice:</b> {order.OrderNo}
• <b>Produk:</b> {order.ProductName}
• <b>ID Tujuan:</b> {order.PlayerId}
• <b>Nominal Dibayar:</b> Rp{order.TotalPaid:N0}
• <b>Status Sebelumnya:</b> {previousStatus}
• <b>Status Pembayaran:</b> {paymentStatus}{voucherSection}

Silakan buka Admin Panel untuk memeriksa pesanan ini dan lakukan <b>Retry</b> atau <b>Refund</b> manual.";

        try
        {
            await _telegramProvider.SendNotificationAsync(alertMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Telegram] Gagal mengirim peringatan late-payment untuk {OrderNo}", order.OrderNo);
        }
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
