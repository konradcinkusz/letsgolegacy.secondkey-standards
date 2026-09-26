# SK-MIG-011 — Preserve behaviour; flag a suspected defect instead of fixing it

| Severity | Category | Applies to | Gate diagnostic | Principle |
|---|---|---|---|---|
| error | behaviour | `*` | — | [P14](https://github.com/konradcinkusz/architecture-standards/blob/main/docs/architecture/00-REFERENCE-ARCHITECTURE.md#p14) |

Sections: Rationale · Non-compliant · Compliant · Migration · Flag instead of fixing

A migration changes the platform, not the business. Every observable behaviour of the legacy
system — results, rounding, ordering, validation outcomes, error responses and side effects such
as e-mails, audit records and outbound calls — is preserved, including behaviour that looks wrong.
When the migration finds a suspected defect, dead code or an inconsistency, it leaves the logic as
it was, marks the site with a `SECONDKEY-FLAG` comment and records it in the behaviour-flags
register. Deciding to fix it belongs to a person, in a separate change.

## Rationale

A migration is only worth having if the new system does what the old one did, and the only way to
show that is to compare the two. A "fix" made during the migration is indistinguishable, in that
comparison, from a regression: both are a difference nobody asked for. It also merges two reviews —
"did the platform move work?" and "is this business change right?" — into one diff that answers
neither well. Recording the finding instead loses nothing the agent noticed, and the fix can be
reviewed on its own merits once the migrated system is known to be equivalent. Writing the decision
down in the repository, with its reason, is the constitution's rule for decisions (P14).

## Non-compliant

```csharp
// Legacy: percentage discounts are applied in list order, so two of them compound.
// The migration "fixed" it to apply only the largest one: a silent change to every price.
var discount = discounts.Max(d => d.Percentage);
return price * (1 - discount);
```

## Compliant

```csharp
// SECONDKEY-FLAG SK-MIG-011: percentage discounts compound in list order; looks unintended.
foreach (var discount in discounts)
{
    price *= 1 - discount.Percentage;
}

return price;
```

```markdown
| Rule | Location | Legacy behaviour | What the migration did | Decision needed |
|---|---|---|---|---|
| SK-MIG-011 | Pricing/DiscountCalculator.cs:57 | Percentage discounts compound in list order | Preserved unchanged | Is compounding intended? |
```

## Migration

The flag protocol, which every rule's "Flag instead of fixing" section refers to:

1. **At the site.** Put a comment on the line above the preserved code:
   `// SECONDKEY-FLAG <rule-id>: <what differs or looks wrong, in one line>`. In Razor use
   `@* SECONDKEY-FLAG … *@`; in XML and configuration files, `<!-- SECONDKEY-FLAG … -->`.
2. **In the register.** Add one row to `docs/migration/behaviour-flags.md` in the migrated
   repository, creating the file if it does not exist, with the columns shown above: the rule id,
   `path:line`, the legacy behaviour, what the migration did, and the decision a person has to
   make.
3. **When a gate diagnostic is suppressed** because the behaviour must be preserved, the
   suppression names the flag:
   `#pragma warning disable PORTCULLIS_MIG_SYNC_OVER_ASYNC // SECONDKEY-FLAG SK-MIG-003: …`.
4. **In the summary.** The migration's final report and its pull request description end with a
   "Behaviour flags" section: how many there are, grouped by rule, with a link to the register.

What counts as behaviour to preserve:

- Adding a null check, a default value or a `try`/`catch` the legacy code did not have changes
  behaviour: a `NullReferenceException` that produced an HTTP 500 still produces one.
- Rounding stays as it was. `Math.Round` uses banker's rounding (`MidpointRounding.ToEven`) by
  default on both platforms; do not change the mode.
- Side effects keep their triggers and their order: the same e-mail for the same event, the same
  audit record, the same outbound call.
- Compiler warnings about unreachable code or unused values are findings to record, not code to
  delete, when deleting would change what runs.

## Flag instead of fixing

- A suspected defect in business logic, even an obvious one.
- A security weakness in the legacy code. Flag it at the top of the register so a person sees it
  first; fix it inside the migration only if the migration itself would otherwise make it
  exploitable, and say so in the flag.
- Dead code, duplicated rules that disagree, and validation that is inconsistent between two paths.

---

Second Key standards 0.1.0, from [`standards/SK-MIG-011-preserve-behaviour.md`](https://github.com/konradcinkusz/letsgolegacy.secondkey-standards/blob/v0.1.0/standards/SK-MIG-011-preserve-behaviour.md). Generated; do not edit.
