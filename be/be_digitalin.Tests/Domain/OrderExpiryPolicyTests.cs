using be_digitalin.Constants;
using be_digitalin.Domain.Orders;
using Xunit;

namespace be_digitalin.Tests;

/// <summary>
/// SSOT expiry per metode: default = konfigurasi admin; cstore fixed 24 jam
/// (Midtrans mengabaikan expiry custom — verifikasi sandbox). Sweep dan
/// invoice bergantung pada ini untuk tidak mematikan order cstore hidup.
/// </summary>
public class OrderExpiryPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("qris")]
    [InlineData("gopay")]
    [InlineData("shopeepay")]
    [InlineData("bank_transfer")]
    [InlineData("echannel")]
    public void NonCstore_UsesConfiguredDefault(string? paymentType)
    {
        Assert.Equal(30, OrderExpiryPolicy.ExpiryMinutes(paymentType, 30));
        Assert.Equal(45, OrderExpiryPolicy.ExpiryMinutes(paymentType, 45));
    }

    [Fact]
    public void Cstore_Always24Hours_IgnoresDefault()
    {
        Assert.Equal(
            AppConstants.MidtransPaymentMethods.CstoreExpiryMinutes,
            OrderExpiryPolicy.ExpiryMinutes("cstore", 30));
        Assert.Equal(24 * 60, OrderExpiryPolicy.ExpiryMinutes("cstore", 30));
    }
}
