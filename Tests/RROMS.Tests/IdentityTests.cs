using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.OrdersAndReservations;

namespace RROMS.Tests;

/// <summary>
/// Who my pages treat as the customer, and who as staff. Program.cs signs every Development visitor in, so the
/// pages never reach the "Development without login" case; these tests call the two helpers directly.
/// </summary>
[Trait("Type", "Unit")]
public class IdentityTests
{
    [Theory(DisplayName = "TC-05-30 Who the customer is: the signed-in user, test customer 1 in Development without login, otherwise nobody")]
    [Trait("Story", "5")]
    [Trait("Story", "12")]
    [Trait("Technique", "Decision table")]
    [InlineData(7, "Production", true, 7)]
    [InlineData(7, "Development", true, 7)]
    [InlineData(null, "Development", true, 1)]
    [InlineData(null, "Production", false, 0)]
    public void WhoTheCustomerIs(int? signedInAs, string environment, bool expectedFound, int expectedCustomerId)
    {
        var user = signedInAs is null
            ? NotSignedIn()
            : SignedIn(new Claim(ClaimTypes.NameIdentifier, signedInAs.Value.ToString()), new Claim(ClaimTypes.Role, PaymentRoles.Customer));

        var found = user.TryGetCustomerId(new TestEnvironment(environment), out var customerId);

        Assert.Equal((expectedFound, expectedCustomerId), (found, customerId));
    }

    [Theory(DisplayName = "TC-11-13 Who counts as staff: Staff and Admin users, everyone in Development without login, otherwise nobody")]
    [Trait("Story", "11")]
    [Trait("Story", "17")]
    [Trait("Technique", "Decision table")]
    [InlineData(PaymentRoles.Staff, "Production", true)]
    [InlineData(PaymentRoles.Admin, "Production", true)]
    [InlineData(PaymentRoles.Customer, "Production", false)]
    [InlineData(null, "Development", true)]
    [InlineData(null, "Production", false)]
    public void WhoCountsAsStaff(string? role, string environment, bool expected)
    {
        var user = role is null
            ? NotSignedIn()
            : SignedIn(new Claim(ClaimTypes.NameIdentifier, "7"), new Claim(ClaimTypes.Role, role));

        Assert.Equal(expected, user.IsStaff(new TestEnvironment(environment)));
    }

    private static ClaimsPrincipal NotSignedIn() => new(new ClaimsIdentity());

    private static ClaimsPrincipal SignedIn(params Claim[] claims) => new(new ClaimsIdentity(claims, "Test"));

    /// <summary>Just enough of the app's environment for IsDevelopment() to work.</summary>
    private class TestEnvironment : IWebHostEnvironment
    {
        public TestEnvironment(string name) => EnvironmentName = name;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "RROMS.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
