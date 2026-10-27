using Microsoft.EntityFrameworkCore;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;

namespace RROMS.Tests.Orders;

/// <summary>Story 17: staff receive, process and update orders.</summary>
[Trait("Type", "Unit")]
[Trait("Story", "17")]
public class StaffOrderTests : DatabaseTest
{
    private const string ChangedByOthers =
        "This order was changed by someone else while you were looking at it. Please reload the page and try again.";

    /// <summary>Saves an order straight to the database in any status: 1 x Teh Tarik at RM 3.80.</summary>
    private async Task<Order> AddOrderAsync(string status = OrderStatus.Pending, int customer = Alice)
    {
        var order = new Order
        {
            CustomerUserId = customer, TableId = 4, Status = status,
            Items = { new OrderItem { MenuItemId = 7, Quantity = 1, UnitPrice = 3.80m } }
        };
        var db = NewContext();
        db.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    /// <summary>Saves a payment for the order straight to the database, in any state.</summary>
    private async Task AddPaymentAsync(int orderId, string status, string method)
    {
        var paidAt = status is PaymentStatus.Paid or PaymentStatus.Refunded ? DateTime.Now : (DateTime?)null;
        var db = NewContext();
        db.Payments.Add(new Payment
        {
            OrderId = orderId, Amount = 3.80m, PaymentMethod = method, PaymentStatus = status,
            CreatedAt = DateTime.Now, PaidAt = paidAt, RefundedAt = status == PaymentStatus.Refunded ? DateTime.Now : null
        });
        await db.SaveChangesAsync();
    }

    private Task<Order> ReloadAsync(int orderId) =>
        NewContext().Set<Order>().AsNoTracking().SingleAsync(o => o.OrderId == orderId);

    /// <summary>Presses a status button with the order's current RowVersion, as a freshly loaded queue page would.</summary>
    private async Task<OrderResult> MoveAsync(int orderId, string newStatus) =>
        await NewOrderService().ChangeStatusAsync(orderId, newStatus, (await ReloadAsync(orderId)).RowVersion);

    private async Task<OrderQueueItem?> FindInQueueAsync(int orderId, string? statusFilter = null) =>
        (await NewOrderService().GetQueueAsync(statusFilter)).SingleOrDefault(item => item.Order.OrderId == orderId);

    [Fact(DisplayName = "TC-17-01 A new customer order appears on the queue, with its table and dishes, not paid")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task NewOrderIsQueued()
    {
        var input = new OrderInput
        {
            TableId = 6,
            Items = { new OrderLineInput { MenuItemId = 1, Quantity = 2 }, new OrderLineInput { MenuItemId = 7, Quantity = 1 } }
        };
        var order = (await NewOrderService().CreateAsync(Alice, input)).Order!;

        var item = await FindInQueueAsync(order.OrderId);

        Assert.NotNull(item);
        Assert.Equal(6, item.Order.TableId);
        Assert.Equal(new[] { "2 x Nasi Lemak", "1 x Teh Tarik" }, item.Lines.Select(l => $"{l.Item.Quantity} x {l.MenuItemName}"));
        Assert.Null(item.Payment);
        Assert.Equal("Not paid", OrderFormatter.PaymentLabel(item.Payment));
        Assert.Equal(new[] { OrderStatus.Preparing, OrderStatus.Cancelled }, item.AllowedStatusChanges);
    }

    [Fact(DisplayName = "TC-17-02 Move an order through every status to Completed, with only the next step offered each time")]
    [Trait("Technique", "State transition")]
    public async Task FullLifecycle()
    {
        var order = await AddOrderAsync();
        var steps = new (string Next, string[] OfferedAfter)[]
        {
            (OrderStatus.Preparing, new[] { OrderStatus.Ready }),
            (OrderStatus.Ready, new[] { OrderStatus.Served }),
            (OrderStatus.Served, new[] { OrderStatus.Completed }),
            (OrderStatus.Completed, Array.Empty<string>())
        };

        foreach (var (next, offeredAfter) in steps)
        {
            if (next == OrderStatus.Served)
                await AddPaymentAsync(order.OrderId, PaymentStatus.Paid, PaymentMethod.Cash);   // paid while being served
            var result = await MoveAsync(order.OrderId, next);

            Assert.True(result.Succeeded, result.ErrorMessage);
            Assert.Equal(next, (await ReloadAsync(order.OrderId)).Status);
            Assert.Equal(offeredAfter, (await FindInQueueAsync(order.OrderId, next))!.AllowedStatusChanges);
        }

        Assert.Null(await FindInQueueAsync(order.OrderId));                             // left the active queue
        Assert.NotNull(await FindInQueueAsync(order.OrderId, OrderStatus.Completed));   // listed under "Completed today"
    }

    [Theory(DisplayName = "TC-17-03 Skipping a step: rejected")]
    [Trait("Technique", "State transition")]
    [InlineData(OrderStatus.Pending, OrderStatus.Ready)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Served)]
    [InlineData(OrderStatus.Ready, OrderStatus.Completed)]
    public async Task SkippingAStep(string from, string to)
    {
        var order = await AddOrderAsync(from);

        var result = await MoveAsync(order.OrderId, to);

        Assert.Equal($"A {from} order can't be changed to {to}.", result.ErrorMessage);
        Assert.Equal(from, (await ReloadAsync(order.OrderId)).Status);
    }

    [Theory(DisplayName = "TC-17-04 Reopening a completed order: rejected")]
    [Trait("Technique", "State transition")]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Served)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task ReopeningACompletedOrder(string to)
    {
        var order = await AddOrderAsync(OrderStatus.Completed);

        var result = await MoveAsync(order.OrderId, to);

        Assert.Equal($"A completed order can't be changed to {to}.", result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-17-05 Cancel a pending order: Cancelled, off the queue, listed under 'Cancelled today'")]
    [Trait("Technique", "State transition")]
    public async Task CancelAPendingOrder()
    {
        var order = await AddOrderAsync();

        var result = await MoveAsync(order.OrderId, OrderStatus.Cancelled);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(OrderStatus.Cancelled, (await ReloadAsync(order.OrderId)).Status);
        Assert.Null(await FindInQueueAsync(order.OrderId));
        Assert.Empty((await FindInQueueAsync(order.OrderId, OrderStatus.Cancelled))!.AllowedStatusChanges);
    }

    [Fact(DisplayName = "TC-17-06 Cancel an order that is already preparing: rejected")]
    [Trait("Technique", "State transition")]
    public async Task CancelAPreparingOrder()
    {
        var order = await AddOrderAsync(OrderStatus.Preparing);

        var result = await MoveAsync(order.OrderId, OrderStatus.Cancelled);

        Assert.Equal("A preparing order can't be changed to cancelled.", result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-17-07 A paid order: shown as Paid, Cancel isn't offered, and forcing it is rejected")]
    [Trait("Technique", "Decision table")]
    public async Task PaidOrderCantBeCancelled()
    {
        var order = await AddOrderAsync();
        var db = NewContext();
        db.Payments.Add(new Payment
        {
            OrderId = order.OrderId, Amount = 3.80m, PaymentMethod = PaymentMethod.Cash, PaymentStatus = PaymentStatus.Paid,
            CreatedAt = DateTime.Now, PaidAt = DateTime.Now, RecordedByStaffId = 1
        });
        await db.SaveChangesAsync();

        var item = await FindInQueueAsync(order.OrderId);
        var result = await MoveAsync(order.OrderId, OrderStatus.Cancelled);

        Assert.Equal("Paid", OrderFormatter.PaymentLabel(item!.Payment));
        Assert.Equal(new[] { OrderStatus.Preparing }, item.AllowedStatusChanges);
        Assert.Equal("This order has been paid, so it can't be cancelled. Refund the payment first.", result.ErrorMessage);
        Assert.Equal(OrderStatus.Pending, (await ReloadAsync(order.OrderId)).Status);
    }

    [Fact(DisplayName = "TC-17-08 Two staff press Start preparing on the same order: the second is told it's already preparing")]
    [Trait("Technique", "Concurrency")]
    public async Task TwoStaffPressTheSameButton()
    {
        var order = await AddOrderAsync();
        var loadedVersion = (await ReloadAsync(order.OrderId)).RowVersion;   // both queue pages loaded this version

        var first = await NewOrderService().ChangeStatusAsync(order.OrderId, OrderStatus.Preparing, loadedVersion);
        var second = await NewOrderService().ChangeStatusAsync(order.OrderId, OrderStatus.Preparing, loadedVersion);

        Assert.True(first.Succeeded, first.ErrorMessage);
        Assert.Equal("A preparing order can't be changed to preparing.", second.ErrorMessage);
    }

    [Fact(DisplayName = "TC-17-09 A button pressed on an out-of-date queue page: 'changed by someone else'")]
    [Trait("Technique", "Concurrency")]
    public async Task OutOfDatePage()
    {
        var order = await AddOrderAsync();
        var loadedVersion = (await ReloadAsync(order.OrderId)).RowVersion;
        await MoveAsync(order.OrderId, OrderStatus.Preparing);   // someone else moved it on in the meantime

        var result = await NewOrderService().ChangeStatusAsync(order.OrderId, OrderStatus.Ready, loadedVersion);

        Assert.Equal(ChangedByOthers, result.ErrorMessage);
        Assert.Equal(OrderStatus.Preparing, (await ReloadAsync(order.OrderId)).Status);
    }

    [Fact(DisplayName = "TC-17-10 The queue: active orders only, oldest first, and the status filter works")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task QueueOrderAndFilter()
    {
        var oldest = await AddOrderAsync();
        var cooking = await AddOrderAsync(OrderStatus.Preparing);
        var ready = await AddOrderAsync(OrderStatus.Ready);
        await AddOrderAsync(OrderStatus.Completed);
        await AddOrderAsync(OrderStatus.Cancelled);

        var active = await NewOrderService().GetQueueAsync(null);
        var preparing = await NewOrderService().GetQueueAsync(OrderStatus.Preparing);

        Assert.Equal(new[] { oldest.OrderId, cooking.OrderId, ready.OrderId }, active.Select(item => item.Order.OrderId));
        Assert.Equal(new[] { cooking.OrderId }, preparing.Select(item => item.Order.OrderId));
    }

    [Fact(DisplayName = "TC-17-11 An order that doesn't exist, or a status that doesn't exist: rejected")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task UnknownOrderOrStatus()
    {
        var order = await AddOrderAsync();

        var noOrder = await NewOrderService().ChangeStatusAsync(99999999, OrderStatus.Preparing, new byte[8]);
        var noStatus = await MoveAsync(order.OrderId, "eaten");

        Assert.Equal("Order not found.", noOrder.ErrorMessage);
        Assert.Equal("A pending order can't be changed to eaten.", noStatus.ErrorMessage);
    }

    [Fact(DisplayName = "TC-17-12 The customer sees the status staff set")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task CustomerSeesTheNewStatus()
    {
        var order = await AddOrderAsync();

        await MoveAsync(order.OrderId, OrderStatus.Preparing);

        Assert.Equal(OrderStatus.Preparing, (await NewOrderService().GetByIdAsync(Alice, order.OrderId))!.Order.Status);
        Assert.Equal(OrderStatus.Preparing, (await NewOrderService().GetForCustomerAsync(Alice)).Single().Order.Status);
    }

    [Theory(DisplayName = "TC-17-13 The payment shown on the queue for each payment state")]
    [Trait("Technique", "Equivalence partitioning")]
    [InlineData(null, null, "Not paid")]
    [InlineData(PaymentStatus.Failed, PaymentMethod.Card, "Not paid")]
    [InlineData(PaymentStatus.Pending, PaymentMethod.PayAtCounter, "Waiting at counter")]
    [InlineData(PaymentStatus.Pending, PaymentMethod.Card, "Paying online")]
    [InlineData(PaymentStatus.Paid, PaymentMethod.Cash, "Paid")]
    [InlineData(PaymentStatus.Refunded, PaymentMethod.Cash, "Refunded")]
    public void PaymentLabels(string? status, string? method, string expected)
    {
        var payment = status is null ? null : new PaymentDetails { Status = status, Method = method! };

        Assert.Equal(expected, OrderFormatter.PaymentLabel(payment));
    }

    // TC-17-17 to TC-17-25 are the browser and UAT tests in the report workbook.
    [Theory(DisplayName = "TC-17-26 Cancel a pending order for each payment state: refused while a payment is going or done")]
    [Trait("Technique", "Decision table")]
    [InlineData(null, null, null)]
    [InlineData(PaymentStatus.Failed, PaymentMethod.Card, null)]
    [InlineData(PaymentStatus.Refunded, PaymentMethod.Cash, null)]
    [InlineData(PaymentStatus.Pending, PaymentMethod.PayAtCounter,
        "This order is waiting to be paid at the counter. Cancel that payment under Counter payments first, then cancel the order.")]
    [InlineData(PaymentStatus.Pending, PaymentMethod.Card,
        "The customer is paying for this order online. Wait until the payment finishes or runs out, then try again.")]
    [InlineData(PaymentStatus.Paid, PaymentMethod.Cash,
        "This order has been paid, so it can't be cancelled. Refund the payment first.")]
    public async Task CancelForEachPaymentState(string? status, string? method, string? expectedError)
    {
        var order = await AddOrderAsync();
        if (status is not null)
            await AddPaymentAsync(order.OrderId, status, method!);

        var offered = (await FindInQueueAsync(order.OrderId))!.AllowedStatusChanges;
        var result = await MoveAsync(order.OrderId, OrderStatus.Cancelled);

        Assert.Equal(expectedError, result.ErrorMessage);
        Assert.Equal(expectedError is null, offered.Contains(OrderStatus.Cancelled));   // the queue offers Cancel only when it would work
        Assert.Equal(expectedError is null ? OrderStatus.Cancelled : OrderStatus.Pending, (await ReloadAsync(order.OrderId)).Status);
    }

    [Theory(DisplayName = "TC-17-29 Complete a served order for each payment state: only once it's paid (or the payment was refunded)")]
    [Trait("Technique", "Decision table")]
    [InlineData(null, null, false)]
    [InlineData(PaymentStatus.Pending, PaymentMethod.PayAtCounter, false)]
    [InlineData(PaymentStatus.Pending, PaymentMethod.Card, false)]
    [InlineData(PaymentStatus.Failed, PaymentMethod.Card, false)]
    [InlineData(PaymentStatus.Paid, PaymentMethod.Cash, true)]
    [InlineData(PaymentStatus.Refunded, PaymentMethod.Cash, true)]
    public async Task CompleteForEachPaymentState(string? status, string? method, bool allowed)
    {
        var order = await AddOrderAsync(OrderStatus.Served);
        if (status is not null)
            await AddPaymentAsync(order.OrderId, status, method!);

        var offered = (await FindInQueueAsync(order.OrderId))!.AllowedStatusChanges;
        var result = await MoveAsync(order.OrderId, OrderStatus.Completed);

        Assert.Equal(allowed ? null : "This order hasn't been paid yet. Take the payment before marking it as completed.", result.ErrorMessage);
        Assert.Equal(allowed, offered.Contains(OrderStatus.Completed));   // the queue offers Complete only when it would work
        Assert.Equal(allowed ? OrderStatus.Completed : OrderStatus.Served, (await ReloadAsync(order.OrderId)).Status);
    }
}
