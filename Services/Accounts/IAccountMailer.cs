namespace RROMS.Web.Services.Accounts;

/// <summary>Sends the e-mails of the account module (welcome e-mail, password-reset code).</summary>
public interface IAccountMailer
{
    /// <summary>
    /// Sends one HTML e-mail through the shared SMTP sender. Never throws: returns false when SMTP is switched off
    /// (Smtp:Host is empty) or sending failed, so a mail problem can never break account creation or the reset page.
    /// </summary>
    Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody);
}
