# SK-MIG-013 — Keep existing tests and their expectations; a failing test is a finding

| Severity | Category | Applies to | Gate diagnostic | Principle |
|---|---|---|---|---|
| error | behaviour | `*.cs` | — | [P13](https://github.com/konradcinkusz/architecture-standards/blob/main/docs/architecture/00-REFERENCE-ARCHITECTURE.md#p13) |

Sections: Rationale · Non-compliant · Compliant · Migration · Flag instead of fixing

Existing tests migrate with the code and keep their assertions. A test may change only to follow an
API move — a namespace, a test host, an `async` signature — never in its expected values, its
tolerances, or the inputs it exercises. When a test fails after the migration, the migrated code
changes until the test passes as written. If the difference cannot be removed, the test stays as it
is and the failure is recorded as a behaviour flag; a test is never deleted, skipped or rewritten to
pass. Characterisation tests that pin legacy behaviour are written against the legacy code, before
the move.

## Rationale

Tests are the migration's evidence that behaviour survived. An expectation edited to match the new
output turns that evidence into its opposite: the one signal that caught a difference is the one
that was silenced. The constitution asks for characterisation tests before a move, not after, for
the same reason (P13): a test written after the migration describes the new behaviour, whatever it
turned out to be.

## Non-compliant

```csharp
[Fact]
public void Vat_is_rounded_per_line()
{
    // Was 24.39m before the migration; changed to make the build green
    Assert.Equal(24.40m, _calculator.Vat(order));
}
```

```csharp
[Fact(Skip = "Fails after the upgrade")] // the evidence, switched off
public void Products_are_sorted_by_name()
{
    // ...
}
```

## Compliant

```csharp
// This test failed after the move because EF Core no longer lazy-loads order lines.
// The migrated query now includes the lines (SK-MIG-010); the assertion is untouched.
[Fact]
public async Task Order_total_includes_every_line()
{
    var total = await _orders.TotalAsync(orderId: 7);
    Assert.Equal(129.97m, total);
}
```

## Migration

- Port test projects with the code: the same framework or its current version, the same
  assertions.
- A failing test first means the migrated code is wrong. Find the behaviour that changed and restore
  it.
- Only when the difference is inherent to the platform — an ICU sort order, a removed API with no
  equivalent — leave the test failing, add the flag, and say so in the summary.
- New tests written during the migration pin legacy behaviour, verified against the legacy system;
  they do not assert whatever the migrated system happens to do.

## Flag instead of fixing

- Every test that still fails at the end of the migration, with its cause.
- A test that was already failing or skipped before the migration: keep it as it was and record it.

---

Second Key standards 0.1.0, from [`standards/SK-MIG-013-tests-are-evidence.md`](https://github.com/konradcinkusz/letsgolegacy.secondkey-standards/blob/v0.1.0/standards/SK-MIG-013-tests-are-evidence.md). Generated; do not edit.
