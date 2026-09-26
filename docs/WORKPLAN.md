# Work plan — `letsgolegacy.secondkey-standards` (C4 standards pack)

One Markdown source of standards, with rule metadata, compiled into everything that
has to agree about them: an Agent Skills catalog the migration agent loads, an
analyzer configuration package the gate reads, and an `.editorconfig`. Drift between
what the agent was told and what the gate enforces is a CI failure, not a finding.

The content is derived from
[architecture-standards](https://github.com/konradcinkusz/architecture-standards) (the
estate constitution and guides) plus standards specific to .NET Framework → modern
.NET migration. The drift check absorbs the idea of `context-pin` — a standards
version pinned per repository — without a separate service: a NuGet package, a git
tag and one CI step.

Ticket IDs match the cross-repository backlog.

## Phase 01 — demo

| ID | Deliverable | Done when | Status |
|---|---|---|---|
| R4 | architecture-standards packaged as a custom skill for Microsoft's modernization agent (modernize-dotnet) | The agent picks it up in bench ticket P6 (P6 is run by a person with GitHub Copilot) | in review (#3) |
| C4-a | Migration standards written as Markdown with rule metadata (ids mapped to Portcullis diagnostics) | Every rule has an id, a severity, a rationale, a compliant and a non-compliant example | in review (#1) |
| C4-b | Generator: Markdown → skills directory + `.editorconfig` / `.globalconfig` + NuGet package | `--check` mode in CI fails when generated output is stale | in review (#2) |
| C4-c | Drift check as one CI step for a consuming repository (pinned version vs published version) | A fixture repository with a stale pin fails the step, a current one passes | in review (#4) |

## Phase 02 and later

| Work | Phase |
|---|---|
| Customer standards delivered as skills (one source per estate) | 02 |
| Release channels of standards through NuGet / git tags, per-repo pinning at partners | 02–03 |
