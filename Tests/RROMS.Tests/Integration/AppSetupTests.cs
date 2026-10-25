using System.Net;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Models.Dev;

namespace RROMS.Tests.Integration;

[Trait("Type", "Integration")]
public class AppSetupTests : PageTest
{
    public AppSetupTests(TestWebApp app) : base(app) { }

    [Fact(DisplayName = "TC-00-03 The app under test is using the RROMS_Tests database")]
    [Trait("Technique", "Security")]
    public async Task AppUsesTheTestDatabase()
    {
        var database = await WithDatabaseAsync(async db =>
        {
            await db.Database.OpenConnectionAsync();
            return db.Database.GetDbConnection().Database;
        });

        Assert.Equal(TestDatabase.Name, database);
    }
}

/// <summary>Outside Development nobody can sign in until the login module exists, so every page refuses.</summary>
[Trait("Type", "Integration")]
public class OutsideDevelopmentTests : IClassFixture<ProductionWebApp>
{
    private readonly HttpClient _client;

    public OutsideDevelopmentTests(ProductionWebApp app) => _client = app.CreateClient();

    [Theory(DisplayName = "TC-00-04 Outside Development, every Orders & Reservations page refuses a visitor who isn't signed in (401)")]
    [Trait("Technique", "Security")]
    [Trait("Story", "5")]
    [Trait("Story", "11")]
    [Trait("Story", "12")]
    [Trait("Story", "17")]
    [InlineData("/Reservations")]
    [InlineData("/Reservations/Create")]
    [InlineData("/Reservations/Confirmation/1")]
    [InlineData("/Reservations/Edit/1")]
    [InlineData("/Reservations/Cancel/1")]
    [InlineData("/Reservations/Manage")]
    [InlineData("/Reservations/Manage/Edit/1")]
    [InlineData("/Orders")]
    [InlineData("/Orders/Create")]
    [InlineData("/Orders/Details/1")]
    [InlineData("/Orders/Queue")]
    public async Task PagesRefuseAVisitor(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "TC-00-05 Outside Development the test login doesn't exist: /Dev/SwitchUser answers like a made-up address, signs nobody in, and no page shows the test-login bar")]
    [Trait("Technique", "Security")]
    public async Task NoTestLoginOutsideDevelopment()
    {
        var signInAsAdmin = new Dictionary<string, string> { ["id"] = TestUsers.Admin.Id.ToString() };

        var get = await _client.GetAsync("/Dev/SwitchUser");
        var post = await _client.PostAsync("/Dev/SwitchUser?handler=SignIn", new FormUrlEncodedContent(signInAsAdmin));
        var madeUpPost = await _client.PostAsync("/Dev/NoSuchPage?handler=SignIn", new FormUrlEncodedContent(signInAsAdmin));
        var afterPost = await _client.GetAsync("/Reservations/Manage");
        var home = await _client.GetStringAsync("/");

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        // The app answers a POST to any address it doesn't have with 405 (only GET is allowed there), so the same here.
        Assert.Equal(madeUpPost.StatusCode, post.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterPost.StatusCode);   // the POST signed nobody in
        Assert.DoesNotContain("test-user-bar", home);
    }
}
