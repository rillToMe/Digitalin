using be_digitalin.Constants;
using be_digitalin.Models;
using be_digitalin.Services;
using FluentAssertions;
using Xunit;

namespace be_digitalin.Tests.Services;

/// <summary>
/// Menguji validasi bidirectional ProductService.ValidateTargetTemplate:
/// setiap placeholder di template harus ada di schema, DAN setiap key schema
/// harus direferensikan template. Mencegah drift yang membuat field (mis. zoneId)
/// diabaikan diam-diam saat render customer_no.
/// </summary>
public class ValidateTargetTemplateTests
{
    private static GameInputField Field(string key) => new()
    {
        Key = key,
        Label = key,
        Type = AppConstants.GameInputFieldTypes.Text,
        Required = true,
        Order = 1
    };

    // ── Kasus valid ───────────────────────────────────────────────

    [Fact]
    public void SingleFieldTemplate_IsValid()
    {
        var fields = new List<GameInputField> { Field("userId") };

        ProductService.ValidateTargetTemplate("{userId}", fields)
            .Should().Be("{userId}");
    }

    [Fact]
    public void MultiFieldTemplate_ReferencingAllKeys_IsValid()
    {
        var fields = new List<GameInputField> { Field("userId"), Field("zone") };

        ProductService.ValidateTargetTemplate("{userId}{zone}", fields)
            .Should().Be("{userId}{zone}");
    }

    [Fact]
    public void Template_IsTrimmed_OnSuccess()
    {
        var fields = new List<GameInputField> { Field("userId") };

        ProductService.ValidateTargetTemplate("  {userId}  ", fields)
            .Should().Be("{userId}");
    }

    [Fact]
    public void MatchingKeys_AreCaseInsensitive()
    {
        var fields = new List<GameInputField> { Field("userId"), Field("zone") };

        // Template pakai kapitalisasi berbeda — tetap dianggap cocok.
        var act = () => ProductService.ValidateTargetTemplate("{UserId}{ZONE}", fields);

        act.Should().NotThrow();
    }

    // ── Arah 2: schema key tidak direferensikan template (drift) ──

    [Fact]
    public void MissingSchemaKeyInTemplate_Throws()
    {
        var fields = new List<GameInputField> { Field("userId"), Field("zoneId") };

        var act = () => ProductService.ValidateTargetTemplate("{userId}", fields);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*zoneId*");
    }

    // ── Arah 1: placeholder tidak ada di schema ───────────────────

    [Fact]
    public void UnknownPlaceholder_Throws()
    {
        var fields = new List<GameInputField> { Field("userId") };

        var act = () => ProductService.ValidateTargetTemplate("{userId}{serverId}", fields);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*serverId*");
    }

    // ── Kasus tepi ────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyTemplate_Throws(string template)
    {
        var fields = new List<GameInputField> { Field("userId") };

        var act = () => ProductService.ValidateTargetTemplate(template, fields);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TemplateWithoutAnyPlaceholder_Throws()
    {
        var fields = new List<GameInputField> { Field("userId") };

        var act = () => ProductService.ValidateTargetTemplate("staticText", fields);

        act.Should().Throw<InvalidOperationException>();
    }
}
