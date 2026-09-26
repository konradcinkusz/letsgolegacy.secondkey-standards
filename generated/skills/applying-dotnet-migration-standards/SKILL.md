---
name: applying-dotnet-migration-standards
description: "Applies the Second Key migration standards while a .NET Framework application is upgraded to .NET 10 or another modern .NET version: preserves observable behaviour and records a behaviour flag instead of fixing a suspected defect; replaces System.Web, HttpContext.Current, ConfigurationManager and blocking on tasks; makes string comparison and culture explicit because .NET Framework uses NLS and modern .NET uses ICU; keeps EF6 query, loading and validation behaviour; keeps routes, JSON shape and cookies; names the diagnostics the Portcullis gate checks. Use when upgrading or migrating a .NET Framework solution to .NET 10, moving ASP.NET MVC or Web API to ASP.NET Core, porting EF6 to EF Core, or moving web.config settings to appsettings.json. Also use when asked to upgrade to .NET 10, migrate from .NET Framework, modernize a solution, or follow the migration standards."
metadata:
  discovery: "preload"
  traits: ".NET|CSharp"
  version: "0.1.0"
  source: "https://github.com/konradcinkusz/letsgolegacy.secondkey-standards"
---

# Applying the Second Key migration standards

This migration will be verified independently. Second Key replays recorded traffic against the
legacy system and against the migrated one, and compares every response, database change and
outbound call against a written contract. A difference introduced on purpose is reported exactly
like a regression. These standards (version 0.1.0, 20 rules) say what to change,
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
- [ ] Before the first task: legacy baseline recorded in docs/migration/behaviour-flags.md
- [ ] Every task: rules applied to the code the task touches
- [ ] After every task: build clean of gate errors, existing tests pass unchanged
- [ ] At the end: flags register complete, summary ends with "Behaviour flags"
```

### Before the first task: record the legacy baseline

Create `docs/migration/behaviour-flags.md` (header in "Flag format" below) and write down, before any code
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

- Every flag has its comment at the site and its row in `docs/migration/behaviour-flags.md`.
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

| Rule | Severity | What migrated code must do | Gate diagnostic |
|---|---|---|---|
| [SK-ARCH-001](references/SK-ARCH-001.md) | error | Treat the existing schema as the contract; migrate it, never EnsureCreated it | `PORTCULLIS_P4_ENSURE_CREATED_OUTSIDE_TEST` |
| [SK-ARCH-002](references/SK-ARCH-002.md) | warning | Move EF6 seed data to a versioned seeding step, not HasData in the model | `PORTCULLIS_P4_SEED_DATA_IN_MODEL` |
| [SK-ARCH-003](references/SK-ARCH-003.md) | error | Move no credential from web.config into a committed file | — |
| [SK-ARCH-004](references/SK-ARCH-004.md) | suggestion | Keep Program.cs a manifest; move Global.asax and App_Start wiring into extension methods | — |
| [SK-ARCH-005](references/SK-ARCH-005.md) | warning | Introduce no DbContext into a controller; record the ones carried over | `PORTCULLIS_P9_CONTROLLER_NO_DBCONTEXT` |
| [SK-ARCH-006](references/SK-ARCH-006.md) | suggestion | Add extension points as interfaces registered in DI, not new base classes | `PORTCULLIS_P10_CUSTOM_BASE_CLASS` |
| [SK-ARCH-007](references/SK-ARCH-007.md) | suggestion | Wire the solution's shared service defaults into the new host; invent none mid-migration | `PORTCULLIS_P15_MISSING_SERVICE_DEFAULTS` |
| [SK-MIG-001](references/SK-MIG-001.md) | error | Replace System.Web hosting types with ASP.NET Core | `PORTCULLIS_MIG_SYSTEM_WEB` |
| [SK-MIG-002](references/SK-MIG-002.md) | error | Pass request data explicitly instead of reading HttpContext.Current | `PORTCULLIS_MIG_HTTPCONTEXT_CURRENT` |
| [SK-MIG-003](references/SK-MIG-003.md) | error | Keep asynchronous code asynchronous all the way; never block on a task | `PORTCULLIS_MIG_SYNC_OVER_ASYNC` |
| [SK-MIG-004](references/SK-MIG-004.md) | error | Read settings through IOptions<T> or IConfiguration, never ConfigurationManager | `PORTCULLIS_MIG_CONFIGURATION_MANAGER` |
| [SK-MIG-005](references/SK-MIG-005.md) | warning | State the StringComparison of every culture-sensitive string comparison and sort | `CA1310` |
| [SK-MIG-006](references/SK-MIG-006.md) | warning | Name the culture for formatting, parsing and casing, and keep the legacy culture source | `CA1304`, `CA1305`, `CA1311` |
| [SK-MIG-007](references/SK-MIG-007.md) | error | Do not switch the globalization mode; flag every result NLS to ICU can change | — |
| [SK-MIG-008](references/SK-MIG-008.md) | warning | Resolve dependencies by constructor injection, not static singletons or service locators | — |
| [SK-MIG-009](references/SK-MIG-009.md) | warning | Create outbound HTTP clients through IHttpClientFactory, never per call | — |
| [SK-MIG-010](references/SK-MIG-010.md) | error | Preserve query, loading and validation behaviour when data access leaves EF6 | — |
| [SK-MIG-011](references/SK-MIG-011.md) | error | Preserve behaviour; flag a suspected defect instead of fixing it | — |
| [SK-MIG-012](references/SK-MIG-012.md) | error | Keep the HTTP contract — routes, status codes, headers, cookies and JSON shape | — |
| [SK-MIG-013](references/SK-MIG-013.md) | error | Keep existing tests and their expectations; a failing test is a finding | — |

**error**: the migrated code must not violate the rule; if it cannot comply without changing
behaviour, flag it and leave the decision to a person. **warning**: fix what the task touches, and
list what remains as flags. **suggestion**: apply when the change is mechanical and
behaviour-neutral.

## What the gate checks

The Portcullis gate and the .NET SDK analyzers report these diagnostics on the migration's pull
request, at these severities. The gate looks at the lines the pull request changes, so code the
migration rewrites is checked even where the legacy code had the same pattern.

| Diagnostic | Severity | Rule |
|---|---|---|
| `PORTCULLIS_P4_ENSURE_CREATED_OUTSIDE_TEST` | error | SK-ARCH-001 |
| `PORTCULLIS_P4_SEED_DATA_IN_MODEL` | warning | SK-ARCH-002 |
| `PORTCULLIS_P9_CONTROLLER_NO_DBCONTEXT` | warning | SK-ARCH-005 |
| `PORTCULLIS_P10_CUSTOM_BASE_CLASS` | suggestion | SK-ARCH-006 |
| `PORTCULLIS_P15_MISSING_SERVICE_DEFAULTS` | suggestion | SK-ARCH-007 |
| `PORTCULLIS_MIG_SYSTEM_WEB` | error | SK-MIG-001 |
| `PORTCULLIS_MIG_HTTPCONTEXT_CURRENT` | error | SK-MIG-002 |
| `PORTCULLIS_MIG_SYNC_OVER_ASYNC` | error | SK-MIG-003 |
| `PORTCULLIS_MIG_CONFIGURATION_MANAGER` | error | SK-MIG-004 |
| `CA1310` | warning | SK-MIG-005 |
| `CA1304` | warning | SK-MIG-006 |
| `CA1305` | warning | SK-MIG-006 |
| `CA1311` | warning | SK-MIG-006 |

## Flag format

At the site — `//` in C#, `@* … *@` in Razor, `<!-- … -->` in XML and configuration files:

```csharp
// SECONDKEY-FLAG SK-MIG-011: percentage discounts compound in list order; looks unintended.
```

In `docs/migration/behaviour-flags.md`, one row per flag (create the file with this header if it is missing):

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
- [ ] `docs/migration/behaviour-flags.md` lists every flag, and the summary ends with the Behaviour flags section.

## When something goes wrong

- **A rule and a task disagree** — a step would add retries, enable invariant globalization, or
  move seed data into `HasData`: the rule wins. Do the task without that part and record the
  conflict as a flag.
- **The build needs a change that alters behaviour**: make the smallest behaviour-preserving
  change; if there is none, flag it, suppress the one diagnostic with the flag, and continue.
- **A test fails for a reason inherent to the platform** (an ICU sort order, an API with no
  equivalent): leave the test failing, add the flag, and say so in the summary.
- **Not sure whether a change alters behaviour**: assume it does, and flag it.

---

Second Key standards 0.1.0, generated from [`standards/`](https://github.com/konradcinkusz/letsgolegacy.secondkey-standards/tree/v0.1.0/standards). Do not edit this copy: take a newer version from the source repository instead.
