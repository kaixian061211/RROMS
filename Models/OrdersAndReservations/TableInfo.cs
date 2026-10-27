namespace RROMS.Web.Models.OrdersAndReservations;

/// <summary>
/// What bookings and orders need to know about a restaurant table. The RestaurantTable table belongs to
/// another module, so reservations and orders only see tables through IRestaurantLookup.
/// TableId is the RestaurantTableId stored in Reservation.TableId and Order.TableId; TableNumber is what people see.
/// </summary>
public record TableInfo(int TableId, string TableNumber, string AreaName, int Capacity, bool IsActive);

/// <summary>The opening hours for one day of the week. When IsClosed is false, Opens and Closes are both set.</summary>
public record OpeningHours(bool IsClosed, TimeOnly? Opens, TimeOnly? Closes);
