# RROMS

## Restore packages

Run from the project folder:

```powershell
dotnet restore
```

## Configure a local development environment

The local launcher reads two private JSON files beside `PaymentModule.csproj`.
Create them from the blank templates:

```powershell
Copy-Item secrets.shared.example.json secrets.shared.json
Copy-Item secrets.local.example.json secrets.local.json
```

In `secrets.shared.json`, configure the shared Stripe sandbox key, SMTP
credentials and optional OneSignal settings. Keep this file private and share it
only through your team's private credential-sharing channel. Leave SMTP host
empty to disable email. `Stripe__WebhookSecret` is only needed when using
webhooks; the return-page payment flow does not require a Stripe CLI listener.

In `secrets.local.json`, set `ConnectionStrings__DefaultConnection` to your own
SQL Server database. Each teammate should use their own connection. For a new
Development database, also set `DefaultAdmin__Email` and
`DefaultAdmin__Password` to create the initial administrator. Existing
accounts are not changed. Change the bootstrap password after the first login
and remove it from the file.

Start the app with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1
```

To validate the files without starting the app or connecting to the database:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1 -Check
```

The launcher loads the shared and local files as process environment variables;
local values take precedence. It does not change Windows user environment
variables. Plain `dotnet run` does not read these files; it uses environment
variables and Development User Secrets instead.

## Test an online payment

Sign in as a customer, create an order and choose online card payment. Use these
Stripe sandbox card details:

```text
Card:   4242 4242 4242 4242
Expiry: Any future date
CVC:    Any three digits
```

Complete checkout and return to the app. Check for **Payment received**, a
**Paid** status in payment history, and a downloadable receipt. If the payment
is not yet confirmed, use **Check payment status** before attempting another
payment. Internet access is required. Without webhooks, a payment may remain
pending until the customer returns and checks its status.

## Notifications and order tracking

Order and reservation events create in-app notifications. Notifications can
also be delivered in real time and through Web Push when the customer has a
push subscription. Email delivery depends on the event and on SMTP being
configured. Failed delivery does not remove the saved inbox notification.

Order and reservation tracking pages refresh status while open and can also
update immediately when a real-time notification arrives.

## Keep private configuration out of Git

`secrets.shared.json` and `secrets.local.json` are private and ignored by Git.
Only the blank `.example.json` templates belong in the repository. These files
are not encrypted. For deployment, configure the same setting names in the
hosting provider's secret environment settings. ASP.NET Core maps `__` to `:`;
see Microsoft's
[configuration guidance](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/).

An SMTP app password was previously committed to this repository. Rotate it
before using or sharing the account. Removing a secret from the current source
does not remove it from Git history.
