1. Restore packages

Run inside the project folder:

```powershell
dotnet restore
```

2. Shared team secrets

Use `secrets.shared.json` for the team's Stripe sandbox key, SMTP account and
OneSignal settings. The team member managing these accounts fills the file once
and shares it privately with teammates. Everyone places their copy beside
`PaymentModule.csproj`.

To prepare the file from the blank template:

```powershell
Copy-Item secrets.shared.example.json secrets.shared.json
```

Edit the values in `secrets.shared.json`. Use the actual sandbox key for
`Stripe__SecretKey`. For email, set `Smtp__Host`, `Smtp__UserName`,
`Smtp__Password` and `Smtp__FromEmail`; the username and app password must belong
to the same account. Leave SMTP Host empty to disable email. OneSignal settings
are optional. Keep the push alias secret at least 32 characters long and stable.

No Stripe CLI, listener, webhook secret or extra package is needed for the
return-page payment flow. Set `Stripe__WebhookSecret` only when using webhooks.

3. Each teammate's database

Each teammate creates their own `secrets.local.json`:

```powershell
Copy-Item secrets.local.example.json secrets.local.json
```

Edit `ConnectionStrings__DefaultConnection` to use that teammate's SQL Server
instance and database. Everyone can use a different connection. Do not put a
database connection in the shared file.

For example, a local SQL Server connection has this form:

```json
{
  "ConnectionStrings__DefaultConnection": "Server=YOUR_SQL_SERVER;Database=YOUR_DATABASE;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true",
  "DefaultAdmin__Email": "",
  "DefaultAdmin__Password": ""
}
```

For a new database, fill both `DefaultAdmin` values to create the initial
administrator in Development. Existing accounts are unchanged. Change the
bootstrap password after the first login, then clear it from this file.
Optional settings such as `Notifications__WebPush__PublicBaseUrl` can also go in
this local file when a teammate needs a different value.

4. Run locally

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1
```

This command reads both private files, passes their values to the app as process
environment variables and starts the HTTPS launch profile. Local values override
shared values. It does not change Windows user environment variables or require
an IDE restart. The execution policy option applies only to this PowerShell
process.

To check the files without starting the app or connecting to the database:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1 -Check
```

Use this launcher for the file-based team setup. Plain `dotnet run` does not read
these two files; it uses existing environment variables and Development User
Secrets instead. A missing, empty or unchanged template database connection is
rejected by the launcher.

## Test a Stripe payment

Sign in as a customer, create an order and choose online card payment.

```text
Card:   4242 4242 4242 4242
Expiry: Any future date
CVC:    Any three digits
```

Complete checkout and return to the app. Confirm **Payment received**, **Paid**
in history and receipt download. If still unconfirmed, use **Check payment
status** before trying another payment. Internet access is required. Without
webhooks, leaving before returning or a delayed payment can leave the local
record pending until the customer revisits and checks again.

## Keeping private files out of Git

`secrets.shared.json` and `secrets.local.json` are ignored by Git and excluded
from build and publish output. Only the empty `.example.json` templates belong
in the repository. Share the real shared file through your team's private
credential-sharing channel. Keep each teammate's database/admin file local.
These JSON files are not encrypted.

Rotate the SMTP app password previously committed to this repository before
sharing it, then update `Smtp__Password` in the private shared file. Removing the
old value from the current source does not remove it from Git history.

For deployment, use the hosting provider's secret environment settings with the
same names. ASP.NET Core maps `__` to `:`; see Microsoft's
[configuration guidance](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/).

## Order and reservation notifications

Order placement and status changes create in-app notifications and are delivered
in real time and through Web Push when the customer has a push subscription.
Order notifications do not send email.

Reservation requests and customer-triggered reservation status changes also send
email to the signed-in customer's `ClaimTypes.Email` claim. Staff-triggered
reservation status changes are in-app, real-time, and Web Push only; the app
does not store customer email addresses on reservations. The authentication
provider must issue an email claim for customer email notifications to work.
