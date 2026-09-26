# SK-MIG-010 — Preserve query, loading and validation behaviour when data access leaves EF6

| Severity | Category | Applies to | Gate diagnostic | Principle |
|---|---|---|---|---|
| error | data-access | `*.cs` | — | [P4](https://github.com/konradcinkusz/architecture-standards/blob/main/docs/architecture/00-REFERENCE-ARCHITECTURE.md#p4) |

Contents: Rationale · Non-compliant · Compliant · Migration · Flag instead of fixing · References

The platform move keeps data access behaving exactly as it did. EF6 (6.4 and later) runs on modern
.NET, so the default is to move to .NET 10 on EF6 first and port to EF Core as a separate step.
Where the migration does port to EF Core, it reproduces each EF6 behaviour the code relied on:
related data read through lazy loading, table names and column types of the existing schema,
validation EF6 performed on save, and the entity states `Attach` and `Update` assign across an
object graph.

## Rationale

The EF6 → EF Core differences that matter are the ones that compile:

- EF6 lazy-loads `virtual` navigation properties by default. EF Core does not, unless the proxies
  package is added and enabled, so a navigation the code relied on is empty or null — an order with
  no lines, rendered without an error.
- EF6 derives table names by pluralizing the class name; EF Core uses the name of the `DbSet`
  property, or the class name.
- EF6 validates entities against their data annotations on `SaveChanges`. EF Core does not
  validate, so data EF6 rejected now reaches the database.
- EF Core decides whether an entity found through a graph is added or unchanged by whether its
  store-generated key is set, which is not how EF6 did it.
- EF Core maps `DateTime` to `datetime2` by default, while a schema created by EF6 uses `datetime`,
  with a different precision.

Microsoft's porting guidance lists these as behaviour changes that do not show up as compilation
errors, and recommends moving to EF6 on modern .NET before porting to EF Core.

## Non-compliant

```csharp
// EF Core without lazy loading: order.Lines is empty, the total is 0, and nothing fails
var order = await db.Orders.SingleAsync(o => o.Id == id);
var total = order.Lines.Sum(l => l.Quantity * l.UnitPrice);
```

## Compliant

```csharp
// Load exactly what the legacy code read through lazy loading
var order = await db.Orders
    .Include(o => o.Lines)
    .SingleAsync(o => o.Id == id);
var total = order.Lines.Sum(l => l.Quantity * l.UnitPrice);
```

```csharp
// Map to the existing schema instead of letting conventions choose
modelBuilder.Entity<Order>(order =>
{
    order.ToTable("Orders");
    order.Property(o => o.CreatedOnUtc).HasColumnType("datetime");
});
```

## Migration

- Prefer EF6 6.4 or later on `net10.0` for the platform move, and port to EF Core in a later change.
- When porting, inventory every navigation property read without `Include` and load it explicitly,
  or enable lazy-loading proxies (`UseLazyLoadingProxies`) as a recorded decision.
- Map every entity to its existing table and column types explicitly. The first EF Core migration
  must describe the existing schema, not change it (SK-ARCH-001).
- Where the legacy code relied on EF6 rejecting an invalid entity on save, validate before
  `SaveChanges` so the same input is still refused.
- EF Core throws at run time for a query it cannot translate. Rewrite such queries so they return
  the same rows, and compare the generated SQL where the results could differ.

## Flag instead of fixing

- Validation EF6 performed and the migrated code cannot reproduce exactly.
- A query with no `OrderBy` whose rows come back in a different order under the new SQL. The order
  was never guaranteed; record the difference rather than adding an ordering the legacy code never
  had.
- Any change to a column type, table name or key the database already has.

## References

- EF Core documentation, "Porting from EF6 to EF Core" — "Behavior changes between EF6 and EF Core"
  and the recommended order of steps.
- EF Core documentation, "Lazy loading of related data".

---

Second Key standards 0.1.0, from [`standards/SK-MIG-010-preserve-ef-behaviour.md`](https://github.com/konradcinkusz/letsgolegacy.secondkey-standards/blob/v0.1.0/standards/SK-MIG-010-preserve-ef-behaviour.md). Generated; do not edit.
