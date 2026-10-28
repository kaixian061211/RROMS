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
