using be_digitalin.Application.Ports;
using be_digitalin.Constants;
using be_digitalin.Domain;
using be_digitalin.Domain.Entities;
using be_digitalin.Domain.Orders;
using be_digitalin.DTOs;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services.Orders;

/// <summary>
/// Use case pemrosesan webhook pembayaran Midtrans: verifikasi signature SHA-512,
/// guard nominal bulat Rupiah, verifikasi otoritatif wajib (fail-closed) ke Midtrans Status API,
/// eskalasi late-payment pada order EXPIRED/FAILED ke NEEDS_ACTION, serta
/// transisi atomik PENDING → PAID di dalam transaksi DB sebelum pemicu fulfillment.
/// </summary>
public sealed class PaymentWebhookService
{
    private readonly IApplicationDbContext _db;
    private readonly IMidtransProvider _midtransProvider;
    private readonly FulfillmentService _fulfillment;
    private readonly ILogger _logger;

    public PaymentWebhookService(
        IApplicationDbContext db,
        IMidtransProvider midtransProvider,
        FulfillmentService fulfillment,
        ILogger logger)
    {
        _db = db;
        _midtransProvider = midtransProvider;
        _fulfillment = fulfillment;
        _logger = logger;
    }

    public async Task<bool> HandleNotificationAsync(MidtransNotificationRequest notification)
    {
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

        var order = await _db.TopupOrders
            .FirstOrDefaultAsync(o => o.OrderNo == notification.order_id);

        if (order == null) return true;

        bool isPositiveNotificationStatus =
            notification.transaction_status == AppConstants.MidtransStatus.Settlement ||
            notification.transaction_status == AppConstants.MidtransStatus.Capture;

        if (order.Status != AppConstants.OrderStatus.Pending)
        {
            if (isPositiveNotificationStatus &&
                OrderStateTransition.CanEscalateLatePayment(order.Status, AppConstants.OrderStatus.NeedsAction))
            {
                var verifiedLateStatus = await TryVerifyAuthoritativeSettlementAsync(order, notification);
                if (verifiedLateStatus != null)
                {
                    await _fulfillment.HandleVerifiedLatePaymentAsync(order, notification, verifiedLateStatus);
                }
                return true;
            }

            _logger.LogWarning(
                "[WEBHOOK] Duplicate/late webhook ignored for Order {OrderNo}. Current status: {Status}",
                order.OrderNo, order.Status);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(order.MidtransTransactionId) &&
            !string.IsNullOrWhiteSpace(notification.transaction_id) &&
            !string.Equals(order.MidtransTransactionId, notification.transaction_id, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "[WEBHOOK] TransactionId notifikasi ({NotifTxId}) tidak cocok dengan transaksi order ({OrderTxId}) untuk Order {OrderNo}. Diabaikan.",
                notification.transaction_id, order.MidtransTransactionId, order.OrderNo);
            return true;
        }

        if (notification.transaction_status == AppConstants.MidtransStatus.Expire ||
            notification.transaction_status == AppConstants.MidtransStatus.Cancel ||
            notification.transaction_status == AppConstants.MidtransStatus.Deny)
        {
            var targetTerminalStatus = notification.transaction_status == AppConstants.MidtransStatus.Expire
                ? AppConstants.OrderStatus.Expired
                : AppConstants.OrderStatus.Failed;

            var terminalPaymentType = string.IsNullOrEmpty(order.PaymentType) && !string.IsNullOrEmpty(notification.payment_type)
                ? notification.payment_type
                : order.PaymentType;
            var terminalTxId = string.IsNullOrEmpty(order.MidtransTransactionId) && !string.IsNullOrEmpty(notification.transaction_id)
                ? notification.transaction_id
                : order.MidtransTransactionId;

            await _fulfillment.TryMarkPendingOrderTerminalAsync(
                order,
                targetTerminalStatus,
                terminalPaymentType,
                terminalTxId);

            return true;
        }

        if (!isPositiveNotificationStatus)
        {
            return true;
        }

        var providerStatus = await TryVerifyAuthoritativeSettlementAsync(order, notification);
        if (providerStatus == null)
        {
            return true;
        }

        var resolvedPaymentType = !string.IsNullOrEmpty(order.PaymentType)
            ? order.PaymentType
            : (!string.IsNullOrEmpty(notification.payment_type)
                ? notification.payment_type
                : providerStatus.PaymentType);

        var resolvedTransactionId = !string.IsNullOrEmpty(order.MidtransTransactionId)
            ? order.MidtransTransactionId
            : (!string.IsNullOrEmpty(notification.transaction_id)
                ? notification.transaction_id
                : providerStatus.TransactionId);

        int rowsAffected;
        using (var dbTransaction = await _db.Database.BeginTransactionAsync())
        {
            var processedAt = OrderFulfillmentState.CreateStandardTimestamp(DateTime.UtcNow);
            rowsAffected = await _db.TopupOrders
                .Where(o => o.Id == order.Id && o.Status == AppConstants.OrderStatus.Pending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(o => o.Status, AppConstants.OrderStatus.Paid)
                    .SetProperty(o => o.ProcessedAt, processedAt)
                    .SetProperty(o => o.PaymentType, resolvedPaymentType)
                    .SetProperty(o => o.MidtransTransactionId, resolvedTransactionId));

            await dbTransaction.CommitAsync();
        }

        await _db.Entry(order).ReloadAsync();

        if (rowsAffected == 1)
        {
            await _fulfillment.ExecutePostPaymentTopupAsync(order);
            return true;
        }

        if (OrderStateTransition.CanEscalateLatePayment(order.Status, AppConstants.OrderStatus.NeedsAction))
        {
            await _fulfillment.HandleVerifiedLatePaymentAsync(order, notification, providerStatus);
            return true;
        }

        _logger.LogWarning(
            "[WEBHOOK] Konflik konkurensi pada Pesanan {OrderNo}. Proses lain sudah menanganinya (Status: {Status})",
            order.OrderNo, order.Status);
        return true;
    }

    internal static bool IsPositivePaymentAndFraudAcceptable(string? transactionStatus, string? fraudStatus)
    {
        if (string.Equals(transactionStatus, AppConstants.MidtransStatus.Capture, StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(fraudStatus, AppConstants.MidtransFraud.Accept, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(transactionStatus, AppConstants.MidtransStatus.Settlement, StringComparison.OrdinalIgnoreCase))
        {
            return fraudStatus is null ||
                   string.Equals(fraudStatus, AppConstants.MidtransFraud.Accept, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private async Task<MidtransTransactionStatus?> TryVerifyAuthoritativeSettlementAsync(
        TopupOrder order,
        MidtransNotificationRequest notification)
    {
        if (!IsPositivePaymentAndFraudAcceptable(notification.transaction_status, notification.fraud_status))
        {
            return null;
        }

        if (!Pricing.IsExactRupiahMatch(notification.gross_amount, order.TotalPaid))
        {
            _logger.LogWarning(
                "[WEBHOOK] Nominal tidak cocok untuk Order {OrderNo}. Diharapkan {Expected}, diterima {Received}. Status tidak diubah.",
                order.OrderNo, order.TotalPaid, notification.gross_amount);
            return null;
        }

        if (!string.IsNullOrWhiteSpace(order.MidtransTransactionId) &&
            !string.IsNullOrWhiteSpace(notification.transaction_id) &&
            !string.Equals(order.MidtransTransactionId, notification.transaction_id, StringComparison.Ordinal))
        {
            _logger.LogCritical(
                "[WEBHOOK] TransactionId notifikasi ({NotifTxId}) tidak cocok dengan transaksi order ({OrderTxId}) untuk Order {OrderNo}. Status tidak diubah.",
                notification.transaction_id, order.MidtransTransactionId, order.OrderNo);
            return null;
        }

        MidtransTransactionStatus? providerStatus;
        try
        {
            providerStatus = await _midtransProvider.GetStatusAsync(order.OrderNo);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[WEBHOOK] Gagal memverifikasi status otoritatif Midtrans untuk Order {OrderNo}. Transisi ditolak (fail-closed).",
                order.OrderNo);
            return null;
        }

        if (providerStatus == null)
        {
            _logger.LogCritical(
                "[WEBHOOK] Status otoritatif Midtrans tidak ditemukan (404/null) untuk Order {OrderNo}. Transisi ditolak (fail-closed).",
                order.OrderNo);
            return null;
        }

        if (!string.Equals(providerStatus.OrderId, order.OrderNo, StringComparison.Ordinal))
        {
            _logger.LogCritical(
                "[WEBHOOK] OrderId pada Midtrans Status API ({ApiOrderId}) tidak cocok dengan Order {OrderNo}. Status tidak diubah.",
                providerStatus.OrderId, order.OrderNo);
            return null;
        }

        if (!IsPositivePaymentAndFraudAcceptable(providerStatus.TransactionStatus, providerStatus.FraudStatus) ||
            !Pricing.IsExactRupiahMatch(providerStatus.GrossAmount, order.TotalPaid))
        {
            _logger.LogCritical(
                "[WEBHOOK] Konfirmasi status otoritatif Midtrans TIDAK VALID untuk Order {OrderNo}. " +
                "Notifikasi: {NotifStatus}/{NotifFraud}/{NotifAmount} — Status API: {ApiStatus}/{ApiFraud}/{ApiAmount}. Status tidak diubah.",
                order.OrderNo,
                notification.transaction_status, notification.fraud_status, notification.gross_amount,
                providerStatus.TransactionStatus, providerStatus.FraudStatus, providerStatus.GrossAmount);
            return null;
        }

        if (!string.IsNullOrWhiteSpace(order.MidtransTransactionId))
        {
            if (string.IsNullOrWhiteSpace(providerStatus.TransactionId) ||
                !string.Equals(order.MidtransTransactionId, providerStatus.TransactionId, StringComparison.Ordinal))
            {
                _logger.LogCritical(
                    "[WEBHOOK] TransactionId pada Midtrans Status API ({ApiTxId}) tidak cocok dengan Order {OrderNo} ({OrderTxId}). Status tidak diubah.",
                    providerStatus.TransactionId, order.OrderNo, order.MidtransTransactionId);
                return null;
            }
        }

        if (!string.IsNullOrWhiteSpace(notification.transaction_id) &&
            !string.IsNullOrWhiteSpace(providerStatus.TransactionId) &&
            !string.Equals(notification.transaction_id, providerStatus.TransactionId, StringComparison.Ordinal))
        {
            _logger.LogCritical(
                "[WEBHOOK] TransactionId notifikasi ({NotifTxId}) berbeda dengan Midtrans Status API ({ApiTxId}) untuk Order {OrderNo}. Status tidak diubah.",
                notification.transaction_id, providerStatus.TransactionId, order.OrderNo);
            return null;
        }

        return providerStatus;
    }
}
