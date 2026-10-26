namespace RROMS.Models.OrdersAndReservations;

public record MenuItemInfo(int MenuItemId,string Name,decimal Price,bool IsAvailable,string CategoryName,int CategoryDisplayOrder = 0
);
