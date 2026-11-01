# Payment module review notes

Payment owns customer checkout/status/cancellation/history/receipts and staff counter collection, payment searches and full refunds. Supported online methods are card, FPX and GrabPay through Stripe; counter methods are cash, card and e-wallet. Payment amounts are server-calculated from saved order lines and denominated in MYR. One payment row per order is retained across retries. No partial refunds, deposits or new business rules are introduced.

Staff can refund; Admin can view payment/refund records. Both existing staff roles retain counter collection access. Customer operations check ownership through the order provider. Financial mutations and saved inbox notifications commit together; delivery happens afterwards.

## UTC migration: review before deployment

`20261031005647_NormalizePaymentTimestampsToUtc` converts legacy Malaysia-local Payment.CreatedAt/PaidAt/RefundedAt to UTC and changes only Payment.CreatedAt's default to SYSUTCDATETIME(). The model snapshot change is limited to that default. The conversion is a one-time EF migration, not a runtime heuristic.

- Back up the operational database and deploy in a maintenance window after review.
- Reconcile/close all old online checkouts in Stripe before conversion, including any unresolved failed attempts. CreatedAt is also the attempt token stored in Stripe metadata.
- The migration holds a transactional table lock and refuses to convert while any pending online attempt remains. It does not expire sessions or change statuses on the user's behalf.
- EF migration history prevents double conversion. Down reverses the timestamp offset/default, with the same pending-attempt guard; coordinate any rollback with the application version.
- No operational database was used during this work. Conversion, reversal and rejection were tested only in the guarded RROMS_Tests database.
- New Payment timestamps use UTC; Payment screens, receipts and local-date filters use Asia/Kuala_Lumpur. The shared PaymentFormatter.When method retains its old behavior for other modules. Reports already expects UTC Payment timestamps and requires no code change for these fields.
- Other modules still have server-local timestamps (for example Order.CreatedAt). Normalizing those and their Reports interpretation is outside this Payment change.

## Recovery and verification limits

An interrupted Checkout response reuses the same attempt, Stripe idempotency key and expiry. An unresolved attempt must retain its original method. Changed Stripe configuration or customer email may prevent Stripe from replaying an old request; staff should reconcile it before allowing another payment.

Online full refunds are recorded locally only after Stripe confirms success. Pending/unknown refunds leave the payment paid and can be rechecked through the same refund action; that action checks for an existing full refund before creating one. There is no background refund reconciliation worker or refund-status webhook. Refunds made independently in the Stripe Dashboard are not automatically imported. Counter refunds record money staff have already returned manually.

Online PaidAt is Stripe's paid Checkout event time in UTC, from a signed webhook or a server-verified matching success event. Session/PaymentIntent creation time and browser return time are not used. Duplicate callbacks preserve the first saved timestamp. Counter PaidAt is the time staff records receipt; RefundedAt remains the time RROMS confirms the refund, not a historical processor backfill.

Server recovery uses Stripe's [Events API](https://docs.stripe.com/api/events/list), which retains 30 days, filters from the Checkout creation time and scans at most 1,000 completed/succeeded checkout events per check. A missing, mismatched or unavailable event leaves the payment unconfirmed and blocks another attempt until reconciliation; it does not substitute today's date. Restricted API keys need permission to read Events as well as Checkout sessions. Older unmatched successful attempts need staff reconciliation outside this change. Signed webhooks avoid this recovery scan and should be configured before deployment.

Optional signed checkout webhooks remain supported. Without them, a customer who does not return from Stripe must revisit payment status for local confirmation. Tests simulate Stripe responses and do not prove a particular Stripe account has enabled card/FPX/GrabPay or that its external payment, refund, SMTP or push delivery is working.

Run focused checks from this project folder:

```powershell
dotnet test Tests/RROMS.Tests/RROMS.Tests.csproj --filter 'FullyQualifiedName~Payments&Type!=Manual'
```

Tests use only TestDatabase's guarded RROMS_Tests database. Payment HTTP tests replace Stripe, payment email notifiers and notification transport with local fakes. The Type=Manual browser fixture is opt-in via RROMS_PAYMENT_PREVIEW=1 and RROMS_PAYMENT_PREVIEW_FILE (a temporary JSON path); it starts http://localhost:5198 and stops when a .stop file is placed beside the JSON file, or after 20 minutes. Do not run other database tests concurrently with that preview.

## UI and Stripe appearance

Payment pages use their own Razor layout and isolated layout stylesheet. Shared layout/styles and the legacy Tracking formatter are unchanged. Customer pages use dockets and narrow layouts; transaction tables become cards on small screens. Critical actions use a keyboard-accessible confirmation dialog, and valid submissions lock until navigation (restored on browser back).

New hosted Stripe sessions receive the RROMS display name, DESIGN.md marble background and pandan button colour, rounded borders, Inter (Stripe does not support Jakarta Sans), and the same ORD reference. This is per-session branding; it does not update team Stripe account settings. The installed SDK's options and Stripe's [Checkout Session API](https://docs.stripe.com/api/checkout/sessions/create) support these settings. Request-level tests check the branding, MYR amount and stable attempt key/expiry. Old unresolved sessions must be closed before deployment as described above.

## Review handoff (31 October 2026)

- Working branch: `codex/payment-functionality-ui`, based on `origin/main` at `7589fef` (includes Reports PR #30). No commit, push, PR or operational migration was performed.
- Final build succeeded. Final focused run: **96 Payment checks and 87 Reports checks passed (183 total)**. Coverage includes all supported methods through fakes, saved totals/ownership, counter collection/void/refund, online retry/recovery/refund outcomes, duplicate callbacks, notification failure boundaries, receipts, HTTP authorization/antiforgery, Malaysia date boundaries and the UTC migration's conversion/reversal/pending-attempt guard.
- A wider run before the final timestamp additions passed 352 of 380 tests. All 28 failures were reproduced on the unchanged main export: shared form helpers match the logout form, old login/access-denied expectations, and stale dashboard expectations. These require coordination with the shared test/owning-module maintainers; no out-of-scope fixes were made.
- Browser review covered desktop (1280px) and mobile (375px), responsive transaction cards and checkout, keyboard method selection/focus/confirmation dismissal, isolated counter collection and refund, pending/recovery/empty states, and filters. No horizontal overflow or browser errors were observed. Reduced-motion styles were reviewed; no external screen-reader audit was performed.
- An unpaid Stripe sandbox Checkout session confirmed the hosted branding visually, then was expired. No card details were entered and no charge/refund was submitted. Live processor outcomes and actual email/push delivery remain unverified.
- Review/deploy the UTC migration only after reconciling old checkout attempts. The app automatically applies pending migrations in Development, so review the target connection and migration plan before starting the normal app against an operational database.
