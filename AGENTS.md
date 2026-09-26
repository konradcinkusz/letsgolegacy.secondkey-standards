# AGENTS.md

This repository is Second Key's standards pack (component C4): the rules a .NET Framework → .NET 10
migration is held to, and the tool that checks them and compiles them for the migration agent and
the gate.

## Before you change anything

- The rules are in `standards/`, one file each; `standards/README.md` defines the format and the
  meaning of every field and severity. Read it before editing a rule.
- The architecture principles are **not** re-derived here. `SK-ARCH-` rules restate principles of
  [architecture-standards](https://github.com/konradcinkusz/architecture-standards) and link them; the
  constitution stays the authority.
- Decisions and their reasons are in `docs/adr/`. A change that reverses one updates the ADR.

## Generated output

Nothing under `generated/` is written by hand.

| To change | Edit | Then |
|---|---|---|
| A rule | `standards/<id>-<slug>.md` | `generate` |
| The pack's version, the skill's name or description, the package | `catalog/pack.json` | `generate` |
| The skill's hand-written body | `catalog/skill.template.md` | `generate` |
| What a version changed | `CHANGELOG.md` | — |

```sh
dotnet test
dotnet run --project src/SecondKey.Standards.Generator -- validate
dotnet run --project src/SecondKey.Standards.Generator -- generate          # then commit generated/
dotnet run --project src/SecondKey.Standards.Generator -- generate --check  # what CI runs
```

A content change to a version `CHANGELOG.md` dates is refused until the version is bumped; see the
README's "Versioning" for what each bump means.

## Conventions

- A rule states what migrated code must or must not do, why, and shows a non-compliant and a
  compliant example. Technical claims cite the primary source (Microsoft documentation, the
  constitution) in `## References`. Rule bodies use absolute links only: they are copied into the
  skill's `references/`.
- A Portcullis diagnostic is added to `catalog/pack.json` only once the gate ships it.
- The tool's own code follows the rules it publishes: ordinal string comparison, explicit culture
  (enforced as build errors by `.editorconfig`).
- Public repository: English only, no secrets, no business information.
