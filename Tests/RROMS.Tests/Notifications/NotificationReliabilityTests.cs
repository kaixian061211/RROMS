using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
    public async Task NotificationEmailReachesSmtpWithTheOwnersAddressAndMessage()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var commands = new List<string>();
        var received = ReceiveAsync();
        var smtp = new SmtpEmailSender(Options.Create(new SmtpSettings
        {
            Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port,
            FromEmail = "sender@example.invalid", FromName = "RROMS test"
        }), NullLogger<SmtpEmailSender>.Instance);

        await new NotificationEmailSender(smtp).SendAsync(
            MailboxAddress.Parse("owner@example.invalid"), "Reservation confirmed",
            "Your reservation is confirmed. <literal text>", deadline.Token);
        using var message = await received;
        Assert.Equal("owner@example.invalid", Assert.Single(message.To.Mailboxes).Address);
        Assert.Equal("sender@example.invalid", Assert.Single(message.From.Mailboxes).Address);
        Assert.Equal("Reservation confirmed", message.Subject);
        Assert.Contains("Your reservation is confirmed. <literal text>", message.TextBody);
        Assert.Contains(commands, command => command.StartsWith("RCPT TO:<owner@example.invalid>", StringComparison.OrdinalIgnoreCase));

        async Task<MimeMessage> ReceiveAsync()
        {
            using var client = await listener.AcceptTcpClientAsync(deadline.Token);
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
                { AutoFlush = true, NewLine = "\r\n" };
            await writer.WriteLineAsync("220 localhost SMTP test");
            var body = new StringBuilder();
            while (await reader.ReadLineAsync(deadline.Token) is { } command)
            {
                commands.Add(command);
                if (command.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase))
                    await writer.WriteLineAsync("250-localhost\r\n250 8BITMIME");
                else if (command == "DATA")
                {
                    await writer.WriteLineAsync("354 Send message");
                    while (await reader.ReadLineAsync(deadline.Token) is { } line && line != ".")
                        body.AppendLine(line.StartsWith("..", StringComparison.Ordinal) ? line[1..] : line);
                    await writer.WriteLineAsync("250 Accepted");
                }
                else if (command == "QUIT")
                {
                    await writer.WriteLineAsync("221 Bye");
                    break;
                }
                else await writer.WriteLineAsync("250 OK");
            }
            using var data = new MemoryStream(Encoding.UTF8.GetBytes(body.ToString()));
            return await MimeMessage.LoadAsync(data, deadline.Token);
        }
    }

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
