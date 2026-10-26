using System.ComponentModel.DataAnnotations;
using RROMS.Web.Models.OrdersAndReservations;

namespace RROMS.Web.Models.CustomerSupportAndFeedback;

/// <summary>
/// Something a customer asks the restaurant for. Separate from feedback, which rates a finished visit.
/// </summary>
public class CustomerRequest
{
    public int CustomerRequestId { get; set; }

    /// <summary>No FK yet: the user table belongs to the login module.</summary>
    public int CustomerUserId { get; set; }

    /// <summary>Optional: the booking this request is about.</summary>
    public int? ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    [Required, StringLength(500)]
    public string RequestText { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Status { get; set; } = RequestStatus.Pending;

    public DateTime CreatedAt { get; set; }
}
