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
