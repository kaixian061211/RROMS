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

- Download: https://github.com/stripe/stripe-cli/releases/latest
- Extract stripe.exe and add its folder to PATH.
- Restart VS Code/Visual Studio if necessary.
- Check installation:

powershell:
stripe version

dotnet add package Stripe.net --version 53.0.0

$paymentStripeKey = "sk_test_REPLACE_WITH_YOUR_KEY"

dotnet user-secrets set "Stripe:SecretKey" $paymentStripeKey
dotnet user-secrets set "Stripe:Currency" "myr"


## start listener

powershell:
stripe listen --api-key $paymentStripeKey --events "checkout.session.completed,checkout.session.async_payment_succeeded,checkout.session.async_payment_failed,checkout.session.expired" --forward-to https://localhost:7116/webhooks/stripe --skip-verify


## project folder

powershell:
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_REPLACE_WITH_SECRET_FROM_TERMINAL_A"

dotnet run --launch-profile https


## Open the payment page

powershell
Start-Process "https://localhost:7116/Payments/Demo"


## Test card

text
Card:   4242 4242 4242 4242
Expiry: Any future date
CVC:    Any three digits


## Order and reservation notifications

Notification timestamps retain the database-local `SYSDATETIME()` convention used
by orders and reservations. The inbox displays that recorded wall time unchanged;
it does not label it UTC or convert it to the browser's timezone. Keep the database
server timezone consistent across deployments.

Notification delivery channels run concurrently with individual five-second
cancellation deadlines. SMTP also has a total operation deadline configured by
`Smtp:TimeoutSeconds` (default 5, bounded to 1–30 seconds), including when used by
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
to active Customer accounts through `NotificationPublisher`. Ordinary edits do
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
