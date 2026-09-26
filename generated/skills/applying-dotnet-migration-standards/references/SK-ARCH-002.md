# SK-ARCH-002 — Move EF6 seed data to a versioned seeding step, not HasData in the model

| Severity | Category | Applies to | Gate diagnostic | Principle |
|---|---|---|---|---|
| warning | data-access | `*.cs` | `PORTCULLIS_P4_SEED_DATA_IN_MODEL` | [P4](https://github.com/konradcinkusz/architecture-standards/blob/main/docs/architecture/00-REFERENCE-ARCHITECTURE.md#p4) |

Contents: Rationale · Non-compliant · Compliant · Migration · Flag instead of fixing · References

Reference data that EF6 seeded — in an initializer's `Seed` method or a migrations configuration's
`Seed` override — moves to a versioned seeding script or an idempotent seeding service that runs
once per environment. It does not move into `HasData` calls in `OnModelCreating`.

This restates principle P4 of the
[architecture constitution](https://github.com/konradcinkusz/architecture-standards/blob/main/docs/architecture/00-REFERENCE-ARCHITECTURE.md#p4)
for a migration; the constitution is the authority.

## Rationale

`HasData` embeds the whole dataset in every migration's model snapshot. The constitution records the
cost: a category taxonomy seeded this way produced migration designer files of about 18,000 lines
each, which made every schema change unreviewable, so schema changes shipped unreviewed. Migrations
describe schema; reference data is data, and is seeded by its own versioned step.

The move also has to keep the legacy seeding semantics. EF6's migrations `Seed` ran every time
migrations were applied and usually used `AddOrUpdate`, so it both inserted missing rows and
overwrote edited ones. Which of those the system relied on is behaviour.

## Non-compliant

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Country>().HasData(
        new Country { Id = 1, TwoLetterIsoCode = "PL", Name = "Poland" },
        new Country { Id = 2, TwoLetterIsoCode = "DE", Name = "Germany" });
}
```

## Compliant

```csharp
// Idempotent: inserts what is missing, run once per environment by the deployment
public sealed class CountrySeeder(ShopDbContext db)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        foreach (var country in ReferenceData.Countries)
        {
            var exists = await db.Countries.AnyAsync(
                c => c.TwoLetterIsoCode == country.TwoLetterIsoCode, cancellationToken);
            if (!exists)
            {
                db.Countries.Add(country);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
```

## Migration

- Move the seed values as they are, with the same keys the legacy data has.
- Decide, from the legacy `Seed` code, whether it only inserted or also overwrote: an
  `AddOrUpdate` that reset operator edits on every deployment is behaviour to keep or to flag.

## Flag instead of fixing

- A legacy `Seed` that overwrote rows operators edit, or that seeded test data into production.

## References

- Architecture constitution, P4 — "Migrations describe schema; reference data is seeded separately".

---

Second Key standards 0.1.0, from [`standards/SK-ARCH-002-seed-data-outside-model.md`](https://github.com/konradcinkusz/letsgolegacy.secondkey-standards/blob/v0.1.0/standards/SK-ARCH-002-seed-data-outside-model.md). Generated; do not edit.
