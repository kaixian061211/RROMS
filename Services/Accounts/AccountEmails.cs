using System.Net;

namespace RROMS.Web.Services.Accounts;

/// <summary>The wording of the emails the account module sends.</summary>
public static class AccountEmails
{
    public static (string Subject, string Body) StaffWelcome(string fullName, string email, string role, string loginUrl)
    {
        string name = WebUtility.HtmlEncode(fullName);
        string subject = "Your RROMS account has been created";
        string body = $@"
<p>Hello {name},</p>
<p>An administrator has created a <strong>{WebUtility.HtmlEncode(role)}</strong> account for you on the
Restaurant Reservation and Order Management System (RROMS).</p>
<p>Your login email is <strong>{WebUtility.HtmlEncode(email)}</strong>.<br/>
Your administrator will give you your initial password directly. For your safety it is not included in this email.</p>
<p><a href=""{WebUtility.HtmlEncode(loginUrl)}"">Log in to RROMS</a></p>
<p>After your first login, please change your password on your profile page. If you do not know your
password, choose <em>Forgot password?</em> on the login page and we will send a code to this address.</p>
<p>If you were not expecting this email, please ignore it or contact the restaurant.</p>";
        return (subject, body);
    }

    public static (string Subject, string Body) PasswordResetOtp(string fullName, string otp, int minutesValid)
    {
        string subject = "Your RROMS password reset code";
        string body = $@"
<p>Hello {WebUtility.HtmlEncode(fullName)},</p>
<p>Use this code to reset your RROMS password:</p>
<p style=""font-size:28px;letter-spacing:6px;font-weight:bold"">{otp}</p>
<p>The code expires in {minutesValid} minutes and can only be used once. Never share it with anyone.</p>
<p>If you did not ask to reset your password, you can ignore this email. Your password has not been changed.</p>";
        return (subject, body);
    }
}
