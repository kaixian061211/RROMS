1. Restore packages

```powershell
dotnet restore
```

## 2. Add the shared settings file

Get the filled `secrets.shared.json` from the team and place it beside `PaymentModule.csproj`. It contains the Stripe test key and the shared email/push settings.

The `.example.json` files are only templates. They do not contain working keys.

Stripe uses these settings in `secrets.shared.json`:

```json
{
  "Stripe__SecretKey": "sk_test_YOUR_TEST_SECRET_KEY",
  "Stripe__WebhookSecret": "",
  "Stripe__Currency": "MYR",
  "Stripe__CheckoutExpiryMinutes": "35"
}
```

If you received the filled file, keep its existing values. The key above is only a placeholder. Normal card testing does not need a publishable key or Stripe CLI.

## 3. Set your database connection

Create your own local settings file. Run this only if you have not created it yet:

```powershell
Copy-Item .\secrets.local.example.json .\secrets.local.json
```

Open `secrets.local.json` and change the server/database to your own:

```json
{
  "ConnectionStrings__DefaultConnection": "Server=localhost;Database=RROMS_Dev;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true",
  "DefaultAdmin__Email": "",
  "DefaultAdmin__Password": ""
}
```

Fill both Admin fields if you need an initial Admin account. After signing in, change its password and clear these fields.

Use a local development database. Startup creates the tables and applies migrations automatically. For an existing database, back it up and read the [Payment migration notes](Services/Payments/README.md#utc-migration-review-before-deployment) before running.

A new database has no menu items or restaurant tables. Add them through the Admin pages before testing orders. Existing demo data and uploaded images can be shared separately by the module owners.

## 4. Run the project

Trust the local HTTPS certificate once:

```powershell
dotnet dev-certs https --trust
```

Check the settings, then start the app:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1 -Check
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1
```

Open **https://localhost:7116**.

Use this launcher because plain `dotnet run` does not read the two private JSON files. Restart the app after changing them. The check command checks the files; it does not test the database or Stripe connection.

## 5. Test a Stripe payment

Sign in as a customer, create an order and choose Card. Use these test details:

```text
Card number: 4242 4242 4242 4242
Expiry:      Any future date
CVC:         Any 3 digits
```

Return to RROMS after checkout. Check that the payment is marked Paid and the receipt opens. If it is still pending, choose **Check payment status** before paying again. Do not use real card details.

## Optional: Stripe webhooks

You can test payments without this step. Webhooks update RROMS even when the customer does not return from Stripe.

Install the [Stripe CLI](https://docs.stripe.com/cli/install), then run these commands in another terminal using the same team sandbox:

```powershell
stripe login
stripe listen --forward-to https://localhost:7116/webhooks/stripe --events "checkout.session.completed,checkout.session.async_payment_succeeded,checkout.session.async_payment_failed,checkout.session.expired"
```

Add `Stripe__WebhookSecret` to your existing `secrets.local.json` using the `whsec_...` value shown by the listener. Keep your other settings, restart the app and leave the listener running. Each teammate uses their own listener secret.

Keep `secrets.shared.json` and `secrets.local.json` out of Git. Share only the shared file privately; each teammate keeps their own local file.