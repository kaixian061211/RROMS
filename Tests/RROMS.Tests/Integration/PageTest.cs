using System.Net;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Microsoft.Extensions.DependencyInjection;
using RROMS.Web.Data;

namespace RROMS.Tests.Integration;

/// <summary>
/// Base class for page tests. Requests go through the real app the way a browser sends them, and what they
/// save really is saved: each request is separate, so they can't share one rolled-back transaction. Instead
/// the tables are emptied before and after every test, so every test, and the unit tests, start from empty
/// tables.
/// </summary>
public abstract class PageTest : IClassFixture<TestWebApp>, IAsyncLifetime
{
    /// <summary>Program.cs signs every Development visitor in as customer 1 (and as staff).</summary>
    protected const int TestCustomer = 1;
    protected const int OtherCustomer = 9001;

    protected static DateOnly Tomorrow => DateOnly.FromDateTime(DateTime.Today.AddDays(1));

    private readonly TestWebApp _app;

    protected PageTest(TestWebApp app)
    {
        _app = app;
        // Like a browser, the client keeps cookies (the anti-forgery cookie and the one-off messages) and
        // follows redirects.
        Client = app.CreateClient();
    }

    protected HttpClient Client { get; }

    public Task InitializeAsync() => WithDatabaseAsync(TestDatabase.EmptyAsync);

    public Task DisposeAsync() => WithDatabaseAsync(TestDatabase.EmptyAsync);

    /// <summary>
    /// Runs some work with the app's own DbContext, to set up or check rows directly. Emptying the tables goes
    /// through here too, so the guard checks the database the app itself is using.
    /// </summary>
    protected async Task<T> WithDatabaseAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = _app.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected Task WithDatabaseAsync(Func<AppDbContext, Task> work) =>
        WithDatabaseAsync(async db => { await work(db); return true; });

    /// <summary>Opens a page and reads it the way a browser would. The test fails unless it loads (200 OK).</summary>
    protected async Task<IHtmlDocument> GetPageAsync(string url)
    {
        var response = await Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadPageAsync(response);
    }

    /// <summary>
    /// Sends a form the way a browser would: every field on it, including the hidden anti-forgery token and
    /// RowVersion, with the test's changes on top. A change can add a field the form doesn't have, as someone
    /// tampering with the request could, and null leaves a field out.
    /// </summary>
    protected async Task<HttpResponseMessage> SendFormAsync(IHtmlFormElement form, Dictionary<string, string?>? changes = null)
    {
        var fields = form.Elements
            .Where(e => e is IHtmlInputElement or IHtmlSelectElement or IHtmlTextAreaElement)
            .Where(e => !string.IsNullOrEmpty(e.GetAttribute("name")))
            .Select(e => KeyValuePair.Create(e.GetAttribute("name")!, ValueOf(e)))
            .ToList();

        foreach (var (name, value) in changes ?? new())
        {
            fields.RemoveAll(f => f.Key == name);
            if (value is not null)
                fields.Add(KeyValuePair.Create(name, value));
        }

        return await Client.PostAsync(form.Action, new FormUrlEncodedContent(fields));
    }

    /// <summary>Sends a form and reads the page the app ends up on, after any redirect.</summary>
    protected async Task<IHtmlDocument> SubmitAsync(IHtmlFormElement form, Dictionary<string, string?>? changes = null)
    {
        var response = await SendFormAsync(form, changes);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadPageAsync(response);
    }

    /// <summary>The page's own form, the one that posts, as opposed to a filter form.</summary>
    protected static IHtmlFormElement MainForm(IHtmlDocument page) =>
        page.Forms.Single(f => f.Method.Equals("post", StringComparison.OrdinalIgnoreCase));

    /// <summary>The form behind one status button, such as Confirm on reservation 12. Null if it isn't offered.</summary>
    protected static IHtmlFormElement? StatusButton(IHtmlDocument page, int id, string buttonText) =>
        page.Forms.SingleOrDefault(f =>
            f.QuerySelector<IHtmlInputElement>("input[name=id]")?.Value == id.ToString()
            && TextOf(f.QuerySelector("button")!) == buttonText);

    /// <summary>
    /// The name of a dish's quantity field on the order page. Each dish on the form is a hidden
    /// Input.Items[i].MenuItemId with a matching Input.Items[i].Quantity.
    /// </summary>
    protected static string QuantityField(IHtmlDocument orderPage, int menuItemId) =>
        orderPage.QuerySelectorAll<IHtmlInputElement>("input[name$='.MenuItemId']")
            .Single(f => f.Value == menuItemId.ToString())
            .Name!.Replace(".MenuItemId", ".Quantity");

    /// <summary>An element's text as a reader sees it, with the spacing tidied up.</summary>
    protected static string TextOf(IElement element) => Regex.Replace(element.TextContent, @"\s+", " ").Trim();

    protected static string TextOf(IHtmlDocument page) => TextOf(page.Body!);

    private static async Task<IHtmlDocument> ReadPageAsync(HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync();
        // The address is the page the request ended up on, so a form with no action posts back to it, as in a browser.
        var address = response.RequestMessage!.RequestUri!.ToString();
        return (IHtmlDocument)await BrowsingContext.New().OpenAsync(page => page.Content(html).Address(address));
    }

    private static string ValueOf(IElement field) => field switch
    {
        IHtmlInputElement input => input.Value,
        IHtmlSelectElement select => select.Value ?? string.Empty,
        IHtmlTextAreaElement textArea => textArea.Value,
        _ => string.Empty
    };
}
