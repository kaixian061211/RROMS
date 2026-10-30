1. Install packages

powershell:

dotnet add package Stripe.net --version 53.0.0
dotnet add package QuestPDF --version 2026.2.4
dotnet add package MailKit --version 4.18.1
dotnet add package Microsoft.EntityFrameworkCore --version 10.0.13
dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 10.0.13
dotnet add package Microsoft.EntityFrameworkCore.Design --version 10.0.13
dotnet add package ClosedXML --version 0.105.0
dotnet add package System.Security.Cryptography.Pkcs --version 10.0.13


2. Stripe setup

Run this command once per computer, inside the project folder:

```powershell
dotnet user-secrets set "Stripe:SecretKey" "sk_test_REPLACE_WITH_YOUR_KEY"
```

Replace the placeholder with your actual Stripe sandbox secret key. If the key
is already configured on that computer, skip this step. User Secrets are stored
locally and are not copied with the project. Do not commit the key.

No Stripe CLI, `stripe listen`, webhook secret or extra package is needed for
this return-page payment flow.

## Run the app

```powershell
dotnet run --launch-profile https
```

## Test a Stripe payment

Sign in as a customer, create an order and choose online card payment.
Use these sandbox card details:

```text
Card:   4242 4242 4242 4242
Expiry: Any future date
CVC:    Any three digits
```

Complete checkout and return to the app. Confirm that the page shows
**Payment received**, payment history shows **Paid**, and the receipt downloads.

The app checks the payment with Stripe when you return or click
**Check payment status**. Internet access is required. If payment is still
unconfirmed, use that button before attempting another payment.

Without webhooks, closing the browser before returning or a delayed payment can
leave the local record pending until you revisit the payment status page and
check again.

## Order and reservation notifications

Notification timestamps retain the database-local `SYSDATETIME()` convention used
by orders and reservations. The inbox displays that recorded wall time unchanged;
it does not label it UTC or convert it to the browser's timezone. Keep the database
server timezone consistent across deployments.

Notification delivery channels run concurrently with individual 30-second
cancellation deadlines. SMTP also has a total operation deadline configured by
`Smtp:TimeoutSeconds` (default 30, bounded to 1–30 seconds), including when used by
payment emails. Delivery is still awaited, with no background queue or retries.
The saved inbox record remains available if a provider fails or times out.

Tracking polls its authorized Razor `Status` handler every five seconds while the
page is visible, independently of SignalR. It refreshes the existing cards when
displayed details change; SignalR notifications also trigger an immediate check.

Order placement and status changes create in-app notifications and are delivered
in real time and through Web Push when the customer has a push subscription.
Order notifications do not send email.

Only reservation confirmation requests email, through the same single delivery
call as its system and push notifications. When the caller omits the address,
delivery looks up the affected customer's active `UserAccount` record using the
notification owner's ID. Explicit trusted recipients remain supported; missing
or invalid addresses are logged and skipped. Other reservation events keep email
disabled. Payment receipt emails keep their existing separate notifier.

New or newly activated promotions and announcements publish system/push notices
through `NotificationPublisher`. Promotions target active Customer accounts with
at least one order or reservation; announcements still target all active Customer
accounts. Ordinary edits do
not rebroadcast. Scheduled promotions are announced when published, including
their dates; there is no background watcher or automatic second announcement on
the start date. Drafts, expired records, staff and inactive accounts are excluded.
Long text is shortened for the inbox without changing the original record.
Publishing uses one notification per recipient; delivery failures are logged
without undoing the business save or automatically retrying the broadcast.

3. Shared SMTP email configuration

The `Smtp` section in `appsettings.json` holds the shared sender configuration:
`Host`, `Port`, `UserName`, `Password`, `FromName` and `FromEmail`.
`UserName` and the Google app password in `Password` must belong to the same
SMTP account.

The shared login and sender are `p24016577@student.newinti.edu.my`. Enter that
account's matching app password in `Smtp.Password` locally before distributing
the configured project to other computers.

Once these shared values are configured and copied with the project, other
computers don't need to set SMTP User Secrets. Start the app normally:

```powershell
dotnet run --launch-profile https
```

Existing `Smtp:*` User Secrets in Development and `Smtp__*` environment variables
still override values in `appsettings.json`. Computers with older local SMTP
overrides must use matching values or remove those overrides.
