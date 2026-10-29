using Microsoft.EntityFrameworkCore;
using RROMS.Web.Models.Accounts;
using RROMS.Web.Models.OrdersAndReservations;
using RROMS.Web.Services.Payments;

namespace RROMS.Tests.Payments;

public class PaymentCustomerEmailTests : DatabaseTest
{
    [Fact]
    public async Task AllOrderLookupsUseTheOwnersCurrentAccountEmail()
    {
        await using var db = NewContext();
        var customer = await db.Set<UserAccount>().SingleAsync(u => u.UserId == Alice);
        customer.FullName = "Receipt Customer";
        customer.Email = "receipt-owner@example.invalid";
        var order = new Order { CustomerUserId = Alice, TableId = 4 };
        db.Add(order);
        await db.SaveChangesAsync();

        var provider = new PaymentOrderProvider(db);
        var single = await provider.GetOrderAsync(order.OrderId);
        var customerOrders = await provider.GetOrdersForCustomerAsync(Alice);
        var batch = await provider.GetOrdersAsync(new[] { order.OrderId, order.OrderId });
        Assert.Equal(customer.Email, single!.CustomerEmail);
        Assert.Equal(customer.FullName, single.CustomerName);
        Assert.Equal(customer.Email, Assert.Single(customerOrders).CustomerEmail);
        Assert.Equal(customer.Email, Assert.Single(batch).Value.CustomerEmail);

        customer.Email = "updated-owner@example.invalid";
        await db.SaveChangesAsync();
        Assert.Equal(customer.Email, (await provider.GetOrderAsync(order.OrderId))!.CustomerEmail);
    }

    [Theory]
    [InlineData(false, UserRoles.Customer)]
    [InlineData(true, UserRoles.Staff)]
    public async Task InactiveOrNoncustomerAccountsDoNotReceiveReceiptEmails(bool active, string role)
    {
        await using var db = NewContext();
        var customer = await db.Set<UserAccount>().SingleAsync(u => u.UserId == Alice);
        customer.IsActive = active;
        customer.Role = role;
        var order = new Order { CustomerUserId = Alice, TableId = 4 };
        db.Add(order);
        await db.SaveChangesAsync();

        var result = await new PaymentOrderProvider(db).GetOrderAsync(order.OrderId);
        Assert.NotNull(result);
        Assert.Empty(result.CustomerEmail);
    }
}
