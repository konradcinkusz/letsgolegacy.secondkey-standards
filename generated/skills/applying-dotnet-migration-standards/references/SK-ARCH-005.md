# SK-ARCH-005 — Introduce no DbContext into a controller; record the ones carried over

| Severity | Category | Applies to | Gate diagnostic | Principle |
|---|---|---|---|---|
| warning | layering | `*.cs` | `PORTCULLIS_P9_CONTROLLER_NO_DBCONTEXT` | [P9](https://github.com/konradcinkusz/architecture-standards/blob/main/docs/architecture/00-REFERENCE-ARCHITECTURE.md#p9) |

Contents: Rationale · Non-compliant · Compliant · Migration · Flag instead of fixing · References

Controllers stay transport: they bind, authorize and delegate. The migration does not introduce a
`DbContext` into a controller that did not use one. Where a legacy controller already used the data
context directly, the migration keeps the data access where it was — moving it into a service is a
refactoring for a separate change — and records the gate's finding as debt carried over.

This restates principle P9 of the
[architecture constitution](https://github.com/konradcinkusz/architecture-standards/blob/main/docs/architecture/00-REFERENCE-ARCHITECTURE.md#p9)
for a migration; the constitution is the authority.

## Rationale

P9 layers a service as controllers over orchestrators over domain services over data access, and a
controller holding a `DbContext` collapses those layers into one class. A migration is tempted to
do exactly that — injecting the context is the shortest path from `new ShopContext()` — and equally
tempted to "fix" the layering while it is in there. Both are wrong for the same reason: the first
adds debt, the second mixes a refactoring into a change whose whole claim is that behaviour did not
move. The gate reports the pattern on changed lines, so carried-over instances surface in review;
recording them keeps the debt visible without doing the refactoring now.

## Non-compliant

```csharp
// The legacy controller called ICategoryService; the migration "simplified" it to the context
public class CategoryController(ShopDbContext db) : Controller
{
    public IActionResult Index() => View(db.Categories.ToList());
}
```

## Compliant

```csharp
public class CategoryController(ICategoryService categories) : Controller
{
    public IActionResult Index() => View(categories.GetAll());
}
```

```csharp
// The legacy controller created the context itself; the migration injects it and leaves the
// queries where they were
// SECONDKEY-FLAG SK-ARCH-005: controller reads ShopDbContext directly (carried over, not refactored)
public class ReportController(ShopDbContext db) : Controller
{
    // ...
}
```

## Migration

- Keep every controller's collaborators what they were. A controller that called a service calls
  the same service.
- A legacy controller that created a context with `new` gets it injected with the same lifetime per
  request, and a flag.

## Flag instead of fixing

- Every controller that still holds a `DbContext` after the migration.

## References

- Architecture constitution, P9 — the layering of controllers, orchestrators, domain services and
  repositories.

---

Second Key standards 0.1.0, from [`standards/SK-ARCH-005-controllers-stay-transport.md`](https://github.com/konradcinkusz/letsgolegacy.secondkey-standards/blob/v0.1.0/standards/SK-ARCH-005-controllers-stay-transport.md). Generated; do not edit.
