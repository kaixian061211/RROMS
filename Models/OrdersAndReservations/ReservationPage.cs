namespace RROMS.Web.Models.OrdersAndReservations;

/// <summary>One page of a long list of reservations. Page counts from 1, and TotalPages is at least 1.</summary>
public record ReservationPage(List<Reservation> Items, int Page, int TotalPages)
{
    public const int PageSize = 25;
}
