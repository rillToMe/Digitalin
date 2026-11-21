using be_digitalin.Application.Ports;
using be_digitalin.Application.Serialization;
using be_digitalin.Config;
using be_digitalin.Constants;
using be_digitalin.Domain;
using be_digitalin.Domain.Entities;
using be_digitalin.Domain.Orders;
using be_digitalin.Domain.Payments;
using be_digitalin.DTOs;
using be_digitalin.Services.Commerce;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services.Orders;

/// <summary>
/// Use case pembuatan pesanan baru (checkout), validasi kanal pembayaran,
/// reservasi kuota voucher atomik, dan inisiasi charge Midtrans Core API.
/// </summary>
public sealed class OrderPlacementService
{
    private readonly IApplicationDbContext _db;
    private readonly IMidtransProvider _midtransProvider;
    private readonly IVoucherService _voucherService;
    private readonly ILogger _logger;
    private readonly OrderPublicHashSettings _publicHashSettings;

    public OrderPlacementService(
        IApplicationDbContext db,
        IMidtransProvider midtransProvider,
        IVoucherService voucherService,
        ILogger logger,
        OrderPublicHashSettings publicHashSettings)
    {
        _db = db;
        _midtransProvider = midtransProvider;
        _voucherService = voucherService;
        _logger = logger;
        _publicHashSettings = publicHashSettings;
    }

    public async Task<OrderResponse> PlaceOrderAsync(
        CreateOrderRequest request,
        int? customerId = null)
    {
        var (paymentMethod, paymentBank, paymentStore) = PaymentMethodRules.ValidateSelection(
            request.PaymentMethod,
            request.PaymentBank,
            request.PaymentStore);

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

        var fullPlayerId = OrderService.BuildTargetValue(
            metadata,
            request.TargetValues);

        _logger.LogInformation(
            "[ORDER] customer_no dirender untuk brand {Brand}: customer_no={CustomerNo}, template={Template}, values={@TargetValues}",
            product.Brand,
            fullPlayerId,
            metadata.TargetTemplate,
            request.TargetValues);

        decimal originalPrice = Pricing.RoundRupiah(product.Price);
        decimal discountAmount = 0m;
        Voucher? voucher = null;

        if (!string.IsNullOrWhiteSpace(request.VoucherCode))
        {
            var (foundVoucher, error) = await _voucherService.ValidateVoucherAsync(
                request.VoucherCode, originalPrice);

            if (error != null)
                throw new InvalidOperationException(error);

            voucher = foundVoucher!;
            discountAmount = Pricing.RoundRupiah(_voucherService.CalculateDiscount(voucher, originalPrice));
        }

        decimal finalPrice = Pricing.RoundRupiah(originalPrice - discountAmount);

        if (finalPrice <= 0)
            throw new InvalidOperationException("Harga akhir setelah diskon tidak boleh nol atau negatif.");

        decimal costPrice = originalPrice - Pricing.MarginOf(product.CustomMargin);
        decimal profitAmount = finalPrice - costPrice;

        string generatedOrderNo = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..10].ToUpper()}";
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
            VoucherCode = voucher?.Code,
            DiscountAmount = discountAmount,

            ProductName = product.Name,
            BuyerSkuCode = product.BuyerSkuCode,
            BrandName = product.Brand,
            BrandSlug = metadata.BrandSlug,
            CategoryName = product.Category,
            ImageUrl = product.ImageUrl,
            OriginalPrice = originalPrice,
            TotalPaid = finalPrice,
            CostPrice = costPrice,
            ProfitAmount = profitAmount
        };

        using (var voucherTx = await _db.Database.BeginTransactionAsync())
        {
            _db.TopupOrders.Add(order);
            await _db.SaveChangesAsync();

            if (voucher != null)
            {
                await _voucherService.ReserveVoucherAsync(voucher);
            }

            await voucherTx.CommitAsync();
        }

        OrderPaymentInstructions? paymentInstructions;
        try
        {
            var charge = await _midtransProvider.CreateChargeAsync(new MidtransChargeRequest(
                order.OrderNo, finalPrice, product.Id.ToString(), product.Name, fullPlayerId,
                paymentMethod,
                paymentMethod == AppConstants.MidtransPaymentMethods.BankTransfer ? paymentBank : null,
                paymentMethod == AppConstants.MidtransPaymentMethods.Cstore ? paymentStore : null));

            order.PaymentType = charge.PaymentType;
            order.MidtransTransactionId = charge.TransactionId;
            paymentInstructions = OrderService.MapPaymentInstructions(charge);
            order.PaymentInstructionsJson = OrderPaymentInstructionsCodec.Serialize(paymentInstructions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ORDER] Midtrans gagal membuat transaksi untuk {OrderNo}. Order dikompensasi ke EXPIRED.",
                order.OrderNo);

            if (OrderStateTransition.CanTransition(order.Status, AppConstants.OrderStatus.Expired))
            {
                try
                {
                    await using var compensationTx = await _db.Database.BeginTransactionAsync();
                    var expiredRows = await _db.TopupOrders
                        .Where(o => o.Id == order.Id && o.Status == AppConstants.OrderStatus.Pending)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(o => o.Status, AppConstants.OrderStatus.Expired)
                            .SetProperty(o => o.ProcessedAt, (DateTime?)null));

                    if (expiredRows == 1 && !string.IsNullOrWhiteSpace(order.VoucherCode))
                    {
                        await _voucherService.ReleaseVoucherAsync(order.VoucherCode);
                    }

                    await compensationTx.CommitAsync();
                    await _db.Entry(order).ReloadAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    _logger.LogWarning(
                        "[SaveChanges] Konflik konkurensi pada Pesanan {OrderNo}. Perubahan dibatalkan.",
                        order.OrderNo);
                }
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
}
