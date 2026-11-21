using be_digitalin.Application.Ports;
using be_digitalin.Application.Serialization;
using be_digitalin.Config;
using be_digitalin.Domain.Entities;
using be_digitalin.Domain.Orders;
using be_digitalin.DTOs;
using be_digitalin.Services.Commerce;
using be_digitalin.Services.Settings;
using Microsoft.Extensions.Options;

namespace be_digitalin.Services.Orders;

/// <summary>
/// Compatibility facade untuk <see cref="IOrderService"/> yang mendelegasikan
/// tanggung jawab ke layanan vertikal terfokus:
/// - <see cref="OrderPlacementService"/> (checkout & pembuatan order)
/// - <see cref="OrderQueryService"/> (riwayat transaksi customer & admin)
/// - <see cref="PaymentWebhookService"/> (webhook pembayaran Midtrans)
/// - <see cref="FulfillmentService"/> (fulfillment Digiflazz, callback, retry, refund & sweep)
/// </summary>
public class OrderService : IOrderService
{
    private readonly OrderPlacementService _placement;
    private readonly OrderQueryService _query;
    private readonly PaymentWebhookService _paymentWebhook;
    private readonly FulfillmentService _fulfillment;

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
        _fulfillment = new FulfillmentService(
            db, midtransProvider, digiflazzProvider, telegramProvider, voucherService, appSettings, logger);
        _placement = new OrderPlacementService(
            db, midtransProvider, voucherService, logger, publicHashSettings.Value);
        _query = new OrderQueryService(db);
        _paymentWebhook = new PaymentWebhookService(
            db, midtransProvider, _fulfillment, logger);
    }

    public Task<OrderResponse> PlaceOrderAsync(CreateOrderRequest request, int? customerId = null) =>
        _placement.PlaceOrderAsync(request, customerId);

    public Task<PagedResult<OrderResponse>> GetOrdersAsync(
        int? customerId = null,
        int page = 1,
        int limit = 20,
        string? status = null) =>
        _query.GetOrdersAsync(customerId, page, limit, status);

    public Task<PagedResult<OrderResponse>> GetAllOrdersAsync(
        int page = 1,
        int limit = 20,
        string? status = null) =>
        _query.GetOrdersAsync(null, page, limit, status);

    public Task<bool> HandleNotificationAsync(MidtransNotificationRequest notification) =>
        _paymentWebhook.HandleNotificationAsync(notification);

    public Task<OrderActionResult> RetryOrderAsync(string orderNo) =>
        _fulfillment.RetryOrderAsync(orderNo);

    public Task<OrderActionResult> RefundOrderAsync(string orderNo) =>
        _fulfillment.RefundOrderAsync(orderNo);

    public Task<string> HandleDigiflazzWebhookAsync(DigiflazzWebhookData data) =>
        _fulfillment.HandleDigiflazzWebhookAsync(data);

    public Task SweepStuckOrdersAsync(CancellationToken cancellationToken = default) =>
        _fulfillment.SweepStuckOrdersAsync(cancellationToken);

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

        OrderTargetRenderer.ValidateTargetValues(
            inputFields,
            targetValues);

        return OrderTargetRenderer.RenderTargetTemplate(
            metadata.TargetTemplate,
            inputFields,
            targetValues);
    }
}
