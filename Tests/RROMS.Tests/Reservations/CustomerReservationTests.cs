using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RROMS.Web.Models.OrdersAndReservations;

namespace RROMS.Tests.Reservations;

/// <summary>Story 5: a customer makes, modifies or cancels a table reservation based on availability.</summary>
[Trait("Type", "Unit")]
[Trait("Story", "5")]
public class CustomerReservationTests : DatabaseTest
{
    private const string SlotTaken =
        "Sorry, that table is already booked for this time slot. Please choose another time or table.";

    private static ReservationInput Booking(int table, DateOnly date, TimeOnly time, int partySize = 2, string? request = null) =>
        new() { TableId = table, ReservationDate = date, TimeSlot = time, PartySize = partySize, SpecialRequest = request };

    private Task<int> CountBookingsAsync(int table, DateOnly date, TimeOnly time) =>
        NewContext().Set<Reservation>().CountAsync(r => r.TableId == table && r.ReservationDate == date && r.TimeSlot == time);

    [Fact(DisplayName = "TC-05-01 Book a free table and time: saved as Pending with a RES reference")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task BookAFreeSlot()
    {
        var result = await NewReservationService().CreateAsync(Alice, Booking(3, Tomorrow, Dinner));

        Assert.True(result.Succeeded, result.ErrorMessage);
        var saved = await ReloadReservationAsync(result.Reservation!.ReservationId);
        Assert.Equal(ReservationStatus.Pending, saved.Status);
        Assert.Equal((3, Tomorrow, Dinner, 2), (saved.TableId, saved.ReservationDate, saved.TimeSlot, saved.PartySize));
        Assert.Matches(@"^RES-\d{6}$", ReservationFormatter.Reference(saved.ReservationId));
    }

    [Fact(DisplayName = "TC-05-02 Book a table and time someone else has already booked: rejected")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task BookATakenSlot()
    {
        await NewReservationService().CreateAsync(Alice, Booking(3, Tomorrow, Dinner));

        var second = await NewReservationService().CreateAsync(Bob, Booking(3, Tomorrow, Dinner));

        Assert.False(second.Succeeded);
        Assert.Equal(SlotTaken, second.ErrorMessage);
        Assert.Equal(1, await CountBookingsAsync(3, Tomorrow, Dinner));
    }

    [Fact(DisplayName = "TC-05-03 A cancelled booking frees its slot, so it can be booked again")]
    [Trait("Technique", "State transition")]
    public async Task RebookACancelledSlot()
    {
        var first = (await NewReservationService().CreateAsync(Alice, Booking(3, Tomorrow, Dinner))).Reservation!;
        await NewReservationService().CancelAsync(Alice, first.ReservationId, first.RowVersion);

        var again = await NewReservationService().CreateAsync(Bob, Booking(3, Tomorrow, Dinner));

        Assert.True(again.Succeeded, again.ErrorMessage);
        Assert.Equal(2, await CountBookingsAsync(3, Tomorrow, Dinner));
    }

    [Fact(DisplayName = "TC-05-04 Party size 0: rejected")]
    [Trait("Technique", "Boundary value analysis")]
    public async Task PartySizeZero()
    {
        var result = await NewReservationService().CreateAsync(Alice, Booking(3, Tomorrow, Dinner, partySize: 0));

        Assert.Equal("Party size must be at least 1.", result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-05-05 Party size 1: accepted")]
    [Trait("Technique", "Boundary value analysis")]
    public async Task PartySizeOne()
    {
        var result = await NewReservationService().CreateAsync(Alice, Booking(3, Tomorrow, Dinner, partySize: 1));

        Assert.True(result.Succeeded, result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-05-06 A time that isn't one of the seatings (19:15): rejected")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task TimeThatIsNotASeating()
    {
        var result = await NewReservationService().CreateAsync(Alice, Booking(3, Tomorrow, new TimeOnly(19, 15)));

        Assert.Equal("Please choose one of the available time slots.", result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-05-07 A date in the past (yesterday): rejected")]
    [Trait("Technique", "Boundary value analysis")]
    public async Task DateInThePast()
    {
        var result = await NewReservationService().CreateAsync(Alice, Booking(3, Yesterday, Dinner));

        Assert.Equal("Please choose a date and time in the future.", result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-05-08 Special request of exactly 500 characters: accepted")]
    [Trait("Technique", "Boundary value analysis")]
    public async Task SpecialRequestOf500()
    {
        var result = await NewReservationService().CreateAsync(Alice, Booking(3, Tomorrow, Dinner, request: new string('x', 500)));

        Assert.True(result.Succeeded, result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-05-09 Special request of 501 characters: rejected")]
    [Trait("Technique", "Boundary value analysis")]
    public async Task SpecialRequestOf501()
    {
        var result = await NewReservationService().CreateAsync(Alice, Booking(3, Tomorrow, Dinner, request: new string('x', 501)));

        Assert.Equal("Special requests can be at most 500 characters.", result.ErrorMessage);
    }

    [Theory(DisplayName = "TC-05-10 Table number: 1 and 10 accepted, 0 and 11 rejected")]
    [Trait("Technique", "Boundary value analysis")]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public async Task TableNumberBoundaries(int table, bool accepted)
    {
        var result = await NewReservationService().CreateAsync(Alice, Booking(table, Tomorrow, Dinner));

        Assert.Equal(accepted, result.Succeeded);
        if (!accepted)
            Assert.Equal("Please choose a table.", result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-05-11 Change your own booking to a free time: saved")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task ChangeToAFreeTime()
    {
        var booking = (await NewReservationService().CreateAsync(Alice, Booking(3, Tomorrow, Dinner))).Reservation!;

        var editPage = await NewReservationService().GetForChangeAsync(Alice, booking.ReservationId);
        var result = await NewReservationService().ModifyAsync(Alice, booking.ReservationId, Booking(3, Tomorrow, Lunch), booking.RowVersion);

        Assert.True(editPage.Succeeded, editPage.ErrorMessage);
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(Lunch, (await ReloadReservationAsync(booking.ReservationId)).TimeSlot);
    }

    [Theory(DisplayName = "TC-05-12 A customer changing a confirmed booking: back to Pending unless only the special request changes")]
    [Trait("Technique", "Decision table")]
    [InlineData("time", ReservationStatus.Pending)]
    [InlineData("date", ReservationStatus.Pending)]
    [InlineData("table", ReservationStatus.Pending)]
    [InlineData("party size", ReservationStatus.Pending)]
    [InlineData("special request only", ReservationStatus.Confirmed)]
    public async Task ChangingAConfirmedBooking(string whatChanges, string expectedStatus)
    {
        var booking = (await NewReservationService().CreateAsync(Alice, Booking(3, Tomorrow, Dinner))).Reservation!;
        await NewReservationService().ChangeStatusAsync(booking.ReservationId, ReservationStatus.Confirmed, booking.RowVersion);
        var confirmed = await ReloadReservationAsync(booking.ReservationId);

        var changed = whatChanges switch
        {
            "time" => Booking(3, Tomorrow, Lunch),
            "date" => Booking(3, Tomorrow.AddDays(1), Dinner),
            "table" => Booking(4, Tomorrow, Dinner),
            "party size" => Booking(3, Tomorrow, Dinner, partySize: 4),
            _ => Booking(3, Tomorrow, Dinner, request: "Window seat")
        };
        var result = await NewReservationService().ModifyAsync(Alice, booking.ReservationId, changed, confirmed.RowVersion);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(expectedStatus, (await ReloadReservationAsync(booking.ReservationId)).Status);
    }

    [Fact(DisplayName = "TC-05-13 Change your booking to a time someone else has booked: rejected")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task ChangeToATakenTime()
    {
        await NewReservationService().CreateAsync(Bob, Booking(3, Tomorrow, Lunch));
        var booking = (await NewReservationService().CreateAsync(Alice, Booking(3, Tomorrow, Dinner))).Reservation!;

        var result = await NewReservationService().ModifyAsync(Alice, booking.ReservationId, Booking(3, Tomorrow, Lunch), booking.RowVersion);

        Assert.Equal(SlotTaken, result.ErrorMessage);
        Assert.Equal(Dinner, (await ReloadReservationAsync(booking.ReservationId)).TimeSlot);
    }

    [Fact(DisplayName = "TC-05-14 Cancel your booking: Cancelled, and cancelling again is rejected")]
    [Trait("Technique", "State transition")]
    public async Task CancelABooking()
    {
        var booking = (await NewReservationService().CreateAsync(Alice, Booking(3, Tomorrow, Dinner))).Reservation!;

        var cancelled = await NewReservationService().CancelAsync(Alice, booking.ReservationId, booking.RowVersion);
        var again = await NewReservationService().CancelAsync(Alice, booking.ReservationId, cancelled.Reservation!.RowVersion);

        Assert.True(cancelled.Succeeded, cancelled.ErrorMessage);
        Assert.Equal(ReservationStatus.Cancelled, (await ReloadReservationAsync(booking.ReservationId)).Status);
        Assert.Equal("This reservation has already been cancelled.", again.ErrorMessage);
    }

    [Fact(DisplayName = "TC-05-15 A booking that has already passed can't be changed or cancelled")]
    [Trait("Technique", "Boundary value analysis")]
    public async Task PastBookingCantChange()
    {
        var past = await AddReservationAsync(Alice, 3, Yesterday, Dinner, ReservationStatus.Confirmed);

        var change = await NewReservationService().ModifyAsync(Alice, past.ReservationId, Booking(3, Tomorrow, Dinner), past.RowVersion);
        var cancel = await NewReservationService().CancelAsync(Alice, past.ReservationId, past.RowVersion);

        const string expected = "This reservation has already passed, so it can't be changed or cancelled.";
        Assert.Equal(expected, change.ErrorMessage);
        Assert.Equal(expected, cancel.ErrorMessage);
    }

    [Fact(DisplayName = "TC-05-16 A completed booking can't be changed or cancelled")]
    [Trait("Technique", "State transition")]
    public async Task CompletedBookingCantChange()
    {
        var completed = await AddReservationAsync(Alice, 3, Yesterday, Dinner, ReservationStatus.Completed);

        var cancel = await NewReservationService().CancelAsync(Alice, completed.ReservationId, completed.RowVersion);

        Assert.Equal("This reservation is completed, so it can't be changed or cancelled.", cancel.ErrorMessage);
    }

    [Fact(DisplayName = "TC-05-17 Another customer's booking can't be seen, changed or cancelled")]
    [Trait("Technique", "Security")]
    public async Task AnotherCustomersBooking()
    {
        var alices = (await NewReservationService().CreateAsync(Alice, Booking(3, Tomorrow, Dinner))).Reservation!;

        var seen = await NewReservationService().GetByIdAsync(Bob, alices.ReservationId);
        var editPage = await NewReservationService().GetForChangeAsync(Bob, alices.ReservationId);
        var change = await NewReservationService().ModifyAsync(Bob, alices.ReservationId, Booking(3, Tomorrow, Lunch), alices.RowVersion);
        var cancel = await NewReservationService().CancelAsync(Bob, alices.ReservationId, alices.RowVersion);

        Assert.Null(seen);
        Assert.Equal("Reservation not found.", editPage.ErrorMessage);
        Assert.Equal("Reservation not found.", change.ErrorMessage);
        Assert.Equal("Reservation not found.", cancel.ErrorMessage);
        Assert.Equal(ReservationStatus.Pending, (await ReloadReservationAsync(alices.ReservationId)).Status);
    }

    [Fact(DisplayName = "TC-05-18 Two edits of the same booking at once: the second gets 'changed by someone else'")]
    [Trait("Technique", "Concurrency")]
    public async Task TwoEditsAtOnce()
    {
        var booking = (await NewReservationService().CreateAsync(Alice, Booking(3, Tomorrow, Dinner))).Reservation!;
        var loadedVersion = booking.RowVersion;   // both browser tabs loaded this version

        var first = await NewReservationService().ModifyAsync(Alice, booking.ReservationId, Booking(3, Tomorrow, Lunch), loadedVersion);
        var second = await NewReservationService().ModifyAsync(Alice, booking.ReservationId, Booking(4, Tomorrow, Dinner), loadedVersion);

        Assert.True(first.Succeeded, first.ErrorMessage);
        Assert.Equal(
            "This reservation was changed by someone else while you were editing it. Please reload the page and try again.",
            second.ErrorMessage);
        var saved = await ReloadReservationAsync(booking.ReservationId);
        Assert.Equal((3, Lunch), (saved.TableId, saved.TimeSlot));   // the first edit, not overwritten by the second
    }

    [Fact(DisplayName = "TC-05-19 Two bookings for the same slot at the same moment: the second gets 'already booked'")]
    [Trait("Technique", "Concurrency")]
    public async Task TwoBookingsAtTheSameMoment()
    {
        // Bob's booking is saved after Alice's request has checked the slot is free, but before hers is saved:
        // the moment two requests overlap. Only the database's unique index can catch this.
        var bobBooksFirst = new BeforeSaving(() => AddReservationAsync(Bob, 3, Tomorrow, Dinner));

        var alices = await NewReservationService(bobBooksFirst).CreateAsync(Alice, Booking(3, Tomorrow, Dinner));

        Assert.True(bobBooksFirst.Ran);
        Assert.Equal(SlotTaken, alices.ErrorMessage);
        Assert.Equal(1, await CountBookingsAsync(3, Tomorrow, Dinner));
    }

    [Fact(DisplayName = "TC-05-20 The database itself rejects a second active booking for a slot (unique index, error 2601)")]
    [Trait("Technique", "Concurrency")]
    public async Task DatabaseBlocksADoubleBooking()
    {
        await AddReservationAsync(Alice, 3, Tomorrow, Dinner);

        // Saving straight to the database skips the service's check entirely.
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => AddReservationAsync(Bob, 3, Tomorrow, Dinner));

        var sqlError = Assert.IsType<SqlException>(error.InnerException);
        Assert.Equal(2601, sqlError.Number);   // 2601: a row would break a unique index
        Assert.Contains("IX_Reservation_TableId_ReservationDate_TimeSlot", sqlError.Message);
    }

    [Fact(DisplayName = "TC-05-21 The unique index ignores cancelled bookings")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task DatabaseAllowsRebookingACancelledSlot()
    {
        await AddReservationAsync(Alice, 3, Tomorrow, Dinner, ReservationStatus.Cancelled);

        var rebooked = await AddReservationAsync(Bob, 3, Tomorrow, Dinner);

        Assert.Equal(ReservationStatus.Pending, (await ReloadReservationAsync(rebooked.ReservationId)).Status);
    }

    [Fact(DisplayName = "TC-05-22 My reservations: only your own pending or confirmed upcoming bookings, soonest first")]
    [Trait("Technique", "Equivalence partitioning")]
    public async Task UpcomingList()
    {
        var dinner = await AddReservationAsync(Alice, 3, Tomorrow, Dinner, ReservationStatus.Confirmed);
        var lunch = await AddReservationAsync(Alice, 3, Tomorrow, Lunch);
        await AddReservationAsync(Alice, 4, Tomorrow, Lunch, ReservationStatus.Cancelled);
        await AddReservationAsync(Alice, 3, Yesterday, Dinner, ReservationStatus.Confirmed);
        await AddReservationAsync(Bob, 5, Tomorrow, Lunch);

        var upcoming = await NewReservationService().GetUpcomingAsync(Alice);

        Assert.Equal(new[] { lunch.ReservationId, dinner.ReservationId }, upcoming.Select(r => r.ReservationId));
    }

    [Theory(DisplayName = "TC-05-31 No date or no party size reaches the service: rejected with its own message, nothing saved")]
    [Trait("Technique", "Equivalence partitioning")]
    [InlineData("date", "Please choose a date.")]
    [InlineData("party size", "Please enter the party size.")]
    public async Task MissingDateOrPartySize(string missing, string expectedMessage)
    {
        var booking = Booking(3, Tomorrow, Dinner);
        if (missing == "date")
            booking.ReservationDate = null;
        else
            booking.PartySize = null;

        var result = await NewReservationService().CreateAsync(Alice, booking);

        Assert.Equal(expectedMessage, result.ErrorMessage);
        Assert.Equal(0, await CountBookingsAsync(3, Tomorrow, Dinner));
    }
}
