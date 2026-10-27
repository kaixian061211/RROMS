using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Models.OrdersAndReservations;

namespace RROMS.Tests.Integration;

/// <summary>Story 12 through the customer pages: /Orders, Create and Details.</summary>
[Trait("Type", "Integration")]
[Trait("Story", "12")]
public class OrderPageTests : PageTest
{
    public OrderPageTests(TestWebApp app) : base(app) { }

    /// <summary>The form changes for choosing a table and some dishes.</summary>
    private static Dictionary<string, string?> Choices(IHtmlDocument orderPage, int table, params (int MenuItemId, int Quantity)[] dishes)
    {
        var changes = new Dictionary<string, string?> { ["Input.TableId"] = table.ToString() };
        foreach (var (menuItemId, quantity) in dishes)
            changes[QuantityField(orderPage, menuItemId)] = quantity.ToString();
        return changes;
    }

    private Task<List<Order>> SavedOrdersAsync() =>
        WithDatabaseAsync(db => db.Set<Order>().AsNoTracking().Include(o => o.Items).ToListAsync());

    [Fact(DisplayName = "TC-12-16 The order page lists only the 8 available dishes, and its only fields are the table, dish ids and quantities")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task TheOrderPage()
    {
        var page = await GetPageAsync("/Orders/Create");
        var form = MainForm(page);

        Assert.Equal(8, page.QuerySelectorAll("input[name$='.MenuItemId']").Length);
        Assert.Contains("Nasi Lemak", TextOf(page));
        Assert.DoesNotContain("Chicken Satay", TextOf(page));
        Assert.All(form.Elements.Select(e => e.GetAttribute("name")).OfType<string>(), name =>
            Assert.Matches(@"^(__RequestVerificationToken|Input\.TableId|Input\.Items\[\d+\]\.(MenuItemId|Quantity))$", name));
        // The tables are tiles: one radio button each, and none is chosen until the customer picks theirs.
        var tables = page.QuerySelectorAll<IHtmlInputElement>("input[name='Input.TableId']").ToList();
        Assert.All(tables, table => Assert.Equal("radio", table.Type));
        Assert.Equal(new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" }, tables.Select(table => table.Value));
        Assert.DoesNotContain(tables, table => table.IsChecked);
    }

    [Fact(DisplayName = "TC-12-17 Place an order with a fake price of RM 0.01 added to the form: the menu prices are used, total RM 37.20")]
    [Trait("Technique", "Security")]
    public async Task FakePriceIsIgnored()
    {
        var orderPage = await GetPageAsync("/Orders/Create");
        var changes = Choices(orderPage, table: 4, (NasiLemak, 2), (TehTarik, 3));
        changes["Input.Items[0].UnitPrice"] = "0.01";
        changes["Input.Total"] = "0.01";

        var details = await SubmitAsync(MainForm(orderPage), changes);

        var saved = Assert.Single(await SavedOrdersAsync());
        Assert.EndsWith($"/Orders/Details/{saved.OrderId}", details.Url);
        var shown = TextOf(details);
        Assert.Contains("Your order has been placed.", shown);
        Assert.Contains("Table 4", shown);
        Assert.Contains("RM 25.80", shown);
        Assert.Contains("RM 11.40", shown);
        Assert.Contains("RM 37.20", shown);
        Assert.Equal(37.20m, saved.Total);
        Assert.Equal((TestCustomer, OrderStatus.Pending), (saved.CustomerUserId, saved.Status));
    }

    [Fact(DisplayName = "TC-12-18 Forcing the unavailable Chicken Satay into the form: blocked by name, and the form keeps the customer's choices")]
    [Trait("Technique", "Security")]
    public async Task UnavailableDishForcedIn()
    {
        var orderPage = await GetPageAsync("/Orders/Create");
        var changes = Choices(orderPage, table: 2, (NasiLemak, 1));
        changes["Input.Items[8].MenuItemId"] = ChickenSatay.ToString();
        changes["Input.Items[8].Quantity"] = "1";

        var page = await SubmitAsync(MainForm(orderPage), changes);

        Assert.EndsWith("/Orders/Create", page.Url);
        Assert.Contains("Sorry, Chicken Satay (10 sticks) isn't available right now.", TextOf(page));
        Assert.Equal("2", page.QuerySelector<IHtmlInputElement>("input[name='Input.TableId']:checked")!.Value);
        Assert.Equal("1", page.QuerySelector<IHtmlInputElement>($"input[name='{QuantityField(page, NasiLemak)}']")!.Value);
        Assert.Empty(await SavedOrdersAsync());
    }

    [Fact(DisplayName = "TC-12-19 Another customer's order: 'Order not found.', and it isn't in My orders")]
    [Trait("Technique", "Security")]
    public async Task AnotherCustomersOrder()
    {
        var order = new Order
        {
            CustomerUserId = OtherCustomer, TableId = 5, Status = OrderStatus.Pending,
            Items = { new OrderItem { MenuItemId = TehTarik, Quantity = 1, UnitPrice = 3.80m } }
        };
        await WithDatabaseAsync(db => { db.Add(order); return db.SaveChangesAsync(); });

        var details = await GetPageAsync($"/Orders/Details/{order.OrderId}");
        var myOrders = await GetPageAsync("/Orders");

        Assert.Contains("Order not found.", TextOf(details));
        Assert.DoesNotContain(OrderFormatter.Reference(order.OrderId), TextOf(myOrders));
        Assert.Contains("You haven't placed any orders yet.", TextOf(myOrders));
    }
}
