# ADR 0001 — The standards source format

- **Status:** accepted (C4-a)
- **Date:** 2026-09-26

## Context

C4 compiles one source of standards into two consumers that must not disagree: the skill the
migration agent loads, and the analyzer configuration the gate (Portcullis, C5) enforces. The
source therefore has to carry, per rule, both prose for the agent and metadata for the gate, in a
form a person can review in a pull request and a program can check.

## Decisions

1. **One Markdown file per rule, with YAML front matter** (`standards/<id>-<slug>.md`). A rule is
   the unit of review, of versioning and of citation; one file each makes a diff about one rule
   read as one. Front matter keeps the metadata next to the prose it describes.

2. **Ids are `PREFIX-NNN`** — `SK-MIG-001` for migration-specific rules, `SK-ARCH-001` for
   principles of the architecture constitution restated for a migration. Numbers keep an id stable
   when its title is reworded; the prefix says where the rule comes from. The validator accepts any
   upper-case prefix, so customer standards (phase 02) can use their own.

3. **One severity vocabulary for agent and gate**: `error`, `warning`, `suggestion` — the
   `.editorconfig` names, with meanings stated for both sides in `standards/README.md`. A rule
   cannot mean "must" to the agent and "may" to the gate.

4. **Closed vocabularies live in `catalog/pack.json`**: the categories, and the Portcullis
   diagnostic ids a rule may map to. The Portcullis list is the contract with C5: a rule naming an
   id the gate does not ship fails validation instead of silently mapping to nothing. The four
   migration ids are fixed by the cross-repository backlog (`PORTCULLIS_MIG_SYSTEM_WEB`,
   `PORTCULLIS_MIG_HTTPCONTEXT_CURRENT`, `PORTCULLIS_MIG_SYNC_OVER_ASYNC`,
   `PORTCULLIS_MIG_CONFIGURATION_MANAGER`); the principle ids come from Portcullis's existing rule
   registry.

5. **`portcullisRule` is single-valued; a diagnostic belongs to one rule.** Where one principle
   has two Portcullis rules (P4), it is two standards (SK-ARCH-001, SK-ARCH-002). A diagnostic
   mapped twice would need two severities, so the validator rejects it.

6. **An `analyzers` field, beyond the ticket's list.** The ticket names `portcullisRule` as the
   mapping; the globalization rules — the migration trap the demo is built around — have no
   Portcullis rule, but the .NET SDK ships analyzers for them (CA1310, CA1304, CA1305, CA1311),
   disabled by default. Mapping them from the same source puts the NLS → ICU rules into the gate's
   configuration with no new tooling (product principle 3), so the agent and the gate agree on
   them too. The field accepts only SDK ids (`CAxxxx`, `IDExxxx`); Portcullis ids must use
   `portcullisRule`.

7. **`appliesTo` is a list of `.editorconfig` file patterns.** It is both documentation for the
   agent and the section scope of the generated `.editorconfig`. Patterns must be quoted in YAML
   (an unquoted `*` starts an alias); the validator's error message says so.

8. **The body has a fixed set of sections**: a statement, then `Rationale`, `Non-compliant` and
   `Compliant` (required; each example section needs a non-empty fenced code block), and optional
   `Migration`, `Flag instead of fixing`, `References`. A closed set catches a misspelled heading
   that would otherwise drop out of every generated artifact. No `#` heading: the title comes from
   the front matter, stated once.

9. **The flag protocol is defined in a rule, SK-MIG-011**, not in the tooling. "Flag instead of
   fixing" is the central behaviour the standards ask for, so its format — a `SECONDKEY-FLAG
   <rule-id>:` comment and a row in the target repository's `docs/migration/behaviour-flags.md` —
   is versioned with the rules and reaches the agent through the same channel.

10. **Architecture rules restate, they do not replace.** Each `SK-ARCH-` rule says what one
    principle asks of a migration and links the principle's anchor in the constitution
    (`00-REFERENCE-ARCHITECTURE.md#p4`); a test asserts the link. The principles chosen are the
    ones a .NET Framework migration touches: P4 (schema, seed data), P5 (secrets), P9 (Program.cs,
    controller layering), P10 (extension through DI) and P15 (observability). P2, P3, P6–P8 and
    P11–P14 are either outside a migration's reach or already carried by a migration rule's
    `principle` (P2 in SK-MIG-009, P13 in SK-MIG-013, P14 in SK-MIG-011).

## Severities chosen, and why

| Rule | Severity | Reason |
|---|---|---|
| System.Web, HttpContext.Current, sync-over-async, ConfigurationManager | error | Each either fails at run time in a way no test sees (a `null` setting, a starved thread pool) or leaves the migration unfinished |
| String comparison, culture (CA1310, CA1304, CA1305, CA1311) | warning | Making a call explicit is mechanical and behaviour-neutral, but a legacy code base has many call sites; reported everywhere, fixed where the migration touches code |
| Globalization mode, EF behaviour, preserve behaviour, HTTP contract, tests | error | These are the behaviour the migration exists to preserve; no analyzer checks them, so the agent's instruction is the control and replay is the verification |
| DI, IHttpClientFactory, carried-over DbContext in controllers, seed data | warning | Debt to remove, but not at the cost of changing behaviour in the migration change |
| Program.cs manifest, base classes, observability wiring | suggestion | Structure, not behaviour |

## Consequences

- The validator (`secondkey-standards validate`) checks all of the above and reports every problem
  with its file and line; CI runs it on every pull request. The C4-a acceptance criterion is a test
  (`RepositoryStandardsTests`), not a reviewer's checklist.
- Adding a Portcullis diagnostic to a rule is a two-file change (`catalog/pack.json` and the rule),
  made only once the gate ships the diagnostic.
- Rules are written for two readers at once: a person reviewing the standard and an agent applying
  it. Examples use the shapes of a shop (orders, prices, discounts) because that is where the demo
  migration's behaviour lives.

## Alternatives considered

- **One large document with anchors.** Easier to read end to end, but a rule could not be versioned,
  validated or cited on its own, and the generator would have to parse structure out of prose.
- **Metadata in a separate JSON or YAML catalog, prose in Markdown.** Two files per rule that can
  disagree; the front matter keeps them in one.
- **Numeric-only severities or a separate gate severity.** Rejected: two vocabularies are exactly the
  drift this component exists to prevent.
