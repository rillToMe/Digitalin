using Xunit;
using be_digitalin.Application.Serialization;
using be_digitalin.Domain.Entities;
using be_digitalin.Domain.Games.GameTemplates;
using be_digitalin.Services.Commerce;
using be_digitalin.Services.Orders;

namespace be_digitalin.Tests;

/// <summary>
/// Target jalur uang: customer_no yang dirender OrderService.BuildTargetValue
/// dikirim ke Digiflazz — salah render = item terkirim ke akun salah.
/// </summary>
public class BuildTargetValueTests
{
    private static GameMetadata Metadata(string schemaJson, string template) => new()
    {
        Brand = "Test",
        InputSchemaJson = schemaJson,
        TargetTemplate = template
    };

    [Fact]
    public void Renders_user_id_only()
    {
        var metadata = Metadata(
            GameInputSchemaSerializer.Serialize(GameTemplateFactory.CreateDefaultInputFields()),
            GameTemplateFactory.DefaultTargetTemplate);

        var result = OrderService.BuildTargetValue(
            metadata,
            new Dictionary<string, string?> { ["userId"] = "12345678" });

        Assert.Equal("12345678", result);
    }

    [Fact]
    public void Renders_template_with_both_fields()
    {
        var fields = new List<GameInputField>
        {
            new() { Key = "userId", Label = "User ID", Type = "number", Required = true, Order = 1 },
            new() { Key = "serverId", Label = "Server", Type = "text", Required = true, Order = 2 },
        };
        var metadata = Metadata(
            GameInputSchemaSerializer.Serialize(fields), "{userId}{serverId}");

        var result = OrderService.BuildTargetValue(
            metadata,
            new Dictionary<string, string?> { ["userId"] = "1234", ["serverId"] = "5678" });

        Assert.Equal("12345678", result);
    }

    [Fact]
    public void Empty_values_throws()
    {
        var metadata = Metadata(
            GameInputSchemaSerializer.Serialize(GameTemplateFactory.CreateDefaultInputFields()),
            GameTemplateFactory.DefaultTargetTemplate);

        Assert.Throws<InvalidOperationException>(() =>
            OrderService.BuildTargetValue(metadata, []));
    }

    [Fact]
    public void Unknown_key_throws()
    {
        var metadata = Metadata(
            GameInputSchemaSerializer.Serialize(GameTemplateFactory.CreateDefaultInputFields()),
            GameTemplateFactory.DefaultTargetTemplate);

        Assert.Throws<InvalidOperationException>(() =>
            OrderService.BuildTargetValue(
                metadata,
                new Dictionary<string, string?> { ["bogus"] = "1" }));
    }

    [Fact]
    public void Missing_required_field_throws()
    {
        var metadata = Metadata(
            GameInputSchemaSerializer.Serialize(GameTemplateFactory.CreateDefaultInputFields()),
            GameTemplateFactory.DefaultTargetTemplate);

        Assert.Throws<InvalidOperationException>(() =>
            OrderService.BuildTargetValue(
                metadata,
                new Dictionary<string, string?> { ["userId"] = " " }));
    }

    [Fact]
    public void Number_field_with_non_digits_throws()
    {
        var metadata = Metadata(
            GameInputSchemaSerializer.Serialize(GameTemplateFactory.CreateDefaultInputFields()),
            GameTemplateFactory.DefaultTargetTemplate);

        Assert.Throws<InvalidOperationException>(() =>
            OrderService.BuildTargetValue(
                metadata,
                new Dictionary<string, string?> { ["userId"] = "12A45" }));
    }
}

public class VoucherDiscountTests
{
    private static readonly VoucherService Svc = new(null!);

    private static Voucher Voucher(string type, decimal value, decimal? max = null) => new()
    {
        Code = "TEST",
        DiscountType = type,
        DiscountValue = value,
        MaximumDiscount = max
    };

    [Fact]
    public void Percentage_without_cap() =>
        Assert.Equal(1500m, Svc.CalculateDiscount(Voucher("Percentage", 10m), 15000m));

    [Fact]
    public void Percentage_with_cap_applies_cap() =>
        Assert.Equal(500m, Svc.CalculateDiscount(Voucher("Percentage", 10m, 500m), 15000m));

    [Fact]
    public void Percentage_cap_above_discount_ignored() =>
        Assert.Equal(1500m, Svc.CalculateDiscount(Voucher("Percentage", 10m, 9999m), 15000m));

    [Fact]
    public void Fixed_amount() =>
        Assert.Equal(2000m, Svc.CalculateDiscount(Voucher("FixedAmount", 2000m), 15000m));

    [Fact]
    public void Discount_capped_at_product_price() =>
        Assert.Equal(15000m, Svc.CalculateDiscount(Voucher("FixedAmount", 99999m), 15000m));
}
