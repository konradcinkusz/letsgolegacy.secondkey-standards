# Second Key — standards pack

Component C4 of [Second Key](https://github.com/konradcinkusz/letsgolegacy.secondkey):
architectural and migration standards written once, delivered to the migration agent as skills and
to the gate as analyzer configuration, from the same source.

> **Status:** phase 01 under construction; see [`docs/WORKPLAN.md`](docs/WORKPLAN.md). The migration
> standards and their validator are in place; compiling them into skills, analyzer configuration
> and a NuGet package, and the consumer drift check, arrive in the following work items.

## What is here

| Path | What it is |
|---|---|
| [`standards/`](standards/) | The rules: one Markdown file per rule, with metadata. [`standards/README.md`](standards/README.md) defines the format |
| [`catalog/pack.json`](catalog/pack.json) | The closed vocabularies the rules are validated against: categories and the Portcullis diagnostics the gate ships |
| [`src/SecondKey.Standards.Generator`](src/SecondKey.Standards.Generator) | The `secondkey-standards` tool (.NET 10): validates the rules |
| [`tests/`](tests/) | Its tests, including the check that holds this repository's own rules to their contract |
| [`docs/adr/`](docs/adr/) | Decisions and the reasons for them |

## The standards

Twenty rules for a .NET Framework → .NET 10 migration. Thirteen are migration-specific (`SK-MIG-`):
no System.Web, no `HttpContext.Current`, async all the way, `IOptions<T>` instead of
`ConfigurationManager`, explicit string comparison and culture, the NLS → ICU switch, constructor
injection, `IHttpClientFactory`, EF6 → EF Core behaviour, the HTTP contract, tests as evidence — and
the one the others lean on: **preserve behaviour, and flag a suspected defect instead of fixing
it** ([SK-MIG-011](standards/SK-MIG-011-preserve-behaviour.md)). Seven restate principles of the
[architecture constitution](https://github.com/konradcinkusz/architecture-standards/blob/main/docs/architecture/00-REFERENCE-ARCHITECTURE.md)
for a migration (`SK-ARCH-`), each linking the principle it restates.

Eleven rules are mapped to a diagnostic the gate can check: the four Portcullis migration rules,
five Portcullis architecture rules, and the .NET SDK's globalization analyzers.

## Working here

Prerequisites: the .NET 10 SDK, and gitleaks or Docker for the pre-commit secret scan.

```sh
./scripts/setup.sh                                                   # prerequisites + pre-commit hook
dotnet test                                                          # the tests
dotnet run --project src/SecondKey.Standards.Generator -- validate   # check every rule
./scripts/scan-secrets.sh                                            # mirror the CI secret scan
```

`validate` reports every problem with its file and line, and exits 0 (valid), 1 (problems found) or
2 (could not run). CI runs it on every pull request.

## Dependencies

| Project | Packages | Why |
|---|---|---|
| `SecondKey.Standards.Generator` | 2 — `System.CommandLine`, `YamlDotNet` | Command surface; YAML front matter with line numbers ([ADR 0002](docs/adr/0002-generator-as-a-dotnet-tool.md)) |
| `SecondKey.Standards.Generator.Tests` | 1 — `xunit.v3` | Tests on Microsoft.Testing.Platform |

Versions are pinned centrally in [`Directory.Packages.props`](Directory.Packages.props).

## Licence

All rights reserved; see [`LICENSE`](LICENSE). The licensing model is an open decision. The
`SK-ARCH-` rules restate principles of
[architecture-standards](https://github.com/konradcinkusz/architecture-standards) (MIT, same
author), and the secret-scanning scripts are ported from it.
