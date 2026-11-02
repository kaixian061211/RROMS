using System.Globalization;

namespace RROMS.Web.Models.Notifications;

public static class NotificationFormatter
{
    // Notification.CreatedAt is database-local wall time (SYSDATETIME).
    // Formatting must neither declare it UTC nor apply a browser-timezone shift.
    public static string When(DateTime value) =>
        value.ToString("dd MMM yyyy, h:mm tt", CultureInfo.InvariantCulture);

    public static string MachineTime(DateTime value) =>
        value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
}
