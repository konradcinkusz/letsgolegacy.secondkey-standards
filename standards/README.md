# Migration standards

The rules a .NET Framework → .NET 10 migration is held to, written once. Each file here is one
rule; the generator compiles the set into the skill the migration agent loads and into the
analyzer configuration the gate enforces, so the agent and the gate cannot disagree about what
a rule says or how much it matters.

This README is documentation, not a rule, and the generator skips it.

## Two kinds of rule

| Prefix | Origin |
|---|---|
| `SK-MIG-` | Migration-specific: the traps of moving a system from .NET Framework to modern .NET — System.Web, ambient state, sync-over-async, configuration, NLS → ICU, dependency injection, outbound HTTP, EF6, and above all behaviour preservation |
| `SK-ARCH-` | A principle of the [architecture constitution](https://github.com/konradcinkusz/architecture-standards/blob/main/docs/architecture/00-REFERENCE-ARCHITECTURE.md) restated for a migration, with a pointer back to it. The constitution stays the authority; the rule says what the principle asks of a migration in particular |

## File format

One file per rule, named `<id>-<slug>.md`, for example `SK-MIG-005-explicit-string-comparison.md`.
The shape, with illustrative values:

```markdown
---
id: SK-MIG-000
title: One imperative line the agent can act on
severity: warning
category: globalization
appliesTo:
  - "*.cs"
portcullisRule: PORTCULLIS_MIG_EXAMPLE   # optional
analyzers:                               # optional
  - CA1310
principle: P5                            # optional
---

The statement: what migrated code must or must not do, in a paragraph or two.

## Rationale
## Non-compliant
## Compliant
## Migration                 (optional)
## Flag instead of fixing    (optional)
## References                (optional)
```

### Front matter

| Key | Required | Meaning |
|---|---|---|
| `id` | yes | Upper-case segments ending in three digits (`SK-MIG-001`). Stable: agents cite it in code comments and the gate's findings are traced back to it |
| `title` | yes | One imperative line, at most 120 characters. It is what the agent sees in the skill's rule table |
| `severity` | yes | `error`, `warning` or `suggestion` — see below |
| `category` | yes | One of the categories in [`catalog/pack.json`](../catalog/pack.json) |
| `appliesTo` | yes | `.editorconfig` file patterns the rule governs (`"*.cs"`, `"*.csproj"`). **Quote them**: an unquoted `*` starts a YAML alias. The generated `.editorconfig` scopes each diagnostic to these patterns |
| `portcullisRule` | no | The Portcullis diagnostic that checks the rule. Must be listed in `catalog/pack.json` (`portcullisRules`), which is the contract with the gate |
| `analyzers` | no | .NET SDK analyzer ids (`CAxxxx`, `IDExxxx`) that check the rule. They ship with the SDK, so the gate can enforce them without new tooling |
| `principle` | no | The constitution principle the rule restates or rests on (`P1`–`P15`) |

A diagnostic may be mapped by one rule only: two rules would give it two severities.

`portcullisRules` in `catalog/pack.json` lists every principle and migration rule the gate ships,
whether or not a standard maps it: it is the vocabulary a rule may choose from. A listed diagnostic
that no rule maps is left out of the generated configuration, so Portcullis's own default severity
applies to it.

### Severity

One vocabulary for the agent and the gate. The names are the `.editorconfig` severities the
mapped diagnostics are set to.

| Severity | For the agent | For the gate |
|---|---|---|
| `error` | Migrated code must not violate the rule. If it cannot comply without changing behaviour, it stops and flags — it never ships the violation silently | The diagnostic is a build error |
| `warning` | Fix what the migration touches; any violation left is recorded as a behaviour flag with its reason | The diagnostic is reported |
| `suggestion` | Apply where the change is mechanical and behaviour-neutral | Reported as a suggestion; never blocking |

Unmapped rules (no `portcullisRule`, no `analyzers`) are enforced by the agent's instructions and,
for behaviour, by Second Key's replay and comparison — not by an analyzer.

### Body

- **Statement** — everything before the first `##`. Normative, short, testable.
- **Rationale** — why. The reason is the part that survives contact with a case the rule did
  not anticipate.
- **Non-compliant** and **Compliant** — at least one fenced code example each. Examples use the
  shapes of a real shop (orders, prices, discounts), because that is where migrations break.
- **Migration** — how to get from the legacy shape to the compliant one without changing behaviour.
- **Flag instead of fixing** — the behaviour differences the agent records instead of resolving.
- **References** — primary sources.

No `#` heading: the title comes from the front matter.

## Flags

A behaviour flag is how the agent reports a difference it must not resolve on its own: a
`SECONDKEY-FLAG <rule-id>: <one line>` comment at the site and a row in the target repository's
`docs/migration/behaviour-flags.md`. The format is defined in
[SK-MIG-011](SK-MIG-011-preserve-behaviour.md), the rule the others refer to.

## Changing a rule

1. Edit or add the file. Keep the statement testable and the examples minimal.
2. `dotnet run --project src/SecondKey.Standards.Generator -- validate` must pass. It reports
   every problem with its file and line; CI runs the same command.
3. Cite the evidence in the pull request: the documentation, the code, or the observed behaviour
   the rule is based on.
