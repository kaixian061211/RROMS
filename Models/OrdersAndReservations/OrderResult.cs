namespace RROMS.Web.Models.OrdersAndReservations;

/// <summary>
/// What an order action returns. Expected failures, like an item that isn't available,
/// carry a message for the page to show instead of throwing an exception.
/// </summary>
public class OrderResult
{
    public bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }
    public Order? Order { get; init; }

    public static OrderResult Success(Order order) =>
        new() { Succeeded = true, Order = order };

    public static OrderResult Failure(string errorMessage) =>
        new() { ErrorMessage = errorMessage };
}
