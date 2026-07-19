using be_digitalin.Constants;
using be_digitalin.Services.GameTemplates;
using FluentAssertions;
using Xunit;

namespace be_digitalin.Tests.GameTemplates;

public class GameTemplateFactoryTests
{
    [Fact]
    public void All_ContainsEveryEnumValue()
    {
        var enumValues = Enum.GetValues<GameTemplateType>();

        GameTemplateFactory.All.Should().HaveCount(enumValues.Length);
        GameTemplateFactory.All.Select(t => t.Type)
            .Should().BeEquivalentTo(enumValues);
    }

    [Fact]
    public void Get_ReturnsTemplateWithMatchingType()
    {
        foreach (var type in Enum.GetValues<GameTemplateType>())
        {
            GameTemplateFactory.Get(type).Type.Should().Be(type);
        }
    }

    // ── Preset penting: UserId ─────────────────────────────────────
    [Fact]
    public void UserId_HasSingleNumberFieldAndCorrectTemplate()
    {
        var template = GameTemplateFactory.Get(GameTemplateType.UserId);

        template.DisplayName.Should().Be("User ID");
        template.TargetTemplate.Should().Be($"{{{AppConstants.GameFieldKeys.UserId}}}");

        template.InputFields.Should().ContainSingle();
        var field = template.InputFields[0];
        field.Key.Should().Be(AppConstants.GameFieldKeys.UserId);
        field.Label.Should().Be("User ID");
        field.Placeholder.Should().Be("Masukkan User ID");
        field.Type.Should().Be(AppConstants.GameInputFieldTypes.Number);
        field.Required.Should().BeTrue();
        field.Order.Should().Be(1);
    }

    // ── Preset dua-field: UserId + Server ──────────────────────────
    [Fact]
    public void UserIdServer_HasTwoOrderedFieldsAndCompositeTemplate()
    {
        var template = GameTemplateFactory.Get(GameTemplateType.UserIdServer);

        template.TargetTemplate.Should().Be(
            $"{{{AppConstants.GameFieldKeys.UserId}}}{{{AppConstants.GameFieldKeys.Server}}}");

        template.InputFields.Should().HaveCount(2);
        template.InputFields[0].Key.Should().Be(AppConstants.GameFieldKeys.UserId);
        template.InputFields[0].Order.Should().Be(1);
        template.InputFields[1].Key.Should().Be(AppConstants.GameFieldKeys.Server);
        template.InputFields[1].Order.Should().Be(2);
    }

    [Fact]
    public void UserIdZone_HasZoneFieldAndCompositeTemplate()
    {
        var template = GameTemplateFactory.Get(GameTemplateType.UserIdZone);

        template.TargetTemplate.Should().Be(
            $"{{{AppConstants.GameFieldKeys.UserId}}}{{{AppConstants.GameFieldKeys.Zone}}}");
        template.InputFields.Should().HaveCount(2);
        template.InputFields[1].Key.Should().Be(AppConstants.GameFieldKeys.Zone);
    }

    // ── Setiap field di setiap preset harus konsisten ──────────────
    [Fact]
    public void EveryTemplate_HasNonEmptyFieldsAndTemplate()
    {
        foreach (var template in GameTemplateFactory.All)
        {
            template.DisplayName.Should().NotBeNullOrWhiteSpace();
            template.TargetTemplate.Should().NotBeNullOrWhiteSpace();
            template.InputFields.Should().NotBeEmpty(
                $"preset {template.Type} harus punya minimal satu field");

            foreach (var field in template.InputFields)
            {
                field.Key.Should().NotBeNullOrWhiteSpace();
                field.Label.Should().NotBeNullOrWhiteSpace();
                field.Type.Should().BeOneOf(
                    AppConstants.GameInputFieldTypes.Text,
                    AppConstants.GameInputFieldTypes.Number,
                    AppConstants.GameInputFieldTypes.Select);
                field.Order.Should().BeGreaterThan(0);
            }
        }
    }

    // ── TargetTemplate hanya boleh mereferensikan key yang ada ─────
    [Fact]
    public void EveryTemplate_TargetTemplateOnlyReferencesExistingKeys()
    {
        foreach (var template in GameTemplateFactory.All)
        {
            var availableKeys = template.InputFields
                .Select(f => f.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Ekstrak semua {key} dari target template
            var referenced = System.Text.RegularExpressions.Regex
                .Matches(template.TargetTemplate, @"\{(?<key>[^{}]+)\}")
                .Select(m => m.Groups["key"].Value);

            foreach (var key in referenced)
            {
                availableKeys.Should().Contain(key,
                    $"preset {template.Type} memakai key '{key}' di target template");
            }
        }
    }

    // ── Default preset ─────────────────────────────────────────────
    [Fact]
    public void DefaultType_MatchesDefaultTemplateHelpers()
    {
        GameTemplateFactory.DefaultType.Should().Be(GameTemplateType.UserId);

        var defaultFields = GameTemplateFactory.CreateDefaultInputFields();
        defaultFields.Should().ContainSingle();
        defaultFields[0].Key.Should().Be(AppConstants.GameFieldKeys.UserId);

        GameTemplateFactory.DefaultTargetTemplate.Should().Be(
            GameTemplateFactory.Get(GameTemplateType.UserId).TargetTemplate);
    }

    // ── CreateDefaultInputFields mengembalikan salinan (bukan shared ref) ──
    [Fact]
    public void CreateDefaultInputFields_ReturnsIndependentCopies()
    {
        var first = GameTemplateFactory.CreateDefaultInputFields();
        first[0].Label = "MUTATED";

        var second = GameTemplateFactory.CreateDefaultInputFields();
        second[0].Label.Should().NotBe("MUTATED",
            "preset tidak boleh termutasi oleh caller sebelumnya");
    }
}
