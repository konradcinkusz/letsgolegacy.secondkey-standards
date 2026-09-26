# ADR 0004 — The standards as a custom skill for GitHub Copilot's modernization agent

- **Status:** accepted (R4); pickup in a real run to be confirmed by bench ticket P6
- **Date:** 2026-09-26

## Context

The migration agent in Second Key's demo is Microsoft's modernization agent for .NET — the backlog's
*modernize-dotnet*, now documented as **GitHub Copilot upgrade** (Visual Studio still installs it as
"GitHub Copilot app modernization" and invokes it as `@Modernize`). It accepts custom skills that
encode a team's standards. R4 packages the standards as such a skill; P6 runs the agent with it.

The mechanism was checked against the current sources rather than assumed:

| Fact | Source (read 2026-09-26) |
|---|---|
| A skill is a Markdown file with front matter `name`, `description`, and optional `metadata.discovery` (`preload`, `lazy`, `scenario`) and `metadata.traits` | `dotnet/docs`: `docs/core/porting/github-copilot-upgrade/customization.md` (2026-09-21) |
| Locations and priority: `%UserProfile%/.copilot/skills/` > `.github/upgrades/skills/` > `.github/skills/` > built-in | same |
| Instructions phrased "From now on…" are saved to `.github/upgrades/<scenarioId>/scenario-instructions.md`, loaded into every decision | same |
| Microsoft's shipped skills are `<name>/SKILL.md` directories with reference files; names start with a gerund; descriptions ≤ 1024 characters, third person, no XML tags; `SKILL.md` under 500 lines | `microsoft/upgrade-agent-plugins` (its `creating-skills` skill and its validation rules) |
| Project skills live in `.github/skills`, `.claude/skills` or `.agents/skills`, one directory per skill, file named `SKILL.md`; skills work in the cloud agent, the CLI, the Copilot app and VS Code | `github/docs`: *About agent skills*, *Adding agent skills* |
| The directory format and the name/description constraints | Agent Skills specification (`agentskills/agentskills`) |

## Decisions

1. **An Agent Skills directory, generated, copyable as is.** `generated/skills/<name>/` holds
   `SKILL.md` and `references/<rule-id>.md`; every link inside is relative to the directory or
   absolute, and a test asserts it. Installing it is copying one directory into the target
   repository's `.github/skills/`. The skill is **not** placed in this repository's own
   `.github/skills/`: this repository is not being migrated, and a skill there would be loaded by
   every agent working on the standards themselves.

2. **The name is `applying-dotnet-migration-standards`.** The ticket's example was
   `dotnet-migration-standards`; Microsoft's authoring rules for upgrade skills ask for a leading
   gerund, and the name is the directory, the `/skill` command and the routing key, so it follows the
   agent's convention.

3. **`metadata.discovery: preload`.** The standards are cross-cutting: every task of a migration
   touches behaviour preservation, and a lazily loaded skill is only loaded when a task's wording
   matches its description. Microsoft recommends `lazy` for most custom skills to save context; this
   skill keeps the always-loaded part to about 200 lines and puts each rule's detail in a reference
   file loaded on demand, which is the progressive disclosure the recommendation is protecting.

4. **The skill carries its version** (`metadata.version`, `metadata.source`). A copied skill is
   otherwise anonymous; with the version in it, the drift check (C4-c) can tell a consumer whether
   the instructions its agent follows are current, and whether they agree with the gate
   configuration it builds with.

5. **The body is hand-written, the tables are generated.** `catalog/skill.template.md` holds the
   instructions — the two overriding rules, the workflow mapped onto the agent's stages (baseline
   before the first task; rules during each task; build, gate and tests after each; flags at the
   end), what never changes silently, the flag format, success criteria and error handling, which is
   the structure Microsoft's authoring rules ask for. The rule table and the gate table are generated
   from the rules, so they cannot drift. The template must include both, and may use only known
   placeholders.

6. **The flag protocol appears twice, and a test keeps the two identical.** SK-MIG-011 defines it
   (the rules are the source); the skill repeats the format because the agent needs it in the part
   it always has loaded. A test asserts both name the same marker, register path and register
   columns.

7. **Belt and braces in P6.** The how-to (`docs/USING-WITH-MODERNIZE-DOTNET.md`) has the person name
   the skill in the first prompt and make it a permanent instruction, so the run does not depend on
   automatic discovery alone.

8. **The description is one double-quoted line, with a "Use when…" clause.** YAML parsers read a
   folded block just as well, but line-based checks in skill tooling see only the first line of a
   block scalar. Reference files over 100 lines open with a `Contents:` line, as Microsoft's authoring
   rules ask.

9. **Checked once against Microsoft's own skill validator, not in CI.** The `validate_skill.sh` script
   in `microsoft/upgrade-agent-plugins` reports 0 failures and two warnings, both heuristics that do
   not apply: its list of gerund prefixes does not include `applying-`, and the `\0` in a C# string
   in SK-MIG-005 looks to it like a Windows path. It is not wired into CI because that repository is
   distributed under Microsoft pre-release licence terms that limit use to Microsoft's products; the
   constraints it checks that matter (name, description length and content, `SKILL.md` under 500
   lines, references one level deep) are enforced by this repository's own generator and tests.

## Consequences

- R4's acceptance — "the agent picks it up in bench ticket P6" — cannot be verified in this
  repository: nothing here can run GitHub Copilot. The how-to lists the checks a person runs, and what
  to record, to confirm it.
- Two points are recorded as open until P6: how `preload` behaves for a repository skill in each
  client, and a discrepancy in Microsoft's page, whose example path is a single file
  (`.github/skills/my-skill.md`) while its shipped skills and GitHub's documentation use the
  `<name>/SKILL.md` directory form used here. The how-to's troubleshooting table says what to try
  first if the directory form is not discovered.
