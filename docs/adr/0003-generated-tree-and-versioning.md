# ADR 0003 — The generated tree, the staleness check, the version gate and the NuGet package

- **Status:** accepted (C4-b)
- **Date:** 2026-09-26

## Context

The rules are compiled into everything that has to agree about them: the skill the migration agent
loads, the analyzer configuration the gate reads, and a NuGet package that delivers that
configuration to a build. The compiled output has to be reviewable in a pull request, impossible to
let drift from its source, and versioned so a consuming repository can pin it (C4-c).

## Decisions

1. **The output is committed, under `generated/`, and never edited by hand.** A reviewer sees what
   a rule change does to the skill and the gate configuration in the same diff; a consumer can copy
   the skill from a tag without running anything. This follows architecture-standards, whose
   packaging layer is generated and checked the same way.

   | Path | What it is |
   |---|---|
   | `generated/skills/<skill>/SKILL.md`, `references/<id>.md` | The Agent Skills directory |
   | `generated/config/.editorconfig` | `dotnet_diagnostic.<id>.severity` per mapped rule, in sections scoped to the rule's `appliesTo` |
   | `generated/config/.globalconfig` | The same severities as a global analyzer config |
   | `generated/nuget/` | The `SecondKey.Standards` package project, its `build/` props and readme |
   | `generated/manifest.json` | Version, content digest, and every rule with its own digest |

2. **`generate --check` builds the whole tree in memory and compares it byte for byte** with the
   disk: missing, stale and orphaned files are each named. `generate` and `generate --check` share
   one code path, so they cannot disagree about what the output is. The generator owns
   `generated/` entirely — a file it no longer produces is removed — except `bin/` and `obj/`,
   the git-ignored build output of packing the package project. CI runs `--check`, and a unit test
   runs the same comparison, so `dotnet test` catches a stale tree locally too.

3. **The version is set by hand and the build refuses to let it be forgotten** — the house rule of
   architecture-standards, adapted to releases being git tags rather than every merge. The
   manifest's content digest is computed with the version replaced by a fixed value, so it changes
   when content changes and only then. If the committed manifest has the same version, a
   different digest, and `CHANGELOG.md` dates that version, the run fails — in a plain run as well
   as under `--check`, before anything is written, so a regeneration cannot launder the change.
   A version whose changelog entry says `Unreleased` has not been tagged, so nobody can have pinned
   it, and its content may still change. Versions never go down, and every version needs a
   changelog entry, which is what the drift check prints to a consumer who is behind.

4. **What a bump means** (README, "Versioning"): **major** — a rule is withdrawn or reversed, or a
   severity is lowered; **minor** — a rule is added or a severity raised; **patch** — wording,
   examples and links only. The number cannot be derived from a diff: only a person can say
   whether a rewritten paragraph reverses a rule.

5. **The package is configuration only, and loads through `build/` props.** `SecondKey.Standards`
   ships no assembly (`IncludeBuildOutput=false`), is a development dependency (it never reaches a
   consumer's runtime dependencies), and its `build/SecondKey.Standards.props` adds the global
   config to the compiler's `EditorConfigFiles` item — the item the .NET SDK uses for its own
   analysis-level configs. A consumer gets every severity by adding one package reference, and can
   switch it off with `SecondKeyStandardsGlobalConfig=false`.

6. **`global_level = 0`.** The SDK's analysis-level configs use negative levels; a repository's own
   `.globalconfig` defaults to 100; any `.editorconfig` entry beats every global config. Level 0
   puts the standards above the SDK's defaults and below anything the consumer sets. Verified with
   a consumer build: the package's `CA1310 = warning` applies; a consumer `.globalconfig` setting
   `none` wins with no conflict warning; a consumer `.editorconfig` setting `error` wins over both.

7. **The package is packed and verified in CI, not published.** The `package` job packs it,
   builds a throwaway consumer against it (without the package CA1310 is silent; with it CA1310 is a
   warning; with the opt-out it is silent again), and uploads the `.nupkg` files as a workflow
   artifact. `scripts/verify-package.sh` is the same job, runnable locally.

8. **A tag must be a release.** On a `v*` tag, CI checks that the tag equals `v` + the pack
   version, that `CHANGELOG.md` dates that version, and that the committed manifest is at it — the
   drift check trusts tags, so tags are guarded.

## Consequences

- Changing a rule is: edit the file, run `generate`, commit both. Forgetting the second step fails
  CI with the list of stale files.
- Releasing is: date the version in `CHANGELOG.md`, merge, tag `v<version>` on `main`. After that,
  any content change needs a new version.
- The Portcullis diagnostics are configured before the analyzers exist in a consumer's build; an
  `.editorconfig` severity for an unknown diagnostic id is inert, so the configuration is safe to
  adopt ahead of the gate.

## Alternatives considered

- **Generate in CI only, commit nothing.** Nothing to go stale, but no reviewable diff of what a
  rule change does, and no way to copy a skill from a tag without building it.
- **Derive the version from the diff.** A tool that guessed would eventually publish a major as a
  patch, which is worse than not versioning at all.
- **Ship the configuration as an `.editorconfig` in the package.** NuGet cannot place an
  `.editorconfig` in a consumer's tree; a global config added through build props is the mechanism
  MSBuild supports.
