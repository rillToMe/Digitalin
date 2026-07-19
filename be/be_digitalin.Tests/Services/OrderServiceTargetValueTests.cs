using be_digitalin.Constants;
using be_digitalin.Infrastructure.Serialization;
using be_digitalin.Models;
using be_digitalin.Services;
using FluentAssertions;
using Xunit;

namespace be_digitalin.Tests.Services;

/// <summary>
/// Menguji helper murni (pure) di OrderService yang membentuk customer_no Digiflazz:
/// BuildTargetValue → ValidateTargetValues → RenderTargetTemplate.
/// Helper ini <c>internal static</c> dan tanpa akses DB, jadi bisa diuji langsung.
/// </summary>
public class OrderServiceTargetValueTests
{
    private static GameMetadata MetadataWith(
        IReadOnlyList<GameInputField> fields,
        string targetTemplate)
    {
        return new GameMetadata
        {
            Brand = "Test Brand",
            InputSchemaJson = GameInputSchemaSerializer.Serialize(fields),
            TargetTemplate = targetTemplate
        };
    }

    private static GameInputField Field(
        string key, string label, string type, bool required = true,
        List<string>? options = null, int order = 1) =>
        new()
        {
            Key = key,
            Label = label,
            Placeholder = $"Masukkan {label}",
            Type = type,
            Required = required,
            Order = order,
            Options = options ?? []
        };

    // ── BuildTargetValue: happy path ──────────────────────────────

    [Fact]
    public void BuildTargetValue_SingleField_RendersValue()
    {
        var metadata = MetadataWith(
            [Field("userId", "User ID", AppConstants.GameInputFieldTypes.Number)],
            "{userId}");

        var result = OrderService.BuildTargetValue(
            metadata,
            new Dictionary<string, string?> { ["userId"] = "12345" });

        result.Should().Be("12345");
    }

    [Fact]
    public void BuildTargetValue_TwoFields_ConcatenatesInTemplateOrder()
    {
        var metadata = MetadataWith(
            [
                Field("userId", "User ID", AppConstants.GameInputFieldTypes.Number, order: 1),
                Field("server", "Server", AppConstants.GameInputFieldTypes.Number, order: 2)
            ],
            "{userId}{server}");

        var result = OrderService.BuildTargetValue(
            metadata,
            new Dictionary<string, string?> { ["userId"] = "12345", ["server"] = "678" });

        result.Should().Be("12345678");
    }

    [Fact]
    public void BuildTargetValue_TrimsWhitespaceFromValues()
    {
        var metadata = MetadataWith(
            [Field("userId", "User ID", AppConstants.GameInputFieldTypes.Number)],
            "{userId}");

        var result = OrderService.BuildTargetValue(
            metadata,
            new Dictionary<string, string?> { ["userId"] = "  12345  " });

        result.Should().Be("12345");
    }

    // ── BuildTargetValue: validasi input ──────────────────────────

    [Fact]
    public void BuildTargetValue_EmptyTargetValues_Throws()
    {
        var metadata = MetadataWith(
            [Field("userId", "User ID", AppConstants.GameInputFieldTypes.Number)],
            "{userId}");

        var act = () => OrderService.BuildTargetValue(
            metadata, new Dictionary<string, string?>());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*wajib diisi*");
    }

    [Fact]
    public void BuildTargetValue_UnknownKey_Throws()
    {
        var metadata = MetadataWith(
            [Field("userId", "User ID", AppConstants.GameInputFieldTypes.Number)],
            "{userId}");

        var act = () => OrderService.BuildTargetValue(
            metadata,
            new Dictionary<string, string?> { ["zoneId"] = "1" });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*tidak dikenali*");
    }

    [Fact]
    public void BuildTargetValue_MissingRequiredValue_Throws()
    {
        var metadata = MetadataWith(
            [
                Field("userId", "User ID", AppConstants.GameInputFieldTypes.Number, order: 1),
                Field("server", "Server", AppConstants.GameInputFieldTypes.Number, order: 2)
            ],
            "{userId}{server}");

        var act = () => OrderService.BuildTargetValue(
            metadata,
            new Dictionary<string, string?> { ["userId"] = "12345", ["server"] = "  " });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Server*wajib diisi*");
    }

    [Fact]
    public void BuildTargetValue_NumberFieldWithNonDigit_Throws()
    {
        var metadata = MetadataWith(
            [Field("userId", "User ID", AppConstants.GameInputFieldTypes.Number)],
            "{userId}");

        var act = () => OrderService.BuildTargetValue(
            metadata,
            new Dictionary<string, string?> { ["userId"] = "12a45" });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*hanya boleh berisi angka*");
    }

    // ── Field bertipe select ──────────────────────────────────────

    [Fact]
    public void BuildTargetValue_SelectFieldValidOption_Passes()
    {
        var metadata = MetadataWith(
            [Field("server", "Server", AppConstants.GameInputFieldTypes.Select,
                options: ["Asia", "Europe"])],
            "{server}");

        var result = OrderService.BuildTargetValue(
            metadata,
            new Dictionary<string, string?> { ["server"] = "asia" }); // case-insensitive

        result.Should().Be("asia");
    }

    [Fact]
    public void BuildTargetValue_SelectFieldInvalidOption_Throws()
    {
        var metadata = MetadataWith(
            [Field("server", "Server", AppConstants.GameInputFieldTypes.Select,
                options: ["Asia", "Europe"])],
            "{server}");

        var act = () => OrderService.BuildTargetValue(
            metadata,
            new Dictionary<string, string?> { ["server"] = "Mars" });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Pilihan Server tidak valid*");
    }

    // ── Optional field ────────────────────────────────────────────

    [Fact]
    public void BuildTargetValue_OptionalFieldOmitted_RendersEmpty()
    {
        var metadata = MetadataWith(
            [
                Field("userId", "User ID", AppConstants.GameInputFieldTypes.Number, order: 1),
                Field("note", "Catatan", AppConstants.GameInputFieldTypes.Text, required: false, order: 2)
            ],
            "{userId}{note}");

        var result = OrderService.BuildTargetValue(
            metadata,
            new Dictionary<string, string?> { ["userId"] = "12345", ["note"] = null });

        result.Should().Be("12345");
    }
}
