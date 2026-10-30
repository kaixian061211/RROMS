using System.Reflection;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Data;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;
using RROMS.Web.Services.Payments.Gateways;
using Stripe.Checkout;

namespace RROMS.Tests.Payments;

[Trait("Type", "Unit")]
public class StripeReturnConfirmationTests
{
    // Exercise the same internal validator used by both trusted Stripe entry points.
    private static OnlinePaymentConfirmation? Validate(Session session)
    {
        var validator = typeof(StripeGateway).Assembly.GetType(
            "RROMS.Web.Services.Payments.Gateways.StripePaymentConfirmation", true)!;
        object?[] args = [session, null];
        var valid = (bool)validator.GetMethod("TryCreate")!.Invoke(null, args)!;
        Assert.Equal(valid, args[1] is not null);
        return (OnlinePaymentConfirmation?)args[1];
    }

    private static Session PaidSession() => new()
    {
        Id = "cs_test_stored", Mode = "payment", PaymentStatus = "paid",
        AmountTotal = 8550, Currency = "myr", ClientReferenceId = "7",
        PaymentIntentId = "pi_test_paid",
        Metadata = new() { ["payment_id"] = "7", ["order_id"] = "12", ["attempt_ticks"] = "123" }
    };

    [Fact]
    public void PaidSessionPreservesTrustedReferences()
    {
        var result = Assert.IsType<OnlinePaymentConfirmation>(Validate(PaidSession()));
        Assert.Equal(7, result.PaymentId);
        Assert.Equal(12, result.OrderId);
        Assert.Equal(8550, result.AmountTotal);
        Assert.Equal("cs_test_stored", result.SessionId);
        Assert.Equal("pi_test_paid", result.PaymentIntentId);
        Assert.Equal(123L, result.AttemptTicks);
    }

    [Theory]
    [InlineData("unpaid")]
    [InlineData("mismatched-payment")]
    [InlineData("missing-order")]
    [InlineData("invalid-attempt")]
    [InlineData("zero-amount")]
    [InlineData("wrong-mode")]
    [InlineData("oversized-session")]
    public void UnconfirmedOrInconsistentSessionsAreRejected(string problem)
    {
        var session = PaidSession();
        switch (problem)
        {
            case "unpaid": session.PaymentStatus = "unpaid"; break;
            case "mismatched-payment": session.Metadata["payment_id"] = "8"; break;
            case "missing-order": session.Metadata.Remove("order_id"); break;
            case "invalid-attempt": session.Metadata["attempt_ticks"] = "-1"; break;
            case "zero-amount": session.AmountTotal = 0; break;
            case "wrong-mode": session.Mode = "subscription"; break;
            case "oversized-session": session.Id = new string('x', 101); break;
        }
        Assert.Null(Validate(session));
    }

    [Fact]
    public void LegacySessionWithoutAttemptMetadataCanReachStoredReferenceValidation()
    {
        var session = PaidSession();
        session.Metadata.Remove("attempt_ticks");
        Assert.Null(Assert.IsType<OnlinePaymentConfirmation>(Validate(session)).AttemptTicks);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    public async Task InvalidRefreshIdsAreRejectedBeforeDatabaseOrStripeAccess(int paymentId, int customerId)
    {
        await using var db = new AppDbContext(new DbContextOptions<AppDbContext>());
        var delivery = new CounterPaymentTestServices.RecordingDelivery();
        var service = CounterPaymentTestServices.Create(db, delivery);
        await Assert.ThrowsAsync<PaymentException>(() => service.RefreshOnlineStatusAsync(paymentId, customerId));
        Assert.Empty(delivery.NotificationIds);
    }
}
