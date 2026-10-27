using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Models.Dev;
using RROMS.Web.Models.OrdersAndReservations;

namespace RROMS.Tests.Integration;

/// <summary>Story 11 through the staff page /Reservations/Manage.</summary>
[Trait("Type", "Integration")]
[Trait("Story", "11")]
public class StaffReservationPageTests : PageTest
{
    public StaffReservationPageTests(TestWebApp app) : base(app) { }

    protected override TestUser? SignedInAs => TestUsers.Staff;

    private string TomorrowsList => $"/Reservations/Manage?date={Tomorrow:yyyy-MM-dd}";

    /// <summary>
    /// A booking made the way a customer makes one: Customer A signs in and uses the booking form, then
    /// staff sign back in.
    /// </summary>
    private async Task<int> CustomerBooksAsync(int table, DateOnly? date = null)
    {
        await SignInAsAsync(TestUsers.CustomerA);
        var form = MainForm(await GetPageAsync("/Reservations/Create"));
        await SubmitAsync(form, new()
        {
            ["Input.ReservationDate"] = (date ?? Tomorrow).ToString("yyyy-MM-dd"),
            ["Input.TimeSlot"] = "19:30",
            ["Input.TableId"] = table.ToString(),
            ["Input.PartySize"] = "2"
        });
        await SignInAsAsync(TestUsers.Staff);
        return await WithDatabaseAsync(db => db.Set<Reservation>().Where(r => r.TableId == table).Select(r => r.ReservationId).SingleAsync());
    }

    private Task<string> SavedStatusAsync(int id) =>
        WithDatabaseAsync(db => db.Set<Reservation>().Where(r => r.ReservationId == id).Select(r => r.Status).SingleAsync());

    private static IEnumerable<string> ButtonsFor(IHtmlDocument page, int id) =>
        page.Forms.Where(f => f.QuerySelector<IHtmlInputElement>("input[name=id]")?.Value == id.ToString())
            .Select(f => TextOf(f.QuerySelector("button")!));

    [Fact(DisplayName = "TC-11-11 Staff confirm a booking from the day's list: shown as Confirmed to staff and to the customer")]
    [Trait("Technique", "State transition")]
    public async Task ConfirmFromTheList()
    {
        var id = await CustomerBooksAsync(table: 3);
        var list = await GetPageAsync(TomorrowsList);
        Assert.Contains("Customer #1", TextOf(list));
        Assert.Equal(new[] { "Confirm", "Cancel" }, ButtonsFor(list, id));

        var afterConfirm = await SubmitAsync(StatusButton(list, id, "Confirm")!);

        Assert.Contains(TomorrowsList, afterConfirm.Url);
        Assert.Contains($"{ReservationFormatter.Reference(id)} is now confirmed.", TextOf(afterConfirm));
        Assert.Equal(new[] { "Cancel" }, ButtonsFor(afterConfirm, id));
        Assert.Equal(ReservationStatus.Confirmed, await SavedStatusAsync(id));
        await SignInAsAsync(TestUsers.CustomerA);
        Assert.Contains("Confirmed", TextOf(await GetPageAsync("/Reservations")));
    }

    [Fact(DisplayName = "TC-11-12 A status button pressed on a page loaded before someone else's change: 'changed by someone else'")]
    [Trait("Technique", "Concurrency")]
    public async Task ButtonOnAnOutOfDatePage()
    {
        var id = await CustomerBooksAsync(table: 3);
        var firstStaff = await GetPageAsync(TomorrowsList);
        var secondStaff = await GetPageAsync(TomorrowsList);

        await SubmitAsync(StatusButton(firstStaff, id, "Confirm")!);
        var page = await SubmitAsync(StatusButton(secondStaff, id, "Cancel")!);

        Assert.Contains("changed by someone else", TextOf(page));
        Assert.Equal(ReservationStatus.Confirmed, await SavedStatusAsync(id));
    }

    [Fact(DisplayName = "TC-11-14 Staff edit a confirmed booking through the page: back on that day's list, still Confirmed; an empty date is refused")]
    [Trait("Technique", "Decision table")]
    public async Task StaffEditPage()
    {
        var id = await CustomerBooksAsync(table: 3);
        await SubmitAsync(StatusButton(await GetPageAsync(TomorrowsList), id, "Confirm")!);

        var editPage = await GetPageAsync($"/Reservations/Manage/Edit/{id}");
        var withNoDate = await SubmitAsync(MainForm(editPage), new() { ["Input.ReservationDate"] = "" });
        var saved = await SubmitAsync(MainForm(editPage), new() { ["Input.PartySize"] = "6" });

        Assert.Contains("Please choose a date.", TextOf(withNoDate));
        Assert.Equal("/Reservations/Manage", withNoDate.QuerySelectorAll("a").Single(a => TextOf(a) == "Back to reservations").GetAttribute("href"));
        Assert.Contains(TomorrowsList, saved.Url);
        Assert.Contains($"{ReservationFormatter.Reference(id)} has been updated.", TextOf(saved));
        Assert.Contains("6 people", TextOf(saved));
        Assert.Equal(ReservationStatus.Confirmed, await SavedStatusAsync(id));
    }

    private const string Upcoming = "/Reservations/Manage/Upcoming";

    [Fact(DisplayName = "TC-11-27 Staff confirm next week's booking from Upcoming: the tab counts it, and after Confirm it leaves the Pending list")]
    [Trait("Technique", "Use case")]
    public async Task ConfirmFromUpcoming()
    {
        var id = await CustomerBooksAsync(table: 3, date: Tomorrow.AddDays(6));
        var dayView = await GetPageAsync("/Reservations/Manage");
        Assert.Contains("Upcoming 1 pending", TextOf(dayView.QuerySelector("nav[aria-label=View]")!));

        var upcoming = await GetPageAsync(Upcoming);
        Assert.Contains(ReservationFormatter.LongDate(Tomorrow.AddDays(6)), TextOf(upcoming));
        var afterConfirm = await SubmitAsync(StatusButton(upcoming, id, "Confirm")!);

        Assert.Contains(Upcoming, afterConfirm.Url);
        Assert.Contains($"{ReservationFormatter.Reference(id)} is now confirmed.", TextOf(afterConfirm));
        Assert.Empty(ButtonsFor(afterConfirm, id));
        Assert.Contains("Nothing waiting to be confirmed.", TextOf(afterConfirm));
        Assert.Contains("Upcoming 0 pending", TextOf(afterConfirm.QuerySelector("nav[aria-label=View]")!));
        Assert.Equal(new[] { "Cancel" }, ButtonsFor(await GetPageAsync($"{Upcoming}?status=all"), id));
        Assert.Equal(ReservationStatus.Confirmed, await SavedStatusAsync(id));
    }

    [Fact(DisplayName = "TC-11-28 Staff edit a booking from Upcoming: Back and Save both return to Upcoming")]
    [Trait("Technique", "Use case")]
    public async Task EditFromUpcoming()
    {
        var id = await CustomerBooksAsync(table: 3);
        var edit = (await GetPageAsync(Upcoming)).QuerySelectorAll<IHtmlAnchorElement>("a")
            .Single(a => TextOf(a) == "Edit" && a.PathName.EndsWith($"/Edit/{id}"));

        var editPage = await GetPageAsync(edit.Href);
        var saved = await SubmitAsync(MainForm(editPage), new() { ["Input.PartySize"] = "6" });

        Assert.Equal(Upcoming, editPage.QuerySelectorAll("a").Single(a => TextOf(a) == "Back to reservations").GetAttribute("href"));
        Assert.Contains(Upcoming, saved.Url);
        Assert.Contains($"{ReservationFormatter.Reference(id)} has been updated.", TextOf(saved));
        Assert.Contains("6 people", TextOf(saved));
    }
}
