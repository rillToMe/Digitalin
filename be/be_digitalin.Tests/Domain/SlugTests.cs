using Xunit;
using be_digitalin.Domain;

namespace be_digitalin.Tests;

public class SlugTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    [InlineData("!@#")]
    public void Garbage_input_yields_null(string? input) =>
        Assert.Null(Slug.From(input));

    [Theory]
    [InlineData("Mobile Legends", "mobile-legends")]
    [InlineData("E-Money", "e-money")]
    [InlineData("Konsol, Game", "konsol-game")]
    [InlineData("  FREE  FIRE  ", "free-fire")]
    [InlineData("PUBG® MOBILE!!", "pubg-mobile")]
    [InlineData("A_2024", "a-2024")]
    public void Normalizes_to_kebab_case_slug(string input, string expected) =>
        Assert.Equal(expected, Slug.From(input));
}
