using be_digitalin.Constants;
using be_digitalin.Domain.Commerce;
using be_digitalin.Domain.Entities;
using be_digitalin.Domain.Payments;
using be_digitalin.Domain.Products;
using Xunit;

namespace be_digitalin.Tests;

/// <summary>
/// Unit test murni untuk aturan bisnis yang diekstrak ke layer Domain:
/// - PaymentMethodRules (validasi kanal, VA bank, dan cstore)
/// - InputSchemaRules (normalisasi field input & validasi placeholder TargetTemplate)
/// - ProductCategoryPolicy (ekstensibilitas kategori produk digital di luar game)
/// - VoucherRules.ValidateEligibility & CalculateDiscount
/// </summary>
public class ExtractedDomainRulesTests
{
    // ── PaymentMethodRules ──

    [Theory]
    [InlineData("QRIS", null, null, "qris", null, null)]
    [InlineData("gopay", null, null, "gopay", null, null)]
    [InlineData("shopeepay", null, null, "shopeepay", null, null)]
    [InlineData("bank_transfer", "BCA", null, "bank_transfer", "bca", null)]
    [InlineData("cstore", null, "Indomaret", "cstore", null, "indomaret")]
    public void PaymentMethodRules_ValidSelections_AreNormalized(
        string method, string? bank, string? store,
        string expectedMethod, string? expectedBank, string? expectedStore)
    {
        var result = PaymentMethodRules.ValidateSelection(method, bank, store);

        Assert.Equal(expectedMethod, result.PaymentMethod);
        Assert.Equal(expectedBank, result.PaymentBank);
        Assert.Equal(expectedStore, result.PaymentStore);
    }

    [Theory]
    [InlineData(null, null, null, "Metode pembayaran wajib dipilih")]
    [InlineData("credit_card", null, null, "Metode pembayaran tidak didukung")]
    [InlineData("bank_transfer", null, null, "wajib memilih bank VA")]
    [InlineData("bank_transfer", "danamon", null, "Bank VA tidak didukung")]
    [InlineData("qris", "bca", null, "paymentBank hanya berlaku")]
    [InlineData("cstore", null, null, "wajib memilih toko")]
    [InlineData("cstore", null, "circlek", "Toko tidak didukung")]
    [InlineData("gopay", null, "indomaret", "paymentStore hanya berlaku")]
    public void PaymentMethodRules_InvalidSelections_ThrowExpectedMessage(
        string? method, string? bank, string? store, string expectedMessagePart)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PaymentMethodRules.ValidateSelection(method, bank, store));

        Assert.Contains(expectedMessagePart, ex.Message);
    }

    // ── InputSchemaRules ──

    [Fact]
    public void InputSchemaRules_NormalizesKeysAndOrdersFields()
    {
        var raw = new List<GameInputField>
        {
            new() { Key = "ServerId", Label = " Server ", Type = "NUMBER", Order = 2, Required = true },
            new() { Key = "UserId", Label = " User ID ", Type = "Text", Order = 1, Required = true }
        };

        var normalized = InputSchemaRules.NormalizeAndValidateInputFields(raw);

        Assert.Equal(2, normalized.Count);
        Assert.Equal("userId", normalized[0].Key);
        Assert.Equal(1, normalized[0].Order);
        Assert.Equal("serverId", normalized[1].Key);
        Assert.Equal(2, normalized[1].Order);
    }

    [Fact]
    public void InputSchemaRules_ValidateTargetTemplate_RejectsMissingOrUnknownPlaceholders()
    {
        var fields = new List<GameInputField>
        {
            new() { Key = "userId", Label = "User ID", Type = "number", Order = 1, Required = true },
            new() { Key = "zoneId", Label = "Zone ID", Type = "number", Order = 2, Required = true }
        };

        // Missing zoneId
        var missingEx = Assert.Throws<InvalidOperationException>(() =>
            InputSchemaRules.ValidateTargetTemplate("{userId}", fields));
        Assert.Contains("zoneId", missingEx.Message);

        // Unknown serverId
        var unknownEx = Assert.Throws<InvalidOperationException>(() =>
            InputSchemaRules.ValidateTargetTemplate("{userId}{zoneId}{serverId}", fields));
        Assert.Contains("serverId", unknownEx.Message);

        // Valid template
        var valid = InputSchemaRules.ValidateTargetTemplate(" {userId}{zoneId} ", fields);
        Assert.Equal("{userId}{zoneId}", valid);
    }

    // ── ProductCategoryPolicy (Extensibility beyond Games) ──

    [Theory]
    [InlineData("Games", "mobile", "userId")]
    [InlineData("Pulsa", "pulsa-data", "phoneNumber")]
    [InlineData("Data", "pulsa-data", "phoneNumber")]
    [InlineData("Voucher", "voucher", "userId")]
    [InlineData("E-Money", "e-money", "phoneNumber")]
    public void ProductCategoryPolicy_ResolvesCategoryAndDefaultInputSchema(
        string supplierCategory, string expectedStorefrontSlug, string expectedFirstFieldKey)
    {
        Assert.True(ProductCategoryPolicy.IsSupportedSupplierCategory(supplierCategory));
        Assert.Equal(
            expectedStorefrontSlug,
            ProductCategoryPolicy.ResolveDefaultStorefrontCategory(supplierCategory));

        var (fields, template) = ProductCategoryPolicy.CreateDefaultInputSchema(supplierCategory);
        Assert.NotEmpty(fields);
        Assert.Equal(expectedFirstFieldKey, fields[0].Key);
        Assert.Contains($"{{{expectedFirstFieldKey}}}", template);
    }

    // ── VoucherRules.ValidateEligibility ──

    [Fact]
    public void VoucherRules_ValidateEligibility_EnforcesAllDomainInvariants()
    {
        var now = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
        var activeVoucher = new Voucher
        {
            Code = "HEMAT10",
            DiscountType = "Percentage",
            DiscountValue = 10,
            MinimumTransaction = 50000,
            MaximumDiscount = 10000,
            UsageLimit = 5,
            UsedCount = 2,
            IsActive = true,
            ExpiredAt = now.AddDays(1)
        };

        Assert.Equal("Kode voucher tidak ditemukan.",
            VoucherRules.ValidateEligibility(null, 50000, now).Error);

        var ok = VoucherRules.ValidateEligibility(activeVoucher, 50000, now);
        Assert.Null(ok.Error);
        Assert.Same(activeVoucher, ok.Voucher);

        var belowMin = VoucherRules.ValidateEligibility(activeVoucher, 20000, now);
        Assert.Contains("minimal transaksi", belowMin.Error);

        activeVoucher.UsedCount = 5;
        Assert.Equal("Kuota voucher ini sudah habis.",
            VoucherRules.ValidateEligibility(activeVoucher, 50000, now).Error);

        activeVoucher.UsedCount = 0;
        Assert.Equal("Voucher ini sudah kedaluwarsa.",
            VoucherRules.ValidateEligibility(activeVoucher, 50000, now.AddDays(2)).Error);

        activeVoucher.IsActive = false;
        Assert.Equal("Voucher ini sudah tidak aktif.",
            VoucherRules.ValidateEligibility(activeVoucher, 50000, now).Error);
    }
}
