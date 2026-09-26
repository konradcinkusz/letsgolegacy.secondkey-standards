## Ticket

<!-- The ticket id from docs/WORKPLAN.md and its "done when" line, verbatim. -->

## What changes, and why

<!-- The rule, generator behaviour or workflow that changed, and the failure it prevents.
     A rule here is stated with its reason; a PR that changes one should say why too. -->

## Standards version

<!-- A change to what consumers receive needs a version decision (see README "Versioning").
     Delete the lines that do not apply. -->

- [ ] **major** — a rule is reversed or withdrawn, or a severity is lowered
- [ ] **minor** — a rule is added, or a severity is raised
- [ ] **patch** — nothing required changes (wording, examples, links)
- [ ] No standards content changed

## How it was verified

- [ ] `dotnet test` passes
- [ ] The generator's checks pass (`validate`, and `generate --check` once the generator exists)
- [ ] `./scripts/scan-secrets.sh` is clean
