using Microsoft.Extensions.Configuration;
using RROMS.Web.Services.Notifications;
using RROMS.Web.Services.Payments.Gateways;

namespace RROMS.Tests;

[Trait("Type", "Unit")]
public class EnvironmentConfigurationTests
{
    [Fact]
    public void EnvironmentVariablesBindSecretsAndOverrideFileSettings()
    {
        // A unique prefix keeps this test away from developers' real credentials.
        var prefix = "RROMS_CONFIG_TEST_" + Guid.NewGuid().ToString("N") + "_";
        var settings = new Dictionary<string, string?>
        {
            ["Stripe__SecretKey"] = "test-stripe-key",
            ["Stripe__WebhookSecret"] = "test-webhook-secret",
            ["Smtp__Host"] = "smtp.example.invalid",
            ["Smtp__UserName"] = "sender@example.invalid",
            ["Smtp__Password"] = "test-smtp-password",
            ["Smtp__FromEmail"] = "sender@example.invalid",
            ["Notifications__WebPush__AppId"] = "test-app-id",
            ["Notifications__WebPush__ApiKey"] = "test-push-key",
            ["Notifications__WebPush__AliasSecret"] = new string('x', 32),
            ["Notifications__WebPush__PublicBaseUrl"] = "https://example.invalid",
            ["ConnectionStrings__DefaultConnection"] = "Server=test;Database=test",
            ["DefaultAdmin__Email"] = "admin@example.invalid",
            ["DefaultAdmin__Password"] = "test-bootstrap-password"
        };

        try
        {
            foreach (var setting in settings)
                Environment.SetEnvironmentVariable(prefix + setting.Key, setting.Value);

            using var config = (ConfigurationRoot)new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Stripe:SecretKey"] = "older-file-value",
                    ["Smtp:Password"] = "older-file-value"
                })
                .AddEnvironmentVariables(prefix)
                .Build();

            var stripe = config.GetSection(StripeSettings.SectionName).Get<StripeSettings>()!;
            var smtp = config.GetSection(SmtpSettings.SectionName).Get<SmtpSettings>()!;
            var push = config.GetSection(WebPushSettings.SectionName).Get<WebPushSettings>()!;
            Assert.Equal(settings["Stripe__SecretKey"], stripe.SecretKey);
            Assert.Equal(settings["Stripe__WebhookSecret"], stripe.WebhookSecret);
            Assert.Equal(settings["Smtp__Password"], smtp.Password);
            Assert.Equal(settings["Smtp__UserName"], smtp.UserName);
            Assert.Equal(settings["Smtp__FromEmail"], smtp.FromEmail);
            Assert.True(smtp.Enabled);
            Assert.Equal(settings["Notifications__WebPush__ApiKey"], push.ApiKey);
            Assert.Equal(settings["Notifications__WebPush__AliasSecret"], push.AliasSecret);
            Assert.Equal(settings["Notifications__WebPush__AppId"], push.AppId);
            Assert.Equal(settings["Notifications__WebPush__PublicBaseUrl"], push.PublicBaseUrl);
            Assert.Equal(settings["ConnectionStrings__DefaultConnection"], config.GetConnectionString("DefaultConnection"));
            Assert.Equal(settings["DefaultAdmin__Email"], config["DefaultAdmin:Email"]);
            Assert.Equal(settings["DefaultAdmin__Password"], config["DefaultAdmin:Password"]);
        }
        finally
        {
            foreach (var setting in settings)
                Environment.SetEnvironmentVariable(prefix + setting.Key, null);
        }
    }
}
