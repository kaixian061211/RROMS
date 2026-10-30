using Microsoft.EntityFrameworkCore;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.CustomerHistoryAndPreferences;
using RROMS.Web.Services.OrdersAndReservations;
using RROMS.Web.Services.Payments;

namespace RROMS.Tests.CustomerHistoryAndPreferences;

/// <summary>Story 15: a customer looks back at their past reservations, orders and payments.</summary>
[Trait("Type", "Unit")]
[Trait("Story", "15")]
public class CustomerHistoryTests : DatabaseTest
{
    private const int NasiLemak = 1;   // RM 12.90 on the test menu

    private CustomerHistoryService NewHistoryService()
    {
        var db = NewContext();
        return new CustomerHistoryService(
            db, NewOrderService(), new PaymentQueries(db, new PaymentOrderProvider(db)), new RestaurantLookup(db));
    }

    private async Task<int> PlaceOrderAsync(int customer)
    {
        var result = await NewOrderService().CreateAsync(customer, new OrderInput
        {
            TableId = 4,
            Items = new() { new OrderLineInput { MenuItemId = NasiLemak, Quantity = 2 } }
        });
        Assert.True(result.Succeeded, result.ErrorMessage);
        return result.Order!.OrderId;
    }

    private async Task AddPaidPaymentAsync(int orderId, decimal amount)
    {
        var db = NewContext();
        db.Add(new Payment
        {
            OrderId = orderId, Amount = amount, PaymentMethod = PaymentMethod.Cash,
            PaymentStatus = PaymentStatus.Paid, PaidAt = DateTime.Now
        });
        await db.SaveChangesAsync();
    }

    [Fact(DisplayName = "TC-15-07 History lists past reservations only: completed, cancelled or dated before today")]
    [Trait("Technique", "Decision table")]
    public async Task OnlyPastReservations()
    {
        var past = await AddReservationAsync(Alice, 1, Yesterday, Dinner, ReservationStatus.Completed);
        var cancelled = await AddReservationAsync(Alice, 2, Tomorrow, Dinner, ReservationStatus.Cancelled);
        var missed = await AddReservationAsync(Alice, 3, Yesterday, Lunch, ReservationStatus.Pending);
        await AddReservationAsync(Alice, 4, Tomorrow, Lunch, ReservationStatus.Confirmed);   // still to come
        await AddReservationAsync(Alice, 5, Tomorrow, Lunch, ReservationStatus.Pending);     // still to come

        var history = await NewHistoryService().GetAsync(Alice);

        // Newest date first; on the same day, the later time first.
        Assert.Equal(
            new[] { cancelled.ReservationId, past.ReservationId, missed.ReservationId },
            history.Reservations.Select(r => r.ReservationId).ToArray());
    }

    [Fact(DisplayName = "TC-15-08 History shows only this customer's records, never another customer's")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task OnlyTheirOwnRecords()
    {
        await AddReservationAsync(Bob, 1, Yesterday, Dinner, ReservationStatus.Completed);
        var bobsOrder = await PlaceOrderAsync(Bob);
        await AddPaidPaymentAsync(bobsOrder, 25.80m);
        var alicesOrder = await PlaceOrderAsync(Alice);

        var history = await NewHistoryService().GetAsync(Alice);

        Assert.Empty(history.Reservations);
        Assert.Equal(alicesOrder, Assert.Single(history.Orders).Order.OrderId);
        Assert.Empty(history.Payments);
    }

    [Fact(DisplayName = "TC-15-09 History shows the customer's orders with their totals and their payments with date, method and amount")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task OrdersAndPayments()
    {
        var orderId = await PlaceOrderAsync(Alice);
        await AddPaidPaymentAsync(orderId, 25.80m);

        var history = await NewHistoryService().GetAsync(Alice);

        var order = Assert.Single(history.Orders);
        Assert.Equal(25.80m, order.Order.Total);
        var payment = Assert.Single(history.Payments);
        Assert.Equal((orderId, PaymentMethod.Cash, 25.80m, PaymentStatus.Paid), (payment.OrderId, payment.Method, payment.Amount, payment.Status));
        Assert.NotNull(payment.PaidAt);
    }

    [Fact(DisplayName = "TC-15-10 A customer with no records gets empty lists")]
    [Trait("Technique", "Boundary value analysis")]
    public async Task NothingYet()
    {
        var history = await NewHistoryService().GetAsync(Alice);

        Assert.Empty(history.Reservations);
        Assert.Empty(history.Orders);
        Assert.Empty(history.Payments);
    }
}
