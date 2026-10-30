using Microsoft.EntityFrameworkCore;
using RROMS.Web.Models.Accounts;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;

namespace RROMS.Tests.Orders;

/// <summary>Story 12: a customer places food and beverage orders.</summary>
[Trait("Type", "Unit")]
[Trait("Story", "12")]
public class CustomerOrderTests : DatabaseTest
{
    // Dishes on the test menu (TestMenu.Dishes)
    private const int NasiLemak = 1;       // RM 12.90
    private const int CharKwayTeow = 3;    // RM 13.90
    private const int ChickenSatay = 6;    // RM 18.00, not available
    private const int TehTarik = 7;        // RM 3.80

    private static OrderInput OrderAt(int? table, params (int Dish, int Quantity)[] lines) => new()
    {
        TableId = table,
        Items = lines.Select(l => new OrderLineInput { MenuItemId = l.Dish, Quantity = l.Quantity }).ToList()
    };

    private Task<int> CountOrdersAsync() => NewContext().Set<Order>().CountAsync();

    [Fact(DisplayName = "TC-12-01 Order 2 x Nasi Lemak and 3 x Teh Tarik: menu prices copied, total RM 37.20")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task PlaceAnOrder()
    {
        var result = await NewOrderService().CreateAsync(Alice, OrderAt(4, (NasiLemak, 2), (TehTarik, 3)));

        Assert.True(result.Succeeded, result.ErrorMessage);
        var saved = await NewContext().Set<Order>().AsNoTracking().Include(o => o.Items)
            .SingleAsync(o => o.OrderId == result.Order!.OrderId);
        Assert.Equal((OrderStatus.Pending, 4), (saved.Status, saved.TableId));
        Assert.Equal(12.90m, saved.Items.Single(i => i.MenuItemId == NasiLemak).UnitPrice);
        Assert.Equal(3.80m, saved.Items.Single(i => i.MenuItemId == TehTarik).UnitPrice);
        Assert.Equal(37.20m, saved.Total);
    }

    [Theory(DisplayName = "TC-12-02 Nothing chosen (no dishes, or every quantity 0): rejected")]
    [Trait("Technique", "Boundary value analysis")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NothingChosen(bool sendDishesWithQuantityZero)
    {
        var input = sendDishesWithQuantityZero ? OrderAt(4, (NasiLemak, 0), (TehTarik, 0)) : OrderAt(4);

        var result = await NewOrderService().CreateAsync(Alice, input);

        Assert.Equal("Please choose at least one item.", result.ErrorMessage);
        Assert.Equal(0, await CountOrdersAsync());
    }

    [Theory(DisplayName = "TC-12-03 Quantity 1 and 20 of a dish: accepted")]
    [Trait("Technique", "Boundary value analysis")]
    [InlineData(1)]
    [InlineData(20)]
    public async Task QuantityAtTheLimits(int quantity)
    {
        var result = await NewOrderService().CreateAsync(Alice, OrderAt(4, (TehTarik, quantity)));

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(quantity, result.Order!.Items.Single().Quantity);
    }

    [Theory(DisplayName = "TC-12-04 More than 20 of a dish (21, or 15 + 10 sent separately): rejected")]
    [Trait("Technique", "Boundary value analysis")]
    [InlineData(21, 0)]
    [InlineData(15, 10)]
    public async Task QuantityOverTheLimit(int first, int second)
    {
        var input = second == 0 ? OrderAt(4, (TehTarik, first)) : OrderAt(4, (TehTarik, first), (TehTarik, second));

        var result = await NewOrderService().CreateAsync(Alice, input);

        Assert.Equal("You can order up to 20 of each item.", result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-12-05 A negative quantity: rejected")]
    [Trait("Technique", "Boundary value analysis")]
    public async Task NegativeQuantity()
    {
        var result = await NewOrderService().CreateAsync(Alice, OrderAt(4, (NasiLemak, -1), (TehTarik, 3)));

        Assert.Equal("Quantities can't be negative.", result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-12-06 The same dish sent twice is combined into one line (2 + 1 = 3)")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task SameDishTwice()
    {
        var result = await NewOrderService().CreateAsync(Alice, OrderAt(4, (TehTarik, 2), (TehTarik, 1)));

        var line = Assert.Single(result.Order!.Items);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(11.40m, result.Order.Total);
    }

    [Fact(DisplayName = "TC-12-07 An unavailable dish, alone or with others: rejected by name, nothing saved")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task UnavailableDish()
    {
        var alone = await NewOrderService().CreateAsync(Alice, OrderAt(4, (ChickenSatay, 1)));
        var mixed = await NewOrderService().CreateAsync(Alice, OrderAt(4, (NasiLemak, 1), (ChickenSatay, 1)));

        const string expected = "Sorry, Chicken Satay (10 sticks) isn't available right now.";
        Assert.Equal(expected, alone.ErrorMessage);
        Assert.Equal(expected, mixed.ErrorMessage);
        Assert.Equal(0, await CountOrdersAsync());
    }

    [Fact(DisplayName = "TC-12-08 A dish id that isn't on the menu: rejected")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task DishNotOnTheMenu()
    {
        var result = await NewOrderService().CreateAsync(Alice, OrderAt(4, (999, 1)));

        Assert.Equal("One of the items you chose is no longer on the menu. Please check your order.", result.ErrorMessage);
    }

    [Theory(DisplayName = "TC-12-09 The table: 1 and 10 accepted, none, 0 and 11 rejected")]
    [Trait("Technique", "Boundary value analysis")]
    [InlineData(null, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public async Task TableBoundaries(int? table, bool accepted)
    {
        var result = await NewOrderService().CreateAsync(Alice, OrderAt(table, (TehTarik, 1)));

        Assert.Equal(accepted, result.Succeeded);
        if (!accepted)
            Assert.Equal("Please choose your table.", result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-12-10 A menu price changing after the order is placed doesn't change the order")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task PriceChangeLater()
    {
        var menu = new TestMenu();
        menu.Items[50] = new MenuItemInfo(50, "Seasonal Durian Cendol", 10.00m, IsAvailable: true, "Desserts");
        var order = (await NewOrderService(menu).CreateAsync(Alice, OrderAt(4, (50, 2)))).Order!;

        menu.Items[50] = menu.Items[50] with { Price = 15.00m };
        var later = await NewOrderService(menu).GetByIdAsync(Alice, order.OrderId);

        Assert.Equal(10.00m, later!.Lines.Single().Item.UnitPrice);
        Assert.Equal(20.00m, later.Order.Total);
    }

    [Fact(DisplayName = "TC-12-11 A dish taken off the menu still shows on old orders, as 'Menu item #50'")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task DishTakenOffTheMenu()
    {
        var menu = new TestMenu();
        menu.Items[50] = new MenuItemInfo(50, "Seasonal Durian Cendol", 10.00m, IsAvailable: true, "Desserts");
        var order = (await NewOrderService(menu).CreateAsync(Alice, OrderAt(4, (50, 1)))).Order!;

        menu.Items.Remove(50);
        var later = await NewOrderService(menu).GetByIdAsync(Alice, order.OrderId);

        Assert.Equal("Menu item #50", later!.Lines.Single().MenuItemName);
    }

    [Fact(DisplayName = "TC-12-12 Another customer's order can't be seen")]
    [Trait("Technique", "Security")]
    public async Task AnotherCustomersOrder()
    {
        var alices = (await NewOrderService().CreateAsync(Alice, OrderAt(4, (TehTarik, 1)))).Order!;

        Assert.Null(await NewOrderService().GetByIdAsync(Bob, alices.OrderId));
        Assert.Empty(await NewOrderService().GetForCustomerAsync(Bob));
    }

    [Fact(DisplayName = "TC-12-13 The payment module reads the same total as the order (RM 37.20)")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task PaymentsSeeTheSameTotal()
    {
        var order = (await NewOrderService().CreateAsync(Alice, OrderAt(4, (NasiLemak, 2), (TehTarik, 3)))).Order!;

        var forPayment = await new PaymentOrderProvider(NewContext()).GetOrderAsync(order.OrderId);

        Assert.Equal(37.20m, forPayment!.TotalAmount);
        Assert.Equal(Alice, forPayment.CustomerUserId);
    }

    [Fact(DisplayName = "TC-12-14 The order page's menu lists only the 8 available dishes")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task MenuShowsAvailableDishesOnly()
    {
        var menu = await NewOrderService().GetMenuAsync();

        Assert.Equal(8, menu.Count);
        Assert.DoesNotContain(menu, dish => dish.MenuItemId == ChickenSatay);
        Assert.All(menu, dish => Assert.True(dish.IsAvailable));
    }

    [Fact(DisplayName = "TC-12-15 My orders: your own orders, newest first, with totals")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task MyOrdersList()
    {
        var first = (await NewOrderService().CreateAsync(Alice, OrderAt(4, (NasiLemak, 1)))).Order!;
        var second = (await NewOrderService().CreateAsync(Alice, OrderAt(4, (CharKwayTeow, 1)))).Order!;
        await NewOrderService().CreateAsync(Bob, OrderAt(5, (TehTarik, 1)));

        var mine = await NewOrderService().GetForCustomerAsync(Alice);

        Assert.Equal(new[] { second.OrderId, first.OrderId }, mine.Select(o => o.Order.OrderId));
        Assert.Equal(new[] { 13.90m, 12.90m }, mine.Select(o => o.Order.Total));
    }

    [Theory(DisplayName = "TC-12-29 Can the customer still pay? Not for a cancelled order or one already paid or refunded")]
    [Trait("Technique", "Decision table")]
    [InlineData(OrderStatus.Cancelled, null, null, false)]
    [InlineData(OrderStatus.Pending, null, null, true)]
    [InlineData(OrderStatus.Pending, PaymentStatus.Pending, PaymentMethod.Card, true)]      // paying now: can carry on
    [InlineData(OrderStatus.Pending, PaymentStatus.Failed, PaymentMethod.Card, true)]       // can try again
    [InlineData(OrderStatus.Served, PaymentStatus.Paid, PaymentMethod.Cash, false)]
    [InlineData(OrderStatus.Pending, PaymentStatus.Refunded, PaymentMethod.Cash, false)]
    public async Task CanPay(string orderStatus, string? paymentStatus, string? method, bool canPay)
    {
        var order = (await NewOrderService().CreateAsync(Alice, OrderAt(4, (TehTarik, 1)))).Order!;
        var db = NewContext();
        await db.Set<Order>().Where(o => o.OrderId == order.OrderId)
            .ExecuteUpdateAsync(o => o.SetProperty(x => x.Status, orderStatus));
        if (paymentStatus is not null)
        {
            var paidAt = paymentStatus is PaymentStatus.Paid or PaymentStatus.Refunded ? DateTime.Now : (DateTime?)null;
            db.Payments.Add(new Payment
            {
                OrderId = order.OrderId, Amount = 3.80m, PaymentMethod = method!, PaymentStatus = paymentStatus,
                CreatedAt = DateTime.Now, PaidAt = paidAt, RefundedAt = paymentStatus == PaymentStatus.Refunded ? DateTime.Now : null
            });
            await db.SaveChangesAsync();
        }

        var details = (await NewOrderService().GetByIdAsync(Alice, order.OrderId))!;
        var inMyOrders = (await NewOrderService().GetForCustomerAsync(Alice)).Single();

        Assert.Equal(canPay, details.CanPay);
        Assert.Equal(canPay, inMyOrders.CanPay);
        Assert.Equal(paymentStatus, details.Payment?.Status);
        Assert.Equal(paymentStatus, inMyOrders.Payment?.Status);
    }

    [Fact(DisplayName = "TC-12-31 Orders switched off, or a retired table: the order is refused and nothing is saved")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task OrdersSwitchedOffOrRetiredTable()
    {
        await RetireTableAsync(5);
        var atRetiredTable = await NewOrderService().CreateAsync(Alice, OrderAt(5, (TehTarik, 1)));
        await SetAcceptingAsync(reservations: true, orders: false);
        var switchedOff = await NewOrderService().CreateAsync(Alice, OrderAt(4, (TehTarik, 1)));

        Assert.Equal("Please choose your table.", atRetiredTable.ErrorMessage);
        Assert.Equal("Sorry, we're not taking orders right now. Please ask a member of staff.", switchedOff.ErrorMessage);
        Assert.Equal(0, await CountOrdersAsync());
    }
}
