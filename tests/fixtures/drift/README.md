# Drift-check fixtures

Consuming repositories, reduced to the files the drift check reads, for CI's `drift-check` job. Each
is checked by the real composite action (`actions/drift-check`) against a release source built from
the commit under test, whose one release is tagged with the pack's current version.

| Fixture | Pins | Expected |
|---|---|---|
| `stale/` | package and skill at `0.0.1` | fails: behind the latest release, and prints what changed |
| `disagreeing/` | package at `0.0.2`, skill at `0.0.1` | fails: the agent and the gate are told different standards |
| `unpinned/` | nothing (an unrelated package only) | fails: the repository does not pin the standards |

The **current** consumer is not a committed fixture: CI assembles it from this commit's generated
skill and the pack's version — exactly what a consumer does when it upgrades — so it can never go
stale when the version is bumped. `0.0.1` and `0.0.2` stay below every real version, so the committed
fixtures never need updating either.
