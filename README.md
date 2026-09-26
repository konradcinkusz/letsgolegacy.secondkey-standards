# Second Key — standards pack

Component C4 of [Second Key](https://github.com/konradcinkusz/letsgolegacy.secondkey):
architectural and migration standards written once, delivered to the migration agent as skills and
to the gate as analyzer configuration, from the same source.

> **Status:** phase 01 under construction; see [`docs/WORKPLAN.md`](docs/WORKPLAN.md). The standards,
> their validator, the generator, the skill for GitHub Copilot's modernization agent and the consumer
> drift check are in place. No version has been released (tagged) yet.

## What is here

| Path | What it is |
|---|---|
| [`standards/`](standards/) | The rules: one Markdown file per rule, with metadata. [`standards/README.md`](standards/README.md) defines the format |
| [`catalog/pack.json`](catalog/pack.json) | The pack's version, the skill's and the package's names, and the closed vocabularies the rules are validated against |
| [`catalog/skill.template.md`](catalog/skill.template.md) | The hand-written body of the agent skill; the generator fills in the rule and gate tables |
| [`generated/`](generated/) | **Generated — never edit by hand.** The agent skill, the analyzer configuration, the NuGet package project and the manifest |
| [`src/SecondKey.Standards.Generator`](src/SecondKey.Standards.Generator) | The `secondkey-standards` tool (.NET 10): validates the rules, generates `generated/`, and checks a consumer for drift |
| [`actions/drift-check/`](actions/drift-check/action.yml) | The drift check as one CI step for a consuming repository |
| [`tests/`](tests/) | The tool's tests, including the checks that hold this repository's own rules and generated tree to their contract, and the drift-check fixture consumers |
| [`CHANGELOG.md`](CHANGELOG.md) | What changed in each version |
| [`docs/USING-WITH-MODERNIZE-DOTNET.md`](docs/USING-WITH-MODERNIZE-DOTNET.md) | How to install the skill into a target repository and run GitHub Copilot's modernization agent with it |
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

## What the generator produces

| Output | For | Path |
|---|---|---|
| Agent skill | The migration agent: `SKILL.md` plus one reference file per rule, copyable into a repository's `.github/skills/` ([how](docs/USING-WITH-MODERNIZE-DOTNET.md)) | `generated/skills/applying-dotnet-migration-standards/` |
| `.editorconfig` | The gate: `dotnet_diagnostic.<id>.severity` for every mapped rule, scoped to the files the rule applies to | `generated/config/.editorconfig` |
| `.globalconfig` | The gate: the same severities as a global analyzer config | `generated/config/.globalconfig` |
| NuGet package | A build: `SecondKey.Standards` loads the `.globalconfig` through `build/` props, so one package reference applies every severity | `generated/nuget/` |
| Manifest | Tools: the version, a content digest, and every rule with its own digest | `generated/manifest.json` |

## Working here

Prerequisites: the .NET 10 SDK, and gitleaks or Docker for the pre-commit secret scan.

```sh
./scripts/setup.sh                                                          # prerequisites + pre-commit hook
dotnet test                                                                 # the tests
dotnet run --project src/SecondKey.Standards.Generator -- validate          # check every rule
dotnet run --project src/SecondKey.Standards.Generator -- generate          # regenerate generated/
dotnet run --project src/SecondKey.Standards.Generator -- generate --check  # what CI runs
./scripts/verify-package.sh                                                 # pack the NuGet package and prove a build picks it up
./scripts/scan-secrets.sh                                                   # mirror the CI secret scan
```

Every command exits 0 (pass), 1 (the check ran and failed) or 2 (it could not run). Changing a rule
is: edit the file, run `generate`, commit both. CI fails a pull request whose `generated/` does not
match its sources, and names each stale file.

## Versioning

The version in `catalog/pack.json` is set by hand, and means one thing to somebody who already
pinned an earlier one — **what does upgrading cost me?**

| Bump | When |
|---|---|
| **major** | A rule is withdrawn or reversed, or a severity is lowered: work done to the old rule may have to be redone |
| **minor** | A rule is added, or a severity raised: nothing already built is wrong, there is more to comply with |
| **patch** | Nothing required changes: wording, examples, links |

A released version is a git tag `v<version>`, and its content is frozen: the generator refuses a
change to the generated content of a version that `CHANGELOG.md` dates, until the version is bumped.
While its entry says `Unreleased`, a version has not been tagged and may still change.

**Releasing:** replace `Unreleased` with the date in `CHANGELOG.md`, run `generate`, merge, and tag
`v<version>` on `main`. CI checks on the tag that it matches the pack version and the changelog.

## Consuming the analyzer configuration

The `SecondKey.Standards` package is packed by CI and attached to each run as the
`secondkey-standards-packages` workflow artifact; it is not published to nuget.org. To use it, put the
`.nupkg` in a local or internal feed (or pack it yourself with `./scripts/verify-package.sh`), then:

```xml
<!-- Directory.Packages.props, with central package management: every project gets it -->
<GlobalPackageReference Include="SecondKey.Standards" Version="0.1.0" />
```

Any severity can be overridden in the consuming repository's `.editorconfig`, which always wins, and
`<SecondKeyStandardsGlobalConfig>false</SecondKeyStandardsGlobalConfig>` switches the package off in a
project. Without the package, copy `generated/config/.globalconfig` to the repository root.

## Checking a consuming repository for drift

A consuming repository pins a standards version in the two places it already states one: the
`SecondKey.Standards` package reference (the gate's configuration) and the installed skill's
`metadata.version` (the agent's instructions). One CI step compares the pin with the latest release —
the highest `v<MAJOR.MINOR.PATCH>` tag of this repository — and fails when the repository is behind,
printing the rules that changed and the changelog entries it is missing. It also fails when the skill
and the package pin different versions: then the agent and the gate are working from different
standards.

```yaml
# .github/workflows/standards.yml in the consuming repository
name: Standards drift
on: [pull_request, push]
jobs:
  drift:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
      - uses: konradcinkusz/letsgolegacy.secondkey-standards/actions/drift-check@main
```

Inputs: `working-directory`, `source` (a git URL or local mirror), `package-id`, `skill-name`,
`pinned-version`, `latest-version`; outputs: `status`, `pinned-version`, `latest-version`
([`action.yml`](actions/drift-check/action.yml)). The same check without the action:

```sh
dotnet run --project src/SecondKey.Standards.Generator -- drift-check --repo /path/to/consumer
```

It exits 0 when current, 1 when behind, ahead of every release, disagreeing or unpinned, and 2 when it
cannot check (an unreadable pin, no release yet, an unreachable source). Until the first release is
tagged, it reports "no release". Design and alternatives: [ADR 0005](docs/adr/0005-drift-check.md).

## Dependencies

| Project | Packages | Why |
|---|---|---|
| `SecondKey.Standards.Generator` | 2 — `System.CommandLine`, `YamlDotNet` | Command surface; YAML front matter with line numbers ([ADR 0002](docs/adr/0002-generator-as-a-dotnet-tool.md)) |
| `SecondKey.Standards.Generator.Tests` | 1 — `xunit.v3` | Tests on Microsoft.Testing.Platform |
| `SecondKey.Standards` (generated package) | 0 | Configuration only: no assembly, no dependencies |

Versions are pinned centrally in [`Directory.Packages.props`](Directory.Packages.props).

## Licence

All rights reserved; see [`LICENSE`](LICENSE). The licensing model is an open decision. The
`SK-ARCH-` rules restate principles of
[architecture-standards](https://github.com/konradcinkusz/architecture-standards) (MIT, same
author), and the secret-scanning scripts are ported from it.
