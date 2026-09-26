---
name: applying-dotnet-migration-standards
description: >-
  Applies the Second Key migration standards while a .NET Framework
  application is upgraded to .NET 10 or another modern .NET version. Preserves
  observable behaviour and records a behaviour flag instead of fixing a
  suspected defect; replaces System.Web, HttpContext.Current,
  ConfigurationManager and blocking on tasks; makes string comparison and
  culture explicit because .NET Framework uses NLS and modern .NET uses ICU;
  keeps EF6 query, loading and validation behaviour; keeps routes, JSON shape
  and cookies; names the diagnostics the Portcullis gate checks. Use for any
  .NET Framework to .NET upgrade, ASP.NET MVC or Web API to ASP.NET Core
  migration, EF6 to EF Core port or web.config to appsettings.json move, and
  whenever asked to upgrade to .NET 10, migrate from .NET Framework, modernize
  a solution, or follow the migration standards.
metadata:
  discovery: "preload"
  traits: ".NET|CSharp"
  version: "0.1.0"
  source: "https://github.com/konradcinkusz/letsgolegacy.secondkey-standards"
---

# Applying the Second Key migration standards

The standards a .NET Framework → .NET 10 migration is held to (version 0.1.0, 20
rules). Read the rule table before changing code, and open a rule's reference file before applying
it: the reference holds the rationale, a non-compliant and a compliant example, and what to flag
instead of fixing.

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

## What the gate checks

The gate reports these diagnostics at these severities:

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

Behaviour flags are recorded in `docs/migration/behaviour-flags.md` (see SK-MIG-011).

---

Second Key standards 0.1.0, generated from [`standards/`](https://github.com/konradcinkusz/letsgolegacy.secondkey-standards/tree/v0.1.0/standards). Do not edit this copy: take a newer version from the source repository instead.
