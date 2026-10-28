using System.Net;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Models.OrdersAndReservations;

namespace RROMS.Tests.Integration;

/// <summary>Story 5 through the customer pages: /Reservations, Create, Confirmation, Edit and Cancel.</summary>
[Trait("Type", "Integration")]
[Trait("Story", "5")]
public class ReservationPageTests : PageTest
{
    public ReservationPageTests(TestWebApp app) : base(app) { }

    /// <summary>What a customer types into the booking form. Dinner tomorrow for two unless the test says otherwise.</summary>
    private static Dictionary<string, string?> Booking(int table, string time = "19:30", int partySize = 2, string request = "") => new()
    {
        ["Input.ReservationDate"] = Tomorrow.ToString("yyyy-MM-dd"),
        ["Input.TimeSlot"] = time,
        ["Input.TableId"] = table.ToString(),
        ["Input.PartySize"] = partySize.ToString(),
        ["Input.SpecialRequest"] = request
    };

    private async Task<IHtmlDocument> BookAsync(Dictionary<string, string?> booking) =>
        await SubmitAsync(MainForm(await GetPageAsync("/Reservations/Create")), booking);

    private Task<List<Reservation>> SavedReservationsAsync() =>
        WithDatabaseAsync(db => db.Set<Reservation>().AsNoTracking().OrderBy(r => r.ReservationId).ToListAsync());

    [Fact(DisplayName = "TC-05-23 Book through the form: the confirmation page shows the booking as Pending, and My reservations lists it")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task BookThroughTheForm()
    {
        var confirmation = await BookAsync(Booking(table: 3, request: "Window seat please"));

        var saved = Assert.Single(await SavedReservationsAsync());
        var reference = ReservationFormatter.Reference(saved.ReservationId);
        Assert.EndsWith($"/Reservations/Confirmation/{saved.ReservationId}", confirmation.Url);
        var shown = TextOf(confirmation);
        Assert.Contains($"Your reference is {reference}", shown);
        Assert.Contains(ReservationFormatter.When(Tomorrow, new TimeOnly(19, 30)), shown);
        Assert.Contains("Table 3", shown);
        Assert.Contains("2 people", shown);
        Assert.Contains("Window seat please", shown);
        Assert.Contains("Pending", shown);
        Assert.Equal((TestCustomer, ReservationStatus.Pending), (saved.CustomerUserId, saved.Status));

        Assert.Contains(reference, TextOf(await GetPageAsync("/Reservations")));
    }

    [Fact(DisplayName = "TC-05-24 Book a table and time that's already taken: stays on the form with 'already booked', nothing saved")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task BookATakenSlot()
    {
        await BookAsync(Booking(table: 3));

        var page = await BookAsync(Booking(table: 3));

        Assert.EndsWith("/Reservations/Create", page.Url);
        Assert.Contains("Sorry, that table is already booked for this time slot.", TextOf(page));
        Assert.Single(await SavedReservationsAsync());
    }

    [Theory(DisplayName = "TC-05-25 Form input that fails validation: the message is shown and nothing is saved")]
    [Trait("Technique", "Boundary value analysis")]
    [InlineData("Input.PartySize", "0", "Party size must be at least 1.")]
    [InlineData("Input.ReservationDate", "", "Please choose a date.")]
    [InlineData("Input.PartySize", "", "Please enter the party size.")]
    public async Task InputThatFailsValidation(string field, string value, string expectedMessage)
    {
        var booking = Booking(table: 3);
        booking[field] = value;

        var page = await BookAsync(booking);

        Assert.EndsWith("/Reservations/Create", page.Url);
        Assert.Contains(expectedMessage, TextOf(page));
        Assert.Empty(await SavedReservationsAsync());
    }

    [Fact(DisplayName = "TC-05-26 Change a booking, then cancel it, through the pages: the slot can be booked again")]
    [Trait("Technique", "State transition")]
    public async Task ChangeThenCancel()
    {
        await BookAsync(Booking(table: 3));
        var id = (await SavedReservationsAsync()).Single().ReservationId;

        var editPage = await GetPageAsync($"/Reservations/Edit/{id}");
        Assert.Equal("2", editPage.QuerySelector<IHtmlInputElement>("input[name='Input.PartySize']")!.Value);
        var afterChange = await SubmitAsync(MainForm(editPage), new() { ["Input.PartySize"] = "4" });

        Assert.EndsWith("/Reservations", afterChange.Url);
        Assert.Contains("Your reservation has been updated.", TextOf(afterChange));
        Assert.Contains("4 people", TextOf(afterChange));

        var afterCancel = await SubmitAsync(MainForm(await GetPageAsync($"/Reservations/Cancel/{id}")));

        Assert.Contains("Your reservation has been cancelled.", TextOf(afterCancel));
        Assert.DoesNotContain(ReservationFormatter.Reference(id), TextOf(afterCancel));

        var rebooked = await BookAsync(Booking(table: 3));

        Assert.Contains("/Reservations/Confirmation/", rebooked.Url);
        Assert.Equal(new[] { ReservationStatus.Cancelled, ReservationStatus.Pending }, (await SavedReservationsAsync()).Select(r => r.Status));
    }

    [Fact(DisplayName = "TC-05-27 The same booking edited in two tabs: the second save gets 'changed by someone else'")]
    [Trait("Technique", "Concurrency")]
    public async Task EditedInTwoTabs()
    {
        await BookAsync(Booking(table: 3));
        var id = (await SavedReservationsAsync()).Single().ReservationId;
        var firstTab = await GetPageAsync($"/Reservations/Edit/{id}");
        var secondTab = await GetPageAsync($"/Reservations/Edit/{id}");

        await SubmitAsync(MainForm(firstTab), new() { ["Input.PartySize"] = "4" });
        var page = await SubmitAsync(MainForm(secondTab), new() { ["Input.PartySize"] = "6" });

        Assert.Contains("This reservation was changed by someone else while you were editing it.", TextOf(page));
        Assert.Equal(4, (await SavedReservationsAsync()).Single().PartySize);
    }

    [Fact(DisplayName = "TC-05-28 A booking posted without the anti-forgery token: refused (400), nothing saved")]
    [Trait("Technique", "Security")]
    public async Task PostWithoutTheAntiForgeryToken()
    {
        var booking = Booking(table: 3);
        booking["__RequestVerificationToken"] = null;

        var response = await SendFormAsync(MainForm(await GetPageAsync("/Reservations/Create")), booking);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await SavedReservationsAsync());
    }

    [Fact(DisplayName = "TC-05-29 HTML typed into the special request is shown as text, not run")]
    [Trait("Technique", "Security")]
    public async Task HtmlInTheSpecialRequest()
    {
        const string attack = "<script>alert('hacked')</script>";

        var confirmation = await BookAsync(Booking(table: 3, request: attack));

        Assert.Contains(attack, TextOf(confirmation));
        Assert.DoesNotContain(confirmation.Scripts, script => script.Text.Contains("hacked"));
    }

    [Fact(DisplayName = "TC-05-46 The booking form offers the restaurant's tables by dining area with their seats, and a party too big for the table is refused")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task TablesOnTheForm()
    {
        var form = await GetPageAsync("/Reservations/Create");
        var tableTiles = form.QuerySelectorAll<IHtmlInputElement>("input[name='Input.TableId']").ToList();

        Assert.Equal(Enumerable.Range(1, 10).Select(n => n.ToString()), tableTiles.Select(t => t.Value));
        Assert.Contains("Main hall", TextOf(form));
        Assert.Equal("10 seats 2", TextOf(form.QuerySelector("label[for='Input_TableId_10']")!));

        var page = await SubmitAsync(MainForm(form), Booking(table: 10, partySize: 3));

        Assert.Contains("Table 10 seats 2 people. Please choose a bigger table.", TextOf(page));
        Assert.Equal(0, await WithDatabaseAsync(db => db.Set<Reservation>().CountAsync()));
    }
}
