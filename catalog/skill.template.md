# Applying the Second Key migration standards

This migration will be verified independently. Second Key replays recorded traffic against the
legacy system and against the migrated one, and compares every response, database change and
outbound call against a written contract. A difference introduced on purpose is reported exactly
like a regression. These standards (version {{version}}, {{rule-count}} rules) say what to change,
what to preserve, which diagnostics the gate checks, and how to report a behaviour change instead
of making it.

## Two rules above all others

1. **Preserve behaviour.** Change the platform, not the business. Results, rounding, ordering,
   validation, error responses, the HTTP contract (routes, status codes, JSON shape, cookies) and
   side effects (e-mails, audit records, outbound calls) stay exactly as they are — including
   behaviour that looks wrong (SK-MIG-011, SK-MIG-012).
2. **Flag instead of fixing.** When the only way to satisfy a rule or finish a task would change
   behaviour, or when you notice a suspected defect, keep the legacy behaviour and record a
   behaviour flag (format below). Never make the change silently: a person decides, in a separate
   change.

## Workflow

Copy this checklist into the upgrade's progress notes and keep it current:

```
Standards progress:
- [ ] Before the first task: legacy baseline recorded in {{flag-register}}
- [ ] Every task: rules applied to the code the task touches
- [ ] After every task: build clean of gate errors, existing tests pass unchanged
- [ ] At the end: flags register complete, summary ends with "Behaviour flags"
```

### Before the first task: record the legacy baseline

Create `{{flag-register}}` (header in "Flag format" below) and write down, before any code
changes — these facts are gone once the migration rewrites the files that hold them:

- the legacy culture source: `<globalization culture="…">` in `web.config` and its transforms,
  `culture="auto"`, or the IIS host's regional settings (SK-MIG-006);
- every `<appSettings>` key and `<connectionStrings>` entry, per `web.config` transform, marking
  the ones that hold credentials (SK-MIG-004, SK-ARCH-003);
- the data-access stack: the EF6 version, navigation properties read without `Include`, database
  initializers and `Seed` methods (SK-MIG-010, SK-ARCH-001, SK-ARCH-002);
- every JSON endpoint and the serializer behind it (SK-MIG-012);
- the test projects, and any test that already fails or is skipped (SK-MIG-013).

When the upgrade's assessment or plan is written, add these as context so every task sees them.

### In every task

1. Before editing a file, find the rules that apply to it in the table below and read their
   reference files (`references/<rule-id>.md`): each has the rationale, a non-compliant and a
   compliant example, migration steps, and what to flag instead of fixing.
2. Make the change the **Compliant** example shows. Where the situation is one the rule's **Flag
   instead of fixing** section describes, flag instead.
3. Make the smallest change that compiles and preserves behaviour. Do not refactor, rename public
   APIs, reorder logic or tidy code the task does not require; a migration diff that also cleans up
   cannot be verified.

### After every task

1. Build. A diagnostic from the gate table is a finding: fix it the rule's way, or — if the fix
   would change behaviour — suppress that line with a justification that names the flag:
   `#pragma warning disable <DIAGNOSTIC> // SECONDKEY-FLAG <rule-id>: <why>`.
2. Run the existing tests. A failing test means the migrated code changed behaviour: find the
   change and restore the behaviour. Never edit a test's expected values, skip it or delete it
   (SK-MIG-013).

### At the end

- Every flag has its comment at the site and its row in `{{flag-register}}`.
- The final summary, and the pull request description, end with a **Behaviour flags** section: the
  number of flags per rule and a link to the register. Security findings go first.

## Never change silently

- The globalization mode: no `InvariantGlobalization`, `UseNls` or `AppLocalIcu`, and no base image
  without ICU (SK-MIG-007).
- String comparison and culture: make them explicit with the semantics the legacy code had, and flag
  linguistic sorts and comparisons over text shown to people (SK-MIG-005, SK-MIG-006).
- Outbound calls: no retries, circuit breakers, hedging or changed timeouts (SK-MIG-009).
- Data access: lazy loading, table and column mapping, validation on save (SK-MIG-010); the schema,
  never `EnsureCreated` outside tests (SK-ARCH-001).
- Configuration: every key and value carried over, read once through `IOptions<T>` (SK-MIG-004);
  no credential in a committed file (SK-ARCH-003).
- Tests: expected values, tolerances and inputs (SK-MIG-013).

## Rules

{{rule-table}}

**error**: the migrated code must not violate the rule; if it cannot comply without changing
behaviour, flag it and leave the decision to a person. **warning**: fix what the task touches, and
list what remains as flags. **suggestion**: apply when the change is mechanical and
behaviour-neutral.

## What the gate checks

The Portcullis gate and the .NET SDK analyzers report these diagnostics on the migration's pull
request, at these severities. The gate looks at the lines the pull request changes, so code the
migration rewrites is checked even where the legacy code had the same pattern.

{{gate-table}}

## Flag format

At the site — `//` in C#, `@* … *@` in Razor, `<!-- … -->` in XML and configuration files:

```csharp
// SECONDKEY-FLAG SK-MIG-011: percentage discounts compound in list order; looks unintended.
```

In `{{flag-register}}`, one row per flag (create the file with this header if it is missing):

```markdown
# Behaviour flags

Legacy culture source: (the web.config globalization element, or the host's regional settings)

| Rule | Location | Legacy behaviour | What the migration did | Decision needed |
|---|---|---|---|---|
| SK-MIG-011 | Pricing/DiscountCalculator.cs:57 | Percentage discounts compound in list order | Preserved unchanged | Is compounding intended? |
```

## Success criteria

- [ ] The solution builds on .NET 10, and every test that passed before still passes, unchanged.
- [ ] No error-severity diagnostic from the gate table remains unsuppressed, and every suppression
      names a flag.
- [ ] No globalization switch, no new retry policy, no `EnsureCreated` outside tests, and no
      credential in a committed file.
- [ ] `{{flag-register}}` lists every flag, and the summary ends with the Behaviour flags section.

## When something goes wrong

- **A rule and a task disagree** — a step would add retries, enable invariant globalization, or
  move seed data into `HasData`: the rule wins. Do the task without that part and record the
  conflict as a flag.
- **The build needs a change that alters behaviour**: make the smallest behaviour-preserving
  change; if there is none, flag it, suppress the one diagnostic with the flag, and continue.
- **A test fails for a reason inherent to the platform** (an ICU sort order, an API with no
  equivalent): leave the test failing, add the flag, and say so in the summary.
- **Not sure whether a change alters behaviour**: assume it does, and flag it.
