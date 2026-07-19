using be_digitalin.Infrastructure.Serialization;
using be_digitalin.Models;
using FluentAssertions;
using Xunit;

namespace be_digitalin.Tests.Serialization;

public class GameInputSchemaSerializerTests
{
    private static List<GameInputField> SampleFields() =>
    [
        new GameInputField
        {
            Key = "userId",
            Label = "User ID",
            Placeholder = "Masukkan User ID",
            Type = "number",
            Required = true,
            Order = 1,
            Options = []
        },
        new GameInputField
        {
            Key = "server",
            Label = "Server",
            Placeholder = "Pilih server",
            Type = "select",
            Required = false,
            Order = 2,
            Options = ["Asia", "Europe"]
        }
    ];

    [Fact]
    public void SerializeThenDeserialize_RoundTripsAllProperties()
    {
        var original = SampleFields();

        var json = GameInputSchemaSerializer.Serialize(original);
        var result = GameInputSchemaSerializer.Deserialize(json);

        result.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void Serialize_UsesCamelCasePropertyNames()
    {
        var json = GameInputSchemaSerializer.Serialize(SampleFields());

        json.Should().Contain("\"key\"");
        json.Should().Contain("\"label\"");
        json.Should().Contain("\"required\"");
        // Tidak boleh PascalCase
        json.Should().NotContain("\"Key\"");
        json.Should().NotContain("\"Label\"");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Deserialize_NullOrEmpty_ReturnsEmptyList(string? json)
    {
        GameInputSchemaSerializer.Deserialize(json).Should().BeEmpty();
    }

    [Fact]
    public void Deserialize_CaseInsensitive_StillMapsProperties()
    {
        // JSON dengan PascalCase (data lama / sumber berbeda) tetap harus terbaca
        const string pascalJson = """
        [
          { "Key": "userId", "Label": "User ID", "Placeholder": "isi", "Type": "number", "Required": true, "Order": 1, "Options": [] }
        ]
        """;

        var result = GameInputSchemaSerializer.Deserialize(pascalJson);

        result.Should().ContainSingle();
        result[0].Key.Should().Be("userId");
        result[0].Label.Should().Be("User ID");
        result[0].Type.Should().Be("number");
        result[0].Required.Should().BeTrue();
        result[0].Order.Should().Be(1);
    }

    [Fact]
    public void Deserialize_InvalidJson_ThrowsInvalidOperationException()
    {
        const string corrupt = "{ this is not valid json ]";

        var act = () => GameInputSchemaSerializer.Deserialize(corrupt);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*tidak valid*")
            .WithInnerException<System.Text.Json.JsonException>();
    }

    [Fact]
    public void Deserialize_EmptyJsonArray_ReturnsEmptyList()
    {
        GameInputSchemaSerializer.Deserialize("[]").Should().BeEmpty();
    }
}
