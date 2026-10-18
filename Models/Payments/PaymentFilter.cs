namespace RROMS.Web.Models.Payments;

public enum DateRangePreset
{
    All,
    Today,
    ThisWeek,
    ThisMonth,
    Custom
}

/// <summary>Filters shared by the payment list, the exports and the reports.</summary>
public class PaymentFilter
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Method { get; set; }

    public DateRangePreset Preset { get; set; } = DateRangePreset.All;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public (DateTime? From, DateTime? ToExclusive) ResolveRange(DateTime? now = null)
    {
        var today = (now ?? DateTime.Now).Date;
        DateTime? rangeStart = null;
        DateTime? rangeEnd = null;

        switch (Preset)
        {
            case DateRangePreset.Today:
                rangeStart = today;
                rangeEnd = today.AddDays(1);
                break;

            case DateRangePreset.ThisWeek:
                var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
                rangeStart = today.AddDays(-daysSinceMonday);
                rangeEnd = rangeStart.Value.AddDays(7);
                break;

            case DateRangePreset.ThisMonth:
                rangeStart = new DateTime(today.Year, today.Month, 1);
                rangeEnd = rangeStart.Value.AddMonths(1);
                break;

            case DateRangePreset.Custom:
                rangeStart = From?.Date;
                rangeEnd = To?.Date.AddDays(1);   // include the whole "to" day
                break;
        }

        return (rangeStart, rangeEnd);
    }
}