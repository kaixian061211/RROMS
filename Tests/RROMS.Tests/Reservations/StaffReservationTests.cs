using RROMS.Web.Models.OrdersAndReservations;

namespace RROMS.Tests.Reservations;

/// <summary>Story 11: staff view, confirm, modify or cancel daily reservations.</summary>
[Trait("Type", "Unit")]
[Trait("Story", "11")]
public class StaffReservationTests : DatabaseTest
{
    private static ReservationInput Booking(int table, DateOnly date, TimeOnly time, int partySize = 2) =>
        new() { TableId = table, ReservationDate = date, TimeSlot = time, PartySize = partySize };

    [Fact(DisplayName = "TC-11-01 The day's list: in time order then table, and the status filter works")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task DayList()
    {
        var dinnerTable2 = await AddReservationAsync(Alice, 2, Tomorrow, Dinner, ReservationStatus.Confirmed);
        var lunchTable5 = await AddReservationAsync(Bob, 5, Tomorrow, Lunch);
        var dinnerTable1 = await AddReservationAsync(Bob, 1, Tomorrow, Dinner);
        await AddReservationAsync(Alice, 1, Tomorrow.AddDays(1), Lunch);   // another day

        var all = await NewReservationService().GetForDateAsync(Tomorrow, null);
        var confirmed = await NewReservationService().GetForDateAsync(Tomorrow, ReservationStatus.Confirmed);

        Assert.Equal(new[] { lunchTable5.ReservationId, dinnerTable1.ReservationId, dinnerTable2.ReservationId }, all.Select(r => r.ReservationId));
        Assert.Equal(new[] { dinnerTable2.ReservationId }, confirmed.Select(r => r.ReservationId));
    }

    [Fact(DisplayName = "TC-11-02 Confirm a pending booking: Confirmed")]
    [Trait("Technique", "State transition")]
    public async Task ConfirmABooking()
    {
        var booking = await AddReservationAsync(Alice, 3, Tomorrow, Dinner);

        var result = await NewReservationService().ChangeStatusAsync(booking.ReservationId, ReservationStatus.Confirmed, booking.RowVersion);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(ReservationStatus.Confirmed, (await ReloadReservationAsync(booking.ReservationId)).Status);
    }

    [Fact(DisplayName = "TC-11-03 Mark a booking completed before its time has started: rejected")]
    [Trait("Technique", "Boundary value analysis")]
    public async Task CompleteTooEarly()
    {
        var booking = await AddReservationAsync(Alice, 3, Tomorrow, Dinner, ReservationStatus.Confirmed);

        var result = await NewReservationService().ChangeStatusAsync(booking.ReservationId, ReservationStatus.Completed, booking.RowVersion);

        Assert.Equal("A reservation can only be marked as completed once its time has started.", result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-11-04 Mark a booking completed once its time has started: Completed")]
    [Trait("Technique", "Boundary value analysis")]
    public async Task CompleteAfterItStarted()
    {
        var booking = await AddReservationAsync(Alice, 3, Yesterday, Dinner, ReservationStatus.Confirmed);

        var result = await NewReservationService().ChangeStatusAsync(booking.ReservationId, ReservationStatus.Completed, booking.RowVersion);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(ReservationStatus.Completed, (await ReloadReservationAsync(booking.ReservationId)).Status);
    }

    [Theory(DisplayName = "TC-11-05 Status changes the rules don't allow: rejected")]
    [Trait("Technique", "State transition")]
    [InlineData(ReservationStatus.Pending, ReservationStatus.Completed)]
    [InlineData(ReservationStatus.Confirmed, ReservationStatus.Pending)]
    [InlineData(ReservationStatus.Cancelled, ReservationStatus.Confirmed)]
    [InlineData(ReservationStatus.Completed, ReservationStatus.Cancelled)]
    public async Task InvalidStatusChanges(string from, string to)
    {
        var booking = await AddReservationAsync(Alice, 3, Yesterday, Dinner, from);

        var result = await NewReservationService().ChangeStatusAsync(booking.ReservationId, to, booking.RowVersion);

        Assert.Equal($"A {from} reservation can't be changed to {to}.", result.ErrorMessage);
        Assert.Equal(from, (await ReloadReservationAsync(booking.ReservationId)).Status);
    }

    [Fact(DisplayName = "TC-11-06 The buttons offered for each status")]
    [Trait("Technique", "State transition")]
    public async Task ButtonsForEachStatus()
    {
        var service = NewReservationService();
        var pending = await AddReservationAsync(Alice, 1, Tomorrow, Dinner);
        var confirmedUpcoming = await AddReservationAsync(Alice, 2, Tomorrow, Dinner, ReservationStatus.Confirmed);
        var confirmedStarted = await AddReservationAsync(Alice, 3, Yesterday, Dinner, ReservationStatus.Confirmed);
        var cancelled = await AddReservationAsync(Alice, 4, Tomorrow, Dinner, ReservationStatus.Cancelled);
        var completed = await AddReservationAsync(Alice, 5, Yesterday, Dinner, ReservationStatus.Completed);

        Assert.Equal(new[] { ReservationStatus.Confirmed, ReservationStatus.Cancelled }, service.GetAllowedStatusChanges(pending));
        Assert.Equal(new[] { ReservationStatus.Cancelled }, service.GetAllowedStatusChanges(confirmedUpcoming));
        Assert.Equal(new[] { ReservationStatus.Cancelled, ReservationStatus.Completed }, service.GetAllowedStatusChanges(confirmedStarted));
        Assert.Empty(service.GetAllowedStatusChanges(cancelled));
        Assert.Empty(service.GetAllowedStatusChanges(completed));
    }

    [Fact(DisplayName = "TC-11-07 Staff change a confirmed booking: saved, and it stays Confirmed")]
    [Trait("Technique", "Decision table")]
    public async Task StaffChangeKeepsStatus()
    {
        var booking = await AddReservationAsync(Alice, 3, Tomorrow, Dinner, ReservationStatus.Confirmed);

        var result = await NewReservationService().ModifyForStaffAsync(booking.ReservationId, Booking(4, Tomorrow, Lunch, partySize: 6), booking.RowVersion);

        Assert.True(result.Succeeded, result.ErrorMessage);
        var saved = await ReloadReservationAsync(booking.ReservationId);
        Assert.Equal((4, Lunch, 6, ReservationStatus.Confirmed), (saved.TableId, saved.TimeSlot, saved.PartySize, saved.Status));
    }

    [Fact(DisplayName = "TC-11-08 Staff move a booking into a taken slot: rejected")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task StaffMoveIntoATakenSlot()
    {
        await AddReservationAsync(Bob, 4, Tomorrow, Lunch);
        var booking = await AddReservationAsync(Alice, 3, Tomorrow, Dinner);

        var result = await NewReservationService().ModifyForStaffAsync(booking.ReservationId, Booking(4, Tomorrow, Lunch), booking.RowVersion);

        Assert.Equal("Sorry, that table is already booked for this time slot. Please choose another time or table.", result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-11-09 Staff cancel a booking: Cancelled, with no further actions or edits")]
    [Trait("Technique", "State transition")]
    public async Task StaffCancel()
    {
        var booking = await AddReservationAsync(Alice, 3, Tomorrow, Dinner, ReservationStatus.Confirmed);

        var result = await NewReservationService().ChangeStatusAsync(booking.ReservationId, ReservationStatus.Cancelled, booking.RowVersion);

        Assert.True(result.Succeeded, result.ErrorMessage);
        var cancelled = await ReloadReservationAsync(booking.ReservationId);
        Assert.Equal(ReservationStatus.Cancelled, cancelled.Status);
        Assert.Empty(NewReservationService().GetAllowedStatusChanges(cancelled));
        Assert.False(NewReservationService().CanChangeDetails(cancelled));
        Assert.Equal("This reservation has already been cancelled.",
            (await NewReservationService().GetForStaffChangeAsync(booking.ReservationId)).ErrorMessage);
    }

    [Fact(DisplayName = "TC-11-10 A booking that doesn't exist: 'Reservation not found.' for every staff action")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task BookingThatDoesNotExist()
    {
        const int missing = 99999999;

        var editPage = await NewReservationService().GetForStaffChangeAsync(missing);
        var edit = await NewReservationService().ModifyForStaffAsync(missing, Booking(3, Tomorrow, Dinner), new byte[8]);
        var status = await NewReservationService().ChangeStatusAsync(missing, ReservationStatus.Confirmed, new byte[8]);

        Assert.All(new[] { editPage, edit, status }, result => Assert.Equal("Reservation not found.", result.ErrorMessage));
    }

    [Fact(DisplayName = "TC-11-15 Upcoming: bookings from today onwards by date, time then table, without yesterday's; the status filter works")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task UpcomingList()
    {
        await AddReservationAsync(Alice, 1, Yesterday, Dinner);   // already passed
        var nextWeek = await AddReservationAsync(Bob, 2, Tomorrow.AddDays(6), Lunch);
        var tomorrowDinner = await AddReservationAsync(Alice, 1, Tomorrow, Dinner, ReservationStatus.Confirmed);
        var tomorrowLunchTable4 = await AddReservationAsync(Bob, 4, Tomorrow, Lunch);
        var tomorrowLunchTable3 = await AddReservationAsync(Alice, 3, Tomorrow, Lunch);
        var today = await AddReservationAsync(Alice, 5, Today, Lunch);

        var all = await NewReservationService().GetUpcomingForStaffAsync(null, 1);
        var confirmed = await NewReservationService().GetUpcomingForStaffAsync(ReservationStatus.Confirmed, 1);

        Assert.Equal(
            new[] { today, tomorrowLunchTable3, tomorrowLunchTable4, tomorrowDinner, nextWeek }.Select(r => r.ReservationId),
            all.Items.Select(r => r.ReservationId));
        Assert.Equal((1, 1), (all.Page, all.TotalPages));
        Assert.Equal(new[] { tomorrowDinner.ReservationId }, confirmed.Items.Select(r => r.ReservationId));
    }

    [Theory(DisplayName = "TC-11-16 Upcoming in pages of 25: 26 bookings make 2 pages, and a page number outside the list gives the first or last page")]
    [Trait("Technique", "Boundary value analysis")]
    [InlineData(0, 1, 1, 1, 0)]
    [InlineData(25, 1, 1, 1, 25)]
    [InlineData(26, 1, 1, 2, 25)]
    [InlineData(26, 2, 2, 2, 1)]
    [InlineData(26, 0, 1, 2, 25)]
    [InlineData(26, 3, 2, 2, 1)]
    public async Task UpcomingInPages(int bookings, int pageAskedFor, int pageShown, int totalPages, int onThePage)
    {
        // Tables 1 to 10 at each seating tomorrow, so no two bookings share a slot.
        for (var i = 0; i < bookings; i++)
            await AddReservationAsync(Alice, i % 10 + 1, Tomorrow, ReservationTimeSlots.All[i / 10]);

        var page = await NewReservationService().GetUpcomingForStaffAsync(null, pageAskedFor);

        Assert.Equal((pageShown, totalPages, onThePage), (page.Page, page.TotalPages, page.Items.Count));
    }

    [Fact(DisplayName = "TC-11-17 The pending count: only pending bookings from today onwards")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task PendingCount()
    {
        await AddReservationAsync(Alice, 1, Yesterday, Dinner);                                  // passed
        await AddReservationAsync(Alice, 2, Today, Dinner);                                      // counted
        await AddReservationAsync(Bob, 3, Tomorrow.AddDays(6), Lunch);                           // counted
        await AddReservationAsync(Bob, 4, Tomorrow, Lunch, ReservationStatus.Confirmed);         // already confirmed
        await AddReservationAsync(Alice, 5, Tomorrow, Lunch, ReservationStatus.Cancelled);       // cancelled

        Assert.Equal(2, await NewReservationService().CountPendingAsync(Today));
    }
}
