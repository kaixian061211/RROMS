# Notification recipient foreign key

Migration: `AddNotificationUserForeignKey`.

`Notification.UserId` references the existing `UserAccount.UserId` primary key with
restricted deletion. The notification table retains its ten columns. No account
business code or old migrations were changed. Deactivating an account does not
delete its notification history. The constraint checks account existence, not role;
the notification service must still enforce ownership and customer targeting.

## Before applying to an application database

Use the actual configured application connection, not a similarly named database.
On 29 October 2026, this machine's User Secrets connection was verified as
`localhost/RROMS_Payment`. That database has no orphan notification recipients,
and `AddNotificationUserForeignKey` is applied with its constraint present.
The checked-in LocalDB connection is still a fallback for other machines; configure
each developer's own connection in User Secrets. Re-run the audit for other databases.

Run this read-only query first:

```sql
SELECT DISTINCT n.UserId
FROM [Notification] AS n
LEFT JOIN [UserAccount] AS u ON u.UserId = n.UserId
WHERE u.UserId IS NULL
ORDER BY n.UserId;
```

If any IDs are returned, reconcile their actual owners with the account module
owner. Do not delete notices, invent application accounts, or automatically remap
IDs to make migration succeed. An existing numeric ID may also have belonged to a
temporary Development identity; check its ownership, not just its existence.

Once the correct references are confirmed, apply with:

```powershell
dotnet ef database update
```

The generated migration adds only `FK_Notification_UserAccount_UserId` with
restricted deletion. Its rollback drops only that constraint. SQL Server validates
existing records when the constraint is added; nothing is automatically reconciled.
Development startup also runs migrations, so reconcile records before starting
that environment with this version.

## Testing and Development identities

The guarded `RROMS_Tests` database is separate from application data. Its fixtures
create missing account rows for their temporary customer IDs (1, 2, 9001, 9002)
without changing existing rows. This fixture-only seeding never runs in the app.
It keeps existing order/reservation/payment tests subject to the real constraint.

Use the real `/Account/Login` for application verification: it issues the actual
`UserAccount.UserId` in `ClaimTypes.NameIdentifier`. `/Dev/SwitchUser` still uses
temporary principals and must not be used to establish the ownership of real data.
