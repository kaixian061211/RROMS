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


2. Stripe setup (no CLI required)

Packages are already declared in the project; use `dotnet restore`.
Each computer still needs the existing .NET 10 SDK/database setup and a Stripe
sandbox API key through user secrets or the `Stripe__SecretKey` environment variable.
User secrets do not travel with the repository. Do not commit API keys.

```powershell
dotnet user-secrets set "Stripe:SecretKey" "sk_test_REPLACE_WITH_YOUR_KEY"
dotnet run --launch-profile https
```

No Stripe CLI, listener, new package or database migration is required for return
verification. This project has no automatic local listener to disable.

Sign in as a customer, create an order and choose card payment. In sandbox use
4242 4242 4242 4242, any future expiry and any three-digit CVC. Return to the app
and check Payment received, paid history and receipt download. Refresh several
times: the payment record and PaidAt should stay the same without duplicate
notifications. Counter payments must still require staff recording.

The success page verifies the stored Checkout Session with Stripe on entry and
manual recheck. Automatic polling only reads the database. Use Check payment
status before attempting another payment when confirmation is uncertain.
Without webhooks, leaving before returning or a delayed payment can leave the
local record pending until the customer revisits the status page and checks again.
Production should retain webhooks for unattended confirmation.

Optional local webhook testing uses Stripe CLI:

```powershell
stripe listen --events "checkout.session.completed,checkout.session.async_payment_succeeded,checkout.session.async_payment_failed,checkout.session.expired" --forward-to https://localhost:7116/webhooks/stripe --skip-verify
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_REPLACE_WITH_LISTENER_SECRET"
```

Restart the app after configuring the webhook secret.

## Order and reservation notifications

Order placement and status changes create in-app notifications and are delivered
in real time and through Web Push when the customer has a push subscription.
Order notifications do not send email.

Reservation requests and customer-triggered reservation status changes also send
email to the signed-in customer's `ClaimTypes.Email` claim. Staff-triggered
reservation status changes are in-app, real-time, and Web Push only; the app
does not store customer email addresses on reservations. The authentication
provider must issue an email claim for customer email notifications to work.

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
