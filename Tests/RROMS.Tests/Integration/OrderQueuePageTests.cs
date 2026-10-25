using System.Net;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Models.Dev;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;

namespace RROMS.Tests.Integration;

/// <summary>Story 17 through the staff page /Orders/Queue.</summary>
[Trait("Type", "Integration")]
[Trait("Story", "17")]
public class OrderQueuePageTests : PageTest
{
    public OrderQueuePageTests(TestWebApp app) : base(app) { }

    protected override TestUser? SignedInAs => TestUsers.Staff;

    /// <summary>An order saved straight to the database: one Teh Tarik at table 4.</summary>
    private async Task<Order> AddOrderAsync()
    {
        var order = new Order
        {
            CustomerUserId = TestCustomer, TableId = 4, Status = OrderStatus.Pending,
            Items = { new OrderItem { MenuItemId = 7, Quantity = 1, UnitPrice = 3.80m } }
        };
        await WithDatabaseAsync(db => { db.Add(order); return db.SaveChangesAsync(); });
        return order;
    }

    private Task<string> SavedStatusAsync(int orderId) =>
        WithDatabaseAsync(db => db.Set<Order>().Where(o => o.OrderId == orderId).Select(o => o.Status).SingleAsync());

    /// <summary>The order's docket on the queue.</summary>
    private static IElement CardFor(IHtmlDocument queue, int orderId) =>
        queue.QuerySelectorAll(".rr-docket").Single(card => TextOf(card).Contains(OrderFormatter.Reference(orderId)));

    /// <summary>The board column the order's docket hangs in, such as "Pending".</summary>
    private static string? ColumnOf(IHtmlDocument queue, int orderId) =>
        CardFor(queue, orderId).Closest(".rr-board__column")?.GetAttribute("aria-label");

    private static IEnumerable<string> ButtonsOn(IElement card) => card.QuerySelectorAll("button").Select(TextOf);

    [Fact(DisplayName = "TC-17-14 A customer's new order is on the queue; Start preparing moves it on, and the customer sees Preparing")]
    [Trait("Technique", "State transition")]
    public async Task NewOrderThroughToPreparing()
    {
        await SignInAsAsync(TestUsers.CustomerA);
        var orderPage = await GetPageAsync("/Orders/Create");
        await SubmitAsync(MainForm(orderPage), new()
        {
            ["Input.TableId"] = "6",
            [QuantityField(orderPage, 1)] = "2",   // Nasi Lemak
            [QuantityField(orderPage, 7)] = "1"    // Teh Tarik
        });
        var orderId = await WithDatabaseAsync(db => db.Set<Order>().Select(o => o.OrderId).SingleAsync());

        await SignInAsAsync(TestUsers.Staff);
        var queue = await GetPageAsync("/Orders/Queue");
        var card = TextOf(CardFor(queue, orderId));
        Assert.Contains("Table 6", card);
        Assert.Contains("2 × Nasi Lemak", card);
        Assert.Contains("1 × Teh Tarik", card);
        Assert.Contains("RM 29.60", card);
        Assert.Equal("Pending", ColumnOf(queue, orderId));
        Assert.Contains("Not paid", card);
        Assert.Equal(new[] { "Start preparing", "Cancel" }, ButtonsOn(CardFor(queue, orderId)));

        var afterPress = await SubmitAsync(StatusButton(queue, orderId, "Start preparing")!);

        Assert.Contains($"{OrderFormatter.Reference(orderId)} is now preparing.", TextOf(afterPress));
        Assert.Equal(new[] { "Mark ready" }, ButtonsOn(CardFor(afterPress, orderId)));
        Assert.Equal("Preparing", ColumnOf(afterPress, orderId));
        Assert.Equal(OrderStatus.Preparing, await SavedStatusAsync(orderId));
        await SignInAsAsync(TestUsers.CustomerA);
        Assert.Contains("Preparing", TextOf(await GetPageAsync($"/Orders/Details/{orderId}")));
    }

    [Fact(DisplayName = "TC-17-15 A paid order: shown as Paid with no Cancel button, and a forced cancel is rejected")]
    [Trait("Technique", "Decision table")]
    public async Task PaidOrder()
    {
        var order = await AddOrderAsync();
        await WithDatabaseAsync(db =>
        {
            db.Payments.Add(new Payment
            {
                OrderId = order.OrderId, Amount = 3.80m, PaymentMethod = PaymentMethod.Cash, PaymentStatus = PaymentStatus.Paid,
                CreatedAt = DateTime.Now, PaidAt = DateTime.Now, RecordedByStaffId = 1
            });
            return db.SaveChangesAsync();
        });

        var queue = await GetPageAsync("/Orders/Queue");

        Assert.Contains("Paid", TextOf(CardFor(queue, order.OrderId)));
        Assert.Equal(new[] { "Start preparing" }, ButtonsOn(CardFor(queue, order.OrderId)));

        // Someone editing the request: the Start preparing form, changed to ask for cancelled.
        var page = await SubmitAsync(StatusButton(queue, order.OrderId, "Start preparing")!,
            new() { ["newStatus"] = OrderStatus.Cancelled });

        Assert.Contains("This order has been paid, so it can't be cancelled. Refund the payment first.", TextOf(page));
        Assert.Equal(OrderStatus.Pending, await SavedStatusAsync(order.OrderId));
    }

    [Fact(DisplayName = "TC-17-16 A status button pressed without the anti-forgery token: refused (400), status unchanged")]
    [Trait("Technique", "Security")]
    public async Task ButtonWithoutTheAntiForgeryToken()
    {
        var order = await AddOrderAsync();
        var queue = await GetPageAsync("/Orders/Queue");

        var response = await SendFormAsync(StatusButton(queue, order.OrderId, "Start preparing")!,
            new() { ["__RequestVerificationToken"] = null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(OrderStatus.Pending, await SavedStatusAsync(order.OrderId));
    }
}
