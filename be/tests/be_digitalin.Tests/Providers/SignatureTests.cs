using Xunit;
using System.Security.Cryptography;
using System.Text;
using be_digitalin.Config;
using be_digitalin.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace be_digitalin.Tests;

public class MidtransSignatureTests
{
    private const string ServerKey = "SB-Mid-test-server-key";
    private static MidtransProvider NewProvider() =>
        new(new HttpClient(), Options.Create(new MidtransSettings { ServerKey = ServerKey }), null!);

    private static string Sha512Hex(string input)
    {
        var hash = SHA512.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLower();
    }

    [Fact]
    public void Valid_signature_accepted()
    {
        var orderId = "INV-20260906-ABCDEF1234";
        var statusCode = "200";
        var grossAmount = "15199.00";
        var signature = Sha512Hex(orderId + statusCode + grossAmount + ServerKey);

        Assert.True(NewProvider().ValidateSignature(orderId, statusCode, grossAmount, signature));
    }

    [Fact]
    public void Tampered_signature_rejected()
    {
        var signature = Sha512Hex("INV-1" + "200" + "100.00" + ServerKey);

        Assert.False(NewProvider().ValidateSignature("INV-2", "200", "100.00", signature));
    }

    [Theory]
    [InlineData("", "200", "100.00", "abc")]
    [InlineData("INV-1", "", "100.00", "abc")]
    [InlineData("INV-1", "200", "", "abc")]
    [InlineData("INV-1", "200", "100.00", "")]
    public void Empty_inputs_rejected(string orderId, string statusCode, string grossAmount, string signature) =>
        Assert.False(NewProvider().ValidateSignature(orderId, statusCode, grossAmount, signature));
}

public class DigiflazzWebhookSignatureTests
{
    private const string Secret = "webhook-secret";
    private static DigiflazzProvider NewProvider() =>
        new(new HttpClient(),
            Options.Create(new DigiflazzSettings { WebhookSecret = Secret }),
            NullLogger<DigiflazzProvider>.Instance);

    private static string HmacSha1(string secret, string body)
    {
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return "sha1=" + Convert.ToHexString(hash).ToLower();
    }

    [Fact]
    public void Valid_signature_accepted()
    {
        var body = """{"data":{"ref_id":"INV-1","status":"Sukses"}}""";

        Assert.True(NewProvider().ValidateWebhookSignature(body, HmacSha1(Secret, body)));
    }

    [Fact]
    public void Wrong_secret_rejected()
    {
        var body = """{"data":{"ref_id":"INV-1"}}""";

        Assert.False(NewProvider().ValidateWebhookSignature(body, HmacSha1("other-secret", body)));
    }

    [Fact]
    public void Missing_prefix_rejected()
    {
        var body = """{"data":{}}""";

        Assert.False(NewProvider().ValidateWebhookSignature(body, "rawhexwithoutprefix"));
    }

    [Theory]
    [InlineData("", "sha1=abc")]
    [InlineData("body", "")]
    public void Empty_inputs_rejected(string body, string header) =>
        Assert.False(NewProvider().ValidateWebhookSignature(body, header));
}
