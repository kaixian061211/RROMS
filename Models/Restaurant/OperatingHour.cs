namespace RROMS.Web.Models.Restaurant;

/// <summary>Opening hours for one day of the week. There is at most one row per day.</summary>
public class OperatingHour
{
    public int OperatingHourId { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public bool IsClosed { get; set; }

    public TimeOnly? OpenTime { get; set; }

    public TimeOnly? CloseTime { get; set; }
}
