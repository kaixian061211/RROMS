using System.ComponentModel.DataAnnotations;
using RROMS.Web.Models.OrdersAndReservations;

namespace RROMS.Web.Models.CustomerSupportAndFeedback;

public class Feedback
{
    public int FeedbackId { get; set; }

    /// <summary>No FK yet: the user table belongs to the login module.</summary>
    public int CustomerUserId { get; set; }

    /// <summary>A rating belongs to ONE completed order or reservation; the other stays null.</summary>
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
    public int? ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [StringLength(1000)]
    public string? Comments { get; set; }

    public DateTime CreatedAt { get; set; }

    [StringLength(1000)]
    public string? AdminReply { get; set; }
    public DateTime? RepliedAt { get; set; }

    /// <summary>Soft delete by an admin: hidden from the website, kept in the database.</summary>
    public bool IsHidden { get; set; }
}
