# Upgrade and operate FoodSupply

## Upgrade an existing database

Stop all app instances before upgrading. Take a MySQL backup and test its restore into a separate database first. MySQL DDL is not transactionally reversible; do not use migration rollback as a backup strategy.

```powershell
mysqldump --host=localhost --user=YOUR_USER --password --single-transaction --routines --triggers --result-file=foodsupply-backup.sql YOUR_DATABASE
```

Use your actual server, database and user. The password prompt keeps credentials out of shell history. Store backups outside the repository with restricted access. Test restoration using MySQL's `SOURCE` command into a separately created database, then check order counts, stock, invoices and paid totals.

With `ConnectionStrings:DefaultConnection` pointing to the intended database, run:

```powershell
dotnet run -- --upgrade-database
dotnet run
```

The explicit upgrade command checks for duplicate inventories, negative stock and invalid paid balances before applying migrations. It also hashes plaintext passwords created by the former user administration screen. Existing passwords remain usable; all browser sessions must sign in again. Resolve any preflight findings before retrying. Use this command instead of only `dotnet ef database update`, which does not migrate legacy plaintext passwords.

The migration preserves monetary precision and existing records. It:

- Uses inventory quantities as authoritative and synchronizes product stock displays. Products lacking inventory get a record using their existing quantity.
- Creates legacy opening batches and stock movements, including active order reservations. Review legacy expiry dates and batch identities against physical stock. Legacy records with spoiled/damaged quantities are quarantined pending inspection.
- Creates opening paid-balance entries. These are explicitly marked as summaries; individual historical payment receipts cannot be reconstructed.
- Creates legacy receipt/proof records for previously completed purchases and deliveries. Missing historical recipient/proof information is labeled as unavailable.

Review the generated upgrade SQL before deployment:

```powershell
dotnet ef migrations script 20260915010205_AddCustomerConcerns 20260920152853_OperationalIntegrity --output artifacts/upgrade.sql
```

If the original data is inconsistent, reconcile it on the restored test copy first. Do not guess which duplicate stock record is correct. Restore the complete pre-upgrade backup if an upgrade fails after schema changes.

## Password reset email

Set these using environment variables or a secret store, not committed configuration:

| Key | Purpose |
| --- | --- |
| `Application__PublicUrl` | Public HTTPS origin, e.g. `https://foodsupply.example.com` |
| `Smtp__Host` | SMTP server supporting STARTTLS |
| `Smtp__Port` | Port, defaults to 587 |
| `Smtp__Username` | SMTP username |
| `Smtp__Password` | SMTP credential |
| `Smtp__From` | Authorized sender address |

An administrator provisions the account email address; confirm it belongs to the employee before using recovery. Links expire in 30 minutes and can be used once. Recovery responses never reveal whether an email exists. If SMTP is unavailable, the app logs a configuration/delivery error without the link or token, and does not expose an alternate reset path. Password resets and administrative account changes invalidate existing sessions.

## Daily workflows

- **Stock:** open product/inventory details → Batches and stock history. Receive opening stock with a batch and expiry; use counted quantities and reasons for adjustments. Quarantine returned/damaged food. Stock on hand includes quarantined/expired goods, while sales allocate only usable batches, earliest expiry first. Expiry dates are treated as unusable starting on that date. An absent expiry means non-expiring stock; do not omit the date for perishable goods.
- **Purchasing:** open purchase details → Record receipt. Enter accepted, rejected and damaged quantities per line. Receipts may be partial. Rejected/damaged quantities do not enter stock; all three quantities count toward the ordered quantity. Create a replacement purchase if needed. Received or partially received purchases cannot have their original items edited.
- **Sales:** duplicate product lines are combined. Cancellation releases allocations once and preserves original quantities/prices for historical reporting. Only cancelled or delivered orders can be archived.
- **Delivery:** dispatch through the delivery edit screen, then use Record delivery to capture per-line delivered quantities, recipient and delivery-note/proof reference. Partial completion is supported. A proof reference is text; attachment storage and electronic signatures are not included.
- **Returns:** open sales order details → Customer returns. Only delivered, not-yet-returned quantities can be returned. Goods enter quarantine. Physical returns do not automatically issue credit notes or refunds; payment reversal corrects a recorded payment and restores the outstanding balance.
- **Billing:** Record payment adds an individual payment, not a replacement cumulative total. Administrators/managers can reverse an entry with a reason. Payment dates use the server's UTC time. Repeated requests with the same request ID cannot create another payment. Archived invoices still count and cannot be replaced by another invoice for the same order.
- **Reports:** delivered order value is the sum of delivered orders; the sales table retains pending/cancelled statuses for comparison. Purchase value excludes cancelled orders. Archived transactions remain in historical totals. Paid/balance totals reflect payment reversals. These are operational reports, not a general ledger or tax-accounting system.
- **Audit:** administrators/managers can open Audit history. Changes record staff ID, before/after values and reasons where applicable. Password hashes, security stamps and recovery tokens are excluded. Restrict database write access; this application-level audit is not tamper-proof against database administrators.

Writes run in a serializable transaction, with version checks and unique request IDs on payments, purchase receipts, delivery receipts and returns. A conflicting update returns HTTP 409; refresh and inspect the saved record before resubmitting. The payment API requires an anti-forgery token and a `RequestId` alongside amount/method/reference.

## Validation and deployment

```powershell
dotnet test Tests/FoodSupply.Tests.csproj
dotnet build --configuration Release
```

The automated tests use an isolated SQLite database. Also rehearse the migration and concurrent purchase/sale/payment requests on a restored MySQL database before production use; SQLite does not reproduce MySQL locking or DDL behavior. Test email delivery using your own test account after configuring SMTP.

Sample catalog seeding is disabled by default and only runs when the environment is Development and `SeedSampleData=true`. It must never be enabled for the real business database. No database schema is changed by ordinary application startup.

For deployment, retain ASP.NET Core data-protection keys securely across restarts, configure HTTPS and log retention, schedule database backups, and periodically exercise restore procedures. Keep SDK/runtime and NuGet dependencies maintained.
