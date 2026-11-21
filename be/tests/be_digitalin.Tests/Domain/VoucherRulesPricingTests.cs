using Xunit;
using be_digitalin.Domain;
using be_digitalin.Domain.Entities;

namespace be_digitalin.Tests;

public class VoucherRulesTests
{
    private static Voucher Voucher(
        bool isActive = true, DateTime? expiredAt = null,
        int usedCount = 0, int usageLimit = 10) => new()
    {
        Code = "TEST",
        IsActive = isActive,
        ExpiredAt = expiredAt ?? DateTime.UtcNow.AddDays(1),
        UsedCount = usedCount,
        UsageLimit = usageLimit
    };

    [Fact]
    public void Status_priority_disabled_first() =>
        Assert.Equal("Disabled", Domain.Commerce.VoucherRules.ComputeStatus(
            Voucher(isActive: false, expiredAt: DateTime.UtcNow.AddDays(-1))));

    [Fact]
    public void Status_priority_expired_second() =>
        Assert.Equal("Expired", Domain.Commerce.VoucherRules.ComputeStatus(
            Voucher(expiredAt: DateTime.UtcNow.AddDays(-1))));

    [Fact]
    public void Status_quota_exceeded_third() =>
        Assert.Equal("QuotaExceeded", Domain.Commerce.VoucherRules.ComputeStatus(
            Voucher(usedCount: 10, usageLimit: 10)));

    [Fact]
    public void Status_active_when_all_ok() =>
        Assert.Equal("Active", Domain.Commerce.VoucherRules.ComputeStatus(Voucher()));

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("123456789012345678901234567890123456789012345678901234567890")]
    public void Validate_rejects_bad_code_length(string code) =>
        Assert.Throws<InvalidOperationException>(() =>
            Domain.Commerce.VoucherRules.ValidatePayload(code, "Percentage", 10, 1,
                DateTime.UtcNow.AddDays(1)));

    [Fact]
    public void Validate_rejects_unknown_discount_type() =>
        Assert.Throws<InvalidOperationException>(() =>
            Domain.Commerce.VoucherRules.ValidatePayload("GOODCODE", "Bogus", 10, 1,
                DateTime.UtcNow.AddDays(1)));

    [Theory]
    [InlineData("Percentage", 0)]
    [InlineData("Percentage", 101)]
    [InlineData("FixedAmount", -1)]
    public void Validate_rejects_bad_discount_value(string type, decimal value) =>
        Assert.Throws<InvalidOperationException>(() =>
            Domain.Commerce.VoucherRules.ValidatePayload("GOODCODE", type, value, 1,
                DateTime.UtcNow.AddDays(1)));

    [Fact]
    public void Validate_rejects_past_expiry() =>
        Assert.Throws<InvalidOperationException>(() =>
            Domain.Commerce.VoucherRules.ValidatePayload("GOODCODE", "Percentage", 10, 1,
                DateTime.UtcNow.AddDays(-1)));

    [Fact]
    public void Validate_rejects_zero_usage_limit() =>
        Assert.Throws<InvalidOperationException>(() =>
            Domain.Commerce.VoucherRules.ValidatePayload("GOODCODE", "Percentage", 10, 0,
                DateTime.UtcNow.AddDays(1)));

    [Fact]
    public void Validate_rejects_usage_limit_below_used_count() =>
        Assert.Throws<InvalidOperationException>(() =>
            Domain.Commerce.VoucherRules.ValidatePayload("GOODCODE", "Percentage", 10, 3,
                DateTime.UtcNow.AddDays(1), existingUsedCount: 5));

    [Fact]
    public void Validate_accepts_valid_payload() =>
        Domain.Commerce.VoucherRules.ValidatePayload("GOODCODE", "Percentage", 10, 5,
            DateTime.UtcNow.AddDays(1), existingUsedCount: 5);
}

public class PricingTests
{
    [Fact]
    public void MarginOf_null_is_zero() =>
        Assert.Equal(0m, Pricing.MarginOf(null));

    [Fact]
    public void MarginOf_returns_custom_margin() =>
        Assert.Equal(2000m, Pricing.MarginOf(2000m));

    [Fact]
    public void CalculateSellingPrice_zero_margin_when_null() =>
        Assert.Equal(10000m, Pricing.CalculateSellingPrice(10000m, null));

    [Fact]
    public void CalculateSellingPrice_adds_margin() =>
        Assert.Equal(12000m, Pricing.CalculateSellingPrice(10000m, 2000m));

    [Theory]
    [InlineData(1519.9, 1520)]
    [InlineData(1519.5, 1520)]
    [InlineData(1519.4, 1519)]
    public void RoundRupiah_UsesMidpointAwayFromZero(decimal input, decimal expected) =>
        Assert.Equal(expected, Pricing.RoundRupiah(input));

    [Theory]
    [InlineData("15199.00", 15199, true)]
    [InlineData("15199", 15199, true)]
    [InlineData("15199.50", 15199, false)]
    [InlineData("15198.00", 15199, false)]
    [InlineData("0.00", 0, false)]
    [InlineData("-100.00", -100, false)]
    public void IsExactRupiahMatch_EnforcesWholePositiveRupiahEquality(
        string rawAmount, decimal expectedAmount, bool expectedMatch) =>
        Assert.Equal(expectedMatch, Pricing.IsExactRupiahMatch(rawAmount, expectedAmount));

    [Fact]
    public void VoucherRules_CalculateDiscount_RoundsFractionalPercentageConsistentlyWithPricing()
    {
        var voucher = new Voucher
        {
            Code = "DISC10",
            DiscountType = "Percentage",
            DiscountValue = 10m
        };

        // 10% dari 15199 = 1519.9 -> dibulatkan ke 1520, sehingga finalPrice = 13679 (bulat)
        var discount = Domain.Commerce.VoucherRules.CalculateDiscount(voucher, 15199m);
        var finalPrice = Pricing.RoundRupiah(15199m - discount);

        Assert.Equal(1520m, discount);
        Assert.Equal(13679m, finalPrice);
        Assert.Equal(13679, Pricing.ToRupiahInt(finalPrice));
        Assert.True(Pricing.IsExactRupiahMatch("13679.00", finalPrice));
    }
}
