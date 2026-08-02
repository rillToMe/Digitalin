using be_digitalin.Constants;
using be_digitalin.Domain.Orders;
using FluentAssertions;
using Xunit;

namespace be_digitalin.Tests.Domain;

public class OrderStateTransitionTests
{
    // ── Transisi yang diizinkan (happy path) ──────────────────────
    public static IEnumerable<object[]> AllowedTransitions()
    {
        yield return new object[] { AppConstants.OrderStatus.Pending, AppConstants.OrderStatus.Paid };
        yield return new object[] { AppConstants.OrderStatus.Pending, AppConstants.OrderStatus.Expired };
        yield return new object[] { AppConstants.OrderStatus.Pending, AppConstants.OrderStatus.Failed };
        yield return new object[] { AppConstants.OrderStatus.Paid, AppConstants.OrderStatus.Processing };
        yield return new object[] { AppConstants.OrderStatus.Paid, AppConstants.OrderStatus.Completed };
        yield return new object[] { AppConstants.OrderStatus.Paid, AppConstants.OrderStatus.NeedsAction };
        yield return new object[] { AppConstants.OrderStatus.Processing, AppConstants.OrderStatus.Completed };
        yield return new object[] { AppConstants.OrderStatus.Processing, AppConstants.OrderStatus.NeedsAction };
        yield return new object[] { AppConstants.OrderStatus.NeedsAction, AppConstants.OrderStatus.Processing };
        yield return new object[] { AppConstants.OrderStatus.NeedsAction, AppConstants.OrderStatus.Completed };
        yield return new object[] { AppConstants.OrderStatus.NeedsAction, AppConstants.OrderStatus.Refunded };
        // Webhook Digiflazz async: bisa tiba sebelum tulisan PROCESSING lokal
        yield return new object[] { AppConstants.OrderStatus.Pending, AppConstants.OrderStatus.Completed };
        yield return new object[] { AppConstants.OrderStatus.Pending, AppConstants.OrderStatus.NeedsAction };
    }

    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public void CanTransition_AllowsValidTransitions(string from, string to)
    {
        OrderStateTransition.CanTransition(from, to).Should().BeTrue(
            $"transisi {from} → {to} seharusnya diizinkan");
    }

    // ── Transisi yang ditolak ─────────────────────────────────────
    public static IEnumerable<object[]> DisallowedTransitions()
    {
        // Status final tidak boleh keluar
        yield return new object[] { AppConstants.OrderStatus.Completed, AppConstants.OrderStatus.NeedsAction };
        yield return new object[] { AppConstants.OrderStatus.Completed, AppConstants.OrderStatus.Processing };
        yield return new object[] { AppConstants.OrderStatus.Refunded, AppConstants.OrderStatus.Completed };
        yield return new object[] { AppConstants.OrderStatus.Expired, AppConstants.OrderStatus.Paid };
        yield return new object[] { AppConstants.OrderStatus.Failed, AppConstants.OrderStatus.Paid };
        // Lompatan mundur / tidak logis
        yield return new object[] { AppConstants.OrderStatus.Paid, AppConstants.OrderStatus.Pending };
        yield return new object[] { AppConstants.OrderStatus.Processing, AppConstants.OrderStatus.Paid };
        yield return new object[] { AppConstants.OrderStatus.Pending, AppConstants.OrderStatus.Processing };
    }

    [Theory]
    [MemberData(nameof(DisallowedTransitions))]
    public void CanTransition_RejectsInvalidTransitions(string from, string to)
    {
        OrderStateTransition.CanTransition(from, to).Should().BeFalse(
            $"transisi {from} → {to} seharusnya ditolak");
    }

    // ── Idempoten: status sama → sama selalu valid ────────────────
    [Theory]
    [InlineData(AppConstants.OrderStatus.Pending)]
    [InlineData(AppConstants.OrderStatus.Paid)]
    [InlineData(AppConstants.OrderStatus.Processing)]
    [InlineData(AppConstants.OrderStatus.Completed)]
    [InlineData(AppConstants.OrderStatus.NeedsAction)]
    [InlineData(AppConstants.OrderStatus.Refunded)]
    [InlineData(AppConstants.OrderStatus.Expired)]
    [InlineData(AppConstants.OrderStatus.Failed)]
    public void CanTransition_SameStatusIsIdempotent(string status)
    {
        OrderStateTransition.CanTransition(status, status).Should().BeTrue(
            "transisi ke status yang sama harus idempoten");
    }

    // ── Status tak dikenal ────────────────────────────────────────
    [Fact]
    public void CanTransition_UnknownFromStatus_ReturnsFalse()
    {
        OrderStateTransition.CanTransition("UNKNOWN", AppConstants.OrderStatus.Paid)
            .Should().BeFalse();
    }
}
