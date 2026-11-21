using be_digitalin.Application.Ports;
using be_digitalin.Domain;
using be_digitalin.DTOs;
using Microsoft.EntityFrameworkCore;
using be_digitalin.Services.Commerce;

namespace be_digitalin.Services.Orders;

public class CheckoutService : ICheckoutService
{
    private readonly IApplicationDbContext _db;
    private readonly IVoucherService _voucherService;

    public CheckoutService(IApplicationDbContext db, IVoucherService voucherService)
    {
        _db = db;
        _voucherService = voucherService;
    }

    public async Task<CheckoutPreviewResponse> PreviewAsync(CheckoutPreviewRequest request)
    {
        // 1. Validate product exists
        var product = await _db.GameProducts.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId);

        if (product == null)
            throw new InvalidOperationException("Produk tidak ditemukan!");

        decimal originalPrice = Pricing.RoundRupiah(product.Price);
        decimal discountAmount = 0m;
        bool isVoucherApplied = false;
        string? voucherCode = null;
        string? discountType = null;
        decimal? discountValue = null;
        decimal? maximumDiscount = null;

        // 2. Validate & calculate voucher if provided
        if (!string.IsNullOrWhiteSpace(request.VoucherCode))
        {
            var (voucher, error) = await _voucherService.ValidateVoucherAsync(
                request.VoucherCode, originalPrice);

            if (error != null)
                throw new InvalidOperationException(error);

            discountAmount = Pricing.RoundRupiah(_voucherService.CalculateDiscount(voucher!, originalPrice));
            isVoucherApplied = true;
            voucherCode = voucher!.Code;
            discountType = voucher.DiscountType;
            discountValue = voucher.DiscountValue;
            maximumDiscount = voucher.MaximumDiscount;
        }

        decimal finalPrice = Pricing.RoundRupiah(originalPrice - discountAmount);

        if (finalPrice <= 0)
            throw new InvalidOperationException("Harga akhir setelah diskon tidak boleh nol atau negatif.");

        return new CheckoutPreviewResponse(
            ProductId: product.Id,
            ProductName: product.Name,
            ImageUrl: product.ImageUrl,
            GameCode: product.GameCode,
            OriginalPrice: originalPrice,
            DiscountAmount: discountAmount,
            FinalPrice: finalPrice,
            Currency: "IDR",
            IsVoucherApplied: isVoucherApplied,
            VoucherCode: voucherCode,
            DiscountType: discountType,
            DiscountValue: discountValue,
            MaximumDiscount: maximumDiscount
        );
    }
}
