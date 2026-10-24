using System.ComponentModel.DataAnnotations;

namespace RROMS.Web.Models.Payments;

public enum DateRangePreset
{
    All,
    Today,
    ThisWeek,
    ThisMonth,
    Custom
}

/// <summary>Filters for the payment transaction list.</summary>
public class PaymentFilter : IValidatableObject
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Method { get; set; }

    public DateRangePreset Preset { get; set; } = DateRangePreset.All;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public (DateTime? From, DateTime? ToExclusive) ResolveRange(
        DateTime? now = null)
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

                // The maximum date has no representable next day.
                // No upper bound includes that entire day without overflowing.
                rangeEnd = To is DateTime end &&
                           end.Date < DateTime.MaxValue.Date
                    ? end.Date.AddDays(1)
                    : null;
                break;
        }

        return (rangeStart, rangeEnd);
    }

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (!Enum.IsDefined(Preset))
        {
            yield return new ValidationResult(
                "Choose a valid date range.",
                new[] { nameof(Preset) });
        }

        if (!string.IsNullOrWhiteSpace(Status) &&
            !PaymentStatus.All.Contains(Status))
        {
            yield return new ValidationResult(
                "Choose a valid payment status.",
                new[] { nameof(Status) });
        }

        if (!string.IsNullOrWhiteSpace(Method) &&
            !PaymentMethod.All.Contains(Method))
        {
            yield return new ValidationResult(
                "Choose a valid payment method.",
                new[] { nameof(Method) });
        }

        if (Preset == DateRangePreset.Custom &&
            From is DateTime start &&
            To is DateTime end &&
            start.Date > end.Date)
        {
            yield return new ValidationResult(
                "The end date must be on or after the start date.",
                new[] { nameof(From), nameof(To) });
        }
    }
}