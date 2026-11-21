using Xunit;
using be_digitalin.Constants;
using be_digitalin.Domain.Orders;

namespace be_digitalin.Tests;

public class OrderStateTransitionTests
{
    [Theory]
    [InlineData(AppConstants.OrderStatus.Pending, AppConstants.OrderStatus.Paid)]
    [InlineData(AppConstants.OrderStatus.Pending, AppConstants.OrderStatus.Expired)]
    [InlineData(AppConstants.OrderStatus.Pending, AppConstants.OrderStatus.Failed)]
    [InlineData(AppConstants.OrderStatus.Pending, AppConstants.OrderStatus.Completed)]
    [InlineData(AppConstants.OrderStatus.Pending, AppConstants.OrderStatus.NeedsAction)]
    [InlineData(AppConstants.OrderStatus.Paid, AppConstants.OrderStatus.Processing)]
    [InlineData(AppConstants.OrderStatus.Paid, AppConstants.OrderStatus.Completed)]
    [InlineData(AppConstants.OrderStatus.Paid, AppConstants.OrderStatus.NeedsAction)]
    [InlineData(AppConstants.OrderStatus.Processing, AppConstants.OrderStatus.Completed)]
    [InlineData(AppConstants.OrderStatus.Processing, AppConstants.OrderStatus.NeedsAction)]
    [InlineData(AppConstants.OrderStatus.NeedsAction, AppConstants.OrderStatus.Processing)]
    [InlineData(AppConstants.OrderStatus.NeedsAction, AppConstants.OrderStatus.Completed)]
    [InlineData(AppConstants.OrderStatus.NeedsAction, AppConstants.OrderStatus.Refunded)]
    public void Legal_transitions_allowed(string from, string to) =>
        Assert.True(OrderStateTransition.CanTransition(from, to));

    [Theory]
    [InlineData(AppConstants.OrderStatus.Completed, AppConstants.OrderStatus.NeedsAction)]
    [InlineData(AppConstants.OrderStatus.Completed, AppConstants.OrderStatus.Refunded)]
    [InlineData(AppConstants.OrderStatus.Expired, AppConstants.OrderStatus.Paid)]
    [InlineData(AppConstants.OrderStatus.Failed, AppConstants.OrderStatus.Paid)]
    [InlineData(AppConstants.OrderStatus.Refunded, AppConstants.OrderStatus.Processing)]
    [InlineData(AppConstants.OrderStatus.Paid, AppConstants.OrderStatus.Pending)]
    [InlineData(AppConstants.OrderStatus.Refunded, AppConstants.OrderStatus.Completed)]
    public void Illegal_transitions_rejected(string from, string to) =>
        Assert.False(OrderStateTransition.CanTransition(from, to));

    [Fact]
    public void Same_status_idempotent_transition_allowed() =>
        Assert.True(OrderStateTransition.CanTransition(
            AppConstants.OrderStatus.Paid, AppConstants.OrderStatus.Paid));

    [Fact]
    public void Unknown_status_rejected() =>
        Assert.False(OrderStateTransition.CanTransition("UNKNOWN", AppConstants.OrderStatus.Paid));

    [Theory]
    [InlineData(AppConstants.OrderStatus.Expired, AppConstants.OrderStatus.NeedsAction, true)]
    [InlineData(AppConstants.OrderStatus.Failed, AppConstants.OrderStatus.NeedsAction, true)]
    [InlineData(AppConstants.OrderStatus.Expired, AppConstants.OrderStatus.Paid, false)]
    [InlineData(AppConstants.OrderStatus.Failed, AppConstants.OrderStatus.Paid, false)]
    [InlineData(AppConstants.OrderStatus.Completed, AppConstants.OrderStatus.NeedsAction, false)]
    [InlineData(AppConstants.OrderStatus.Refunded, AppConstants.OrderStatus.NeedsAction, false)]
    public void CanEscalateLatePayment_OnlyAllowsExpiredOrFailedToNeedsAction(
        string from, string to, bool expected) =>
        Assert.Equal(expected, OrderStateTransition.CanEscalateLatePayment(from, to));
}
