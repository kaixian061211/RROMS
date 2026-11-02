1. Restore packages

```powershell
dotnet restore
```

## 2. Configure settings

Place the team's filled `secrets.shared.json` beside `PaymentModule.csproj` (Stripe, SMTP and OneSignal).

Create your personal file if missing:

```powershell
Copy-Item .\secrets.local.example.json .\secrets.local.json
```

Set `ConnectionStrings__DefaultConnection` to your database. For a new Admin, fill both `DefaultAdmin__*` fields; change the password and clear them after login.

Startup applies migrations. Back up existing databases and reconcile old Stripe checkouts before the Payment UTC conversion.

Never commit either private file. Rotate the exposed SMTP app password.

## 3. Run

Trust HTTPS once:

```powershell
dotnet dev-certs https --trust
```

Check settings, then start:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1 -Check
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1
```

Open **https://localhost:7116**. Use the launcher; plain `dotnet run` ignores these private files. Add menu items/tables through Admin.

## 4. Test a payment

Create a customer order and choose Card. Test with `4242 4242 4242 4242`, a future expiry and any three-digit CVC. Return and check **Paid** and the receipt. If pending, select **Check payment status**.

Webhooks are optional for local testing. Never use real card details.

## 5. Notifications and tracking

Open `/Notifications` for the inbox and `/Tracking` for orders/bookings. Tracking uses live events and five-second polling while visible.

In `secrets.shared.json`:

- Email: fill `Smtp__*` with matching sender credentials.
- Push: fill `Notifications__WebPush__*`: AppId, ApiKey, stable AliasSecret (32+ characters), PublicBaseUrl `https://localhost:7116`.

Match that origin in OneSignal. Sign in and allow browser notifications.

Email: confirmed reservations, receipts and refunds. Orders: inbox/live/push. Promotions: active customers with order/booking history. Announcements: all active customers. Delivery failures keep inbox records.

## 6. Tests

```powershell
$env:RROMS_TEST_CONNECTION = 'Server=localhost;Database=RROMS_NotificationTracking_Tests;Trusted_Connection=True;TrustServerCertificate=True'
dotnet test Tests/RROMS.Tests/RROMS.Tests.csproj
node --test Tests/Browser/*.test.cjs Tests/Configuration/*.test.cjs
```

Tests reset the isolated database and use fake/local providers. Verify actual delivery separately.