using System.Net;
using System.Security.Cryptography;
using System.Text;
using be_digitalin.Config;
using be_digitalin.Providers;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace be_digitalin.Tests.Providers;

/// <summary>
/// Menguji DigiflazzProvider.GetBalanceAsync: payload cek-saldo (cmd "deposit",
/// sign bersuffix "depo"), parsing angka di dalam wrapper "data", dan
/// penerusan pesan error Digiflazz saat "deposit" tidak dikirim.
/// </summary>
public class DigiflazzBalanceTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;

        public string? CapturedPath;
        public string? CapturedBody;

        public StubHandler(string body) => _body = body;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedPath = request.RequestUri?.AbsolutePath;
            CapturedBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }

    private static (DigiflazzProvider Provider, StubHandler Handler) Build(string responseBody)
    {
        var handler = new StubHandler(responseBody);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.digiflazz.test") };

        var settings = Options.Create(new DigiflazzSettings
        {
            Username = "user",
            ApiKey = "key",
            BaseUrl = "https://api.digiflazz.test"
        });

        return (new DigiflazzProvider(client, settings), handler);
    }

    private static string Md5(string raw) =>
        Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

    [Fact]
    public async Task GetBalance_ReadsDepositInsideDataWrapper()
    {
        var (provider, _) = Build("""{"data":{"deposit":500000000000}}""");

        (await provider.GetBalanceAsync()).Should().Be(500000000000m);
    }

    [Fact]
    public async Task GetBalance_PostsToCekSaldo_WithDepositCommandAndDepoSignature()
    {
        var (provider, handler) = Build("""{"data":{"deposit":1000}}""");

        await provider.GetBalanceAsync();

        handler.CapturedPath.Should().Be("/v1/cek-saldo");
        handler.CapturedBody.Should().Contain("\"cmd\":\"deposit\"");
        // Suffix sign wajib "depo", bukan nilai cmd. Kalau tertukar, Digiflazz
        // menolak dengan "signature tidak sesuai" dan saldo tidak pernah tampil.
        handler.CapturedBody.Should().Contain($"\"sign\":\"{Md5("userkeydepo")}\"");
    }

    [Fact]
    public async Task GetBalance_ThrowsWithDigiflazzMessage_WhenDepositMissing()
    {
        var (provider, _) = Build("""{"data":{"message":"IP Anda tidak terdaftar","rc":"41"}}""");

        var act = async () => await provider.GetBalanceAsync();

        await act.Should().ThrowAsync<Exception>().WithMessage("*IP Anda tidak terdaftar*");
    }

    [Fact]
    public async Task GetBalance_Throws_WhenResponseHasNoDataWrapper()
    {
        var (provider, _) = Build("""{"error":"forbidden"}""");

        var act = async () => await provider.GetBalanceAsync();

        await act.Should().ThrowAsync<Exception>();
    }
}
