namespace RROMS.Web.Models.Payments;

public enum ReportGrouping
{
    Daily,
    Weekly,
    Monthly
}

public class PaymentSummary
{
    public decimal TotalRevenue { get; set; }
    public int PaidCount { get; set; }
    public decimal AverageOrderValue { get; set; }

    public int PendingCount { get; set; }
    public decimal PendingAmount { get; set; }

    public int FailedCount { get; set; }

    public int RefundedCount { get; set; }
    public decimal RefundedAmount { get; set; }
}

public class RevenuePoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public class MethodBreakdown
{
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public class PaymentReport
{
    public PaymentSummary Summary { get; set; } = new();
    public List<RevenuePoint> Trend { get; set; } = new();
    public List<MethodBreakdown> ByMethod { get; set; } = new();
}