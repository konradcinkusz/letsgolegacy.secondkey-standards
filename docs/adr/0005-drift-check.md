# ADR 0005 — The drift check: one CI step, no service

- **Status:** accepted (C4-c)
- **Date:** 2026-09-26

## Context

A consuming repository adopts a standards version and then falls behind as the standards move. The
owner's `context-pin` repository solved this with a service: a manifest API, a lock file synced from
it, and a GitHub Action that compared the two. C4-c keeps the idea — a version pinned per repository,
and CI that fails when the pin is behind — without the service: a NuGet package, a git tag and one CI
step.

## Decisions

1. **The pin is read where the consumer already states it.** A consumer has two artifacts from this
   repository, and both carry the version: the `SecondKey.Standards` package reference (the gate's
   configuration — `PackageVersion`, `GlobalPackageReference` or `PackageReference` in any MSBuild
   file, or `packages.config`) and the installed skill's `metadata.version` (the agent's
   instructions). The check reads both; there is no third file to keep in step. The ticket's example
   location, `.secondkey/standards.lock`, was also rejected for a concrete reason: `.secondkey/` is
   git-ignored in the estate's repositories (it is Second Key's run-output directory), including the
   nopCommerce bench, so a pin there would never be committed. `--pinned-version` covers a consumer
   that pins some other way.

2. **Disagreeing pins fail, even when one of them is current.** If the skill says 0.2.0 and the
   package says 0.1.0, the migration agent and the gate are working from different standards — the
   exact drift this component exists to prevent.

3. **The latest release is the highest `v<MAJOR.MINOR.PATCH>` tag** of the standards repository,
   listed with `git ls-remote`. Pre-release and non-version tags are ignored. The generator's version
   gate and CI's release guard (ADR 0003) make a tag trustworthy: a tagged version's content never
   changes. `--latest-version` covers an offline or mirrored setup; `--source` takes any git URL or a
   local path, which is also how the tests and the CI fixtures run offline.

4. **"What changed" is printed in two halves.** The mechanical half diffs the two releases'
   `generated/manifest.json`: rules added, removed, or changed — spelling out a severity change,
   because that is what decides whether upgrading costs work. The human half prints the
   `CHANGELOG.md` entries between the pin and the latest release. Both are read from the tags with a
   shallow `git fetch`, so they are exactly what those releases shipped.

5. **One implementation, two entry points.** The logic is the tool's `drift-check` command. The
   composite action (`actions/drift-check/action.yml`) installs a .NET 10 SDK only if the runner has
   none, and runs the command from its own checkout (so this repository's `global.json`, not the
   consumer's, picks the SDK). Inputs reach the script through the environment, never by expression
   interpolation into the script text. Inside GitHub Actions the result is also an error annotation, a
   step summary, and step outputs (`status`, `pinned-version`, `latest-version`).

6. **Exit codes separate a finding from a failure to check.** Behind, ahead, disagreeing or unpinned
   is exit 1: the check ran and found a problem. An unreadable pin, a source with no release, or an
   unreachable source is exit 2. Both fail the step — a check that cannot run must not look like a
   check that passed.

7. **Fixtures that cannot go stale.** CI checks committed fixtures pinned at `0.0.1`/`0.0.2` — below
   every real version, so they are behind forever — and a *current* consumer assembled from the
   commit under test, exactly as a consumer upgrades. A release source is built from the same commit
   and tagged with the pack's version. Nothing in the fixtures has to change when the version does.

## Consequences

- Until the first release is tagged, the check against the real repository reports "no release" (exit
  2). That is correct: there is nothing to pin yet.
- A consumer that references the package through an MSBuild property (`Version="$(StandardsVersion)"`)
  must pass `--pinned-version`; the check does not evaluate MSBuild.
- The action builds the tool on each run (tens of seconds). A consumer that runs it often can install
  the packed tool (`dotnet tool install SecondKey.Standards.Generator`) from its own feed instead and
  run `secondkey-standards drift-check`.

## Alternatives considered

- **context-pin's service and lock file.** A server to run and secure, and a lock that has to be synced
  — two new moving parts for a question git tags already answer.
- **Comparing against the NuGet feed's latest version.** The package is not published to nuget.org,
  and a consumer's internal feed may lag; tags are the one place every release exists.
- **A bash implementation in the action.** Two implementations of the same check would drift; the
  action and the command share one.
