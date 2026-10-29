using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using RROMS.Web.Data;
using RROMS.Web.Pages.Tracking;
using RROMS.Web.Services.Notifications;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Tests.Notifications;

[Trait("Type", "Unit")]
public class NotificationReliabilityTests
{
    [Fact]
    public async Task SmtpStopsWhenTheServerAcceptsButNeverResponds()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var testDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var connection = listener.AcceptTcpClientAsync(testDeadline.Token).AsTask();
        var sender = new SmtpEmailSender(Options.Create(new SmtpSettings
        {
            Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port,
            TimeoutSeconds = 1, FromEmail = "sender@example.com"
        }), NullLogger<SmtpEmailSender>.Instance);
        var message = new MimeMessage();
        message.To.Add(MailboxAddress.Parse("customer@example.com"));
        message.Body = new TextPart("plain") { Text = "Timeout test" };
        var elapsed = Stopwatch.StartNew();
        var sending = sender.SendAsync(message, testDeadline.Token);
        using var client = await connection;
        // Deliberately never send the SMTP greeting. The production deadline,
        // rather than the outer test deadline, must stop the operation.
        var error = await Record.ExceptionAsync(() => sending);
        Assert.NotNull(error);
        Assert.False(testDeadline.IsCancellationRequested);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(1, 2, null)]
    [InlineData(0, null, null)]
    [InlineData(null, -1, null)]
    [InlineData(null, null, 0)]
    public async Task StatusHandlerRejectsInvalidSelectorsBeforeDatabaseAccess(
        int? orderId, int? reservationId, int? paymentId)
    {
        await using var db = new AppDbContext(new DbContextOptions<AppDbContext>());
        var page = Page(db, authenticated: true);
        Assert.IsType<BadRequestResult>(await page.OnGetStatusAsync(orderId, reservationId, paymentId, default));
    }

    [Fact]
    public async Task StatusHandlerRejectsInvalidLoginBeforeDatabaseAccess()
    {
        await using var db = new AppDbContext(new DbContextOptions<AppDbContext>());
        Assert.IsType<ForbidResult>(await Page(db, false).OnGetStatusAsync(1, null, null, default));
    }

    private static IndexModel Page(AppDbContext db, bool authenticated)
    {
        var page = new IndexModel(db, new RestaurantLookup(db));
        var context = new DefaultHttpContext();
        if (authenticated)
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "9001"), new Claim(ClaimTypes.Role, "Customer")], "test"));
        page.PageContext = new PageContext { HttpContext = context };
        return page;
    }
}
