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
}
