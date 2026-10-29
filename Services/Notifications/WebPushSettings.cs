namespace RROMS.Web.Services.Notifications;

public sealed class WebPushSettings
{
    public const string SectionName = "Notifications:WebPush";

    public string AppId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string AliasSecret { get; set; } = string.Empty;
    public string PublicBaseUrl { get; set; } = string.Empty;
}