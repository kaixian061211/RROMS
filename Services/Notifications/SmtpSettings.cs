namespace RROMS.Web.Services.Notifications;

/// <summary>Bound from the "Smtp" section. Leave Host empty to switch e-mail off.</summary>
public class SmtpSettings
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromName { get; set; } = "RROMS Restaurant";
    public string FromEmail { get; set; } = "no-reply@rroms.local";

    public bool Enabled => !string.IsNullOrWhiteSpace(Host);
}
