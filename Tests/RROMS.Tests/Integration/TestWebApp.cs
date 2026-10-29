using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RROMS.Web.Models.Notifications;
using RROMS.Web.Services.Notifications;

namespace RROMS.Tests.Integration;

/// <summary>
/// The real app (the same Program.cs, pages and services) started in memory for the page tests, but using
/// the RROMS_Tests database. Requests go straight to it, without a network port or a browser.
/// </summary>
public class TestWebApp : WebApplicationFactory<Program>
{
    protected virtual string EnvironmentName => Environments.Development;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);

        // TestDatabase.ConnectionString throws unless it names RROMS_Tests, so the app can't start against
        // any other database.
        builder.UseSetting("ConnectionStrings:DefaultConnection", TestDatabase.ConnectionString);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<INotificationDelivery>();
            services.AddSingleton<INotificationDelivery, TestNotificationDelivery>();
        });
    }

    private sealed class TestNotificationDelivery : INotificationDelivery
    {
        public Task DeliverAsync(
            int notificationId,
            bool sendEmail = false,
            NotificationEmailRecipient? recipient = null,
            CancellationToken ct = default) => Task.CompletedTask;
    }
}

/// <summary>The app as it runs anywhere but Development: no test login, so nobody can sign in.</summary>
public class ProductionWebApp : TestWebApp
{
    protected override string EnvironmentName => Environments.Production;
}
