# SK-ARCH-001 — Treat the existing schema as the contract; migrate it, never EnsureCreated it

| Severity | Category | Applies to | Gate diagnostic | Principle |
|---|---|---|---|---|
| error | data-access | `*.cs` | `PORTCULLIS_P4_ENSURE_CREATED_OUTSIDE_TEST` | [P4](https://github.com/konradcinkusz/architecture-standards/blob/main/docs/architecture/00-REFERENCE-ARCHITECTURE.md#p4) |

Contents: Rationale · Non-compliant · Compliant · Migration · Flag instead of fixing · References

The database the legacy system runs on is part of the contract. The migrated system never calls
`Database.EnsureCreated()` outside tests, and never lets a model convention alter the schema: the
EF Core model is mapped to the existing tables, the first EF Core migration describes the existing
schema, and later schema changes are applied as reviewed migrations — with `MigrateAsync` in a
hosted service, or as scripts. EF6 database initializers (`CreateDatabaseIfNotExists`,
`DropCreateDatabaseIfModelChanges`, `MigrateDatabaseToLatestVersion`) are not re-created with
`EnsureCreated`.

This restates principle P4 of the
[architecture constitution](https://github.com/konradcinkusz/architecture-standards/blob/main/docs/architecture/00-REFERENCE-ARCHITECTURE.md#p4)
for a migration; the constitution is the authority.

## Rationale

`EnsureCreated` creates a schema from the current model if the database does not exist and does
nothing if it does: there is no migration history and no upgrade path, so the schema freezes at
first boot. P4 permits it only for the in-memory and test path, after a reference system shipped it
against a real database and froze its production schema. In a migration it fails more quietly
still: pointed at a copy of the legacy database, it does nothing at all, and a mismatch between the
new model and the old schema surfaces later as a failing query.

## Non-compliant

```csharp
// Replacing EF6's CreateDatabaseIfNotExists initializer
using var scope = app.Services.CreateScope();
scope.ServiceProvider.GetRequiredService<ShopDbContext>().Database.EnsureCreated();
```

## Compliant

```csharp
// Schema changes arrive as reviewed migrations, applied after start-up so health probes answer
public sealed class MigrationHostedService(IServiceProvider services) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
        await db.Database.MigrateAsync(stoppingToken);
    }
}
```

## Migration

- Generate the first EF Core migration from a model mapped to the existing schema, and check it
  against that schema: it must describe the tables as they are.
- Record that migration as applied in existing databases (a baseline row in
  `__EFMigrationsHistory`) instead of running it; new environments run it normally.
- Tests that use the in-memory provider or a throwaway database may keep `EnsureCreated`.

## Flag instead of fixing

- A difference between the mapped model and the existing schema that cannot be mapped away: record
  it instead of generating a migration that changes the schema.
- An EF6 initializer that dropped or recreated the database, if any environment relied on that.

## References

- Architecture constitution, P4 "Persistence is provider-portable and its schema is migrated, never
  ensured".
- EF Core documentation, "Porting from EF6 to EF Core" — Code First database initialization.

---

Second Key standards 0.1.0, from [`standards/SK-ARCH-001-migrate-never-ensure-schema.md`](https://github.com/konradcinkusz/letsgolegacy.secondkey-standards/blob/v0.1.0/standards/SK-ARCH-001-migrate-never-ensure-schema.md). Generated; do not edit.
