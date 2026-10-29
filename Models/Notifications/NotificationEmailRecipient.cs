namespace RROMS.Web.Models.Notifications;

// Supply from trusted server code, never bind directly from browser input.
public sealed record NotificationEmailRecipient(int UserId, string Email);