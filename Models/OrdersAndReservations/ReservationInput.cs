using System.ComponentModel.DataAnnotations;

namespace RROMS.Web.Models.OrdersAndReservations;

/// <summary>What the customer chooses when making or changing a reservation.</summary>
public class ReservationInput
{
    [Display(Name = "Table")]
    public int TableId { get; set; }

    [Display(Name = "Date")]
    [Required(ErrorMessage = "Please choose a date.")]
    public DateOnly ReservationDate { get; set; }

    [Display(Name = "Time")]
    public TimeOnly TimeSlot { get; set; }

    [Display(Name = "Party size")]
    [Range(1, int.MaxValue, ErrorMessage = "Party size must be at least 1.")]
    public int PartySize { get; set; } = 2;

    [Display(Name = "Special request (optional)")]
    [StringLength(500, ErrorMessage = "Special requests can be at most 500 characters.")]
    public string? SpecialRequest { get; set; }

    /// <summary>Fills the form with a reservation's current values, for the edit page.</summary>
    public static ReservationInput From(Reservation reservation) => new()
    {
        TableId = reservation.TableId,
        ReservationDate = reservation.ReservationDate,
        TimeSlot = reservation.TimeSlot,
        PartySize = reservation.PartySize,
        SpecialRequest = reservation.SpecialRequest
    };
}
