using be_digitalin.Services.Orders;
using Xunit;

namespace be_digitalin.Tests.Services;

/// <summary>
/// Helper SSOT derivasi PublicHash: format (hex kecil 64), deterministik,
/// keyed (berubah bila secret berubah), dan deteksi format 64-hex.
/// </summary>
public class OrderPublicHashTests
{
    private const string SecretA = "secret-a-0123456789abcdef0123456789abcdef";
    private const string SecretB = "secret-b-fedcba9876543210fedcba9876543210";

    [Fact]
    public void Compute_Returns64LowercaseHex()
    {
        var hash = OrderPublicHash.Compute("INV-20260906-ABCDEF1234", SecretA);

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void Compute_Deterministic_ForSameOrderNoAndSecret()
    {
        var a1 = OrderPublicHash.Compute("INV-20260906-ABCDEF1234", SecretA);
        var a2 = OrderPublicHash.Compute("INV-20260906-ABCDEF1234", SecretA);

        Assert.Equal(a1, a2);
    }

    [Fact]
    public void Compute_Keyed_DifferentSecretDifferentHash()
    {
        // Keyed HMAC: mengetahui OrderNo (dan hash lama) tidak cukup
        // merekonstruksi hash kunci lain.
        var byA = OrderPublicHash.Compute("INV-20260906-ABCDEF1234", SecretA);
        var byB = OrderPublicHash.Compute("INV-20260906-ABCDEF1234", SecretB);

        Assert.NotEqual(byA, byB);
    }

    [Fact]
    public void Compute_DifferentOrderNoDifferentHash()
    {
        var h1 = OrderPublicHash.Compute("INV-20260906-ABCDEF1234", SecretA);
        var h2 = OrderPublicHash.Compute("INV-20260906-ABCDEF5678", SecretA);

        Assert.NotEqual(h1, h2);
    }

    [Theory]
    [InlineData("000000000000000000000000000000000000000000000000000000000000000a")] // 64 hex → true
    [InlineData("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF")] // 64 hex uppercase → true
    [InlineData("INV-20260906-ABCDEF1234")] // OrderNo → false
    [InlineData("abc")]                     // pendek → false
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")] // 64 non-hex → false
    [InlineData("")]                        // kosong → false
    public void IsPublicHashFormat_ClassifiesCorrectly(string reference)
    {
        var expected = reference.Length == 64 && reference.All(Uri.IsHexDigit);

        Assert.Equal(expected, OrderPublicHash.IsPublicHashFormat(reference));
    }
}
