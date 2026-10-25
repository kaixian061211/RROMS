using System.Net;
using Microsoft.EntityFrameworkCore;

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

/// <summary>Outside Development nobody is signed in until the login module exists, so every page refuses.</summary>
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
}
