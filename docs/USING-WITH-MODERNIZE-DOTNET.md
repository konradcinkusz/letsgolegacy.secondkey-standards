# Using the standards with GitHub Copilot's modernization agent

How a person with GitHub Copilot installs the Second Key migration standards into a target
repository as a custom skill, and runs Microsoft's .NET modernization agent so that it picks the
skill up. This is the procedure for bench ticket **P6** (modernize-dotnet run on nopCommerce with
the standards skill), and it works the same way for any .NET Framework repository.

> **Naming.** Microsoft now documents the agent as **GitHub Copilot upgrade** (the .NET part was
> previously "GitHub Copilot app modernization for .NET", and the plugin the backlog calls
> *modernize-dotnet*). In Visual Studio it is still installed as the **GitHub Copilot app
> modernization** component and invoked as `@Modernize`; elsewhere it is the **Upgrade** agent
> (`@upgrade`). Same agent, same skill mechanism.

**Contents**

1. [What the agent reads](#1-what-the-agent-reads)
2. [Prerequisites](#2-prerequisites)
3. [Install the skill into the target repository](#3-install-the-skill-into-the-target-repository)
4. [Install the agent](#4-install-the-agent)
5. [Run the upgrade so it uses the skill](#5-run-the-upgrade-so-it-uses-the-skill)
6. [Confirm the agent picked the skill up](#6-confirm-the-agent-picked-the-skill-up)
7. [Add the gate configuration to the migrated solution](#7-add-the-gate-configuration-to-the-migrated-solution)
8. [Keep the pin current](#8-keep-the-pin-current)
9. [Troubleshooting](#9-troubleshooting)
10. [What is verified and what is not](#10-what-is-verified-and-what-is-not)

## 1. What the agent reads

The skill is generated in this repository at
[`generated/skills/applying-dotnet-migration-standards/`](../generated/skills/applying-dotnet-migration-standards/):
a `SKILL.md` (front matter plus instructions) and one `references/<rule-id>.md` per rule. It is the
[Agent Skills](https://agentskills.io/specification) format — a directory named after the skill,
containing `SKILL.md` — with the two metadata fields GitHub Copilot upgrade adds:

```yaml
name: applying-dotnet-migration-standards
description: "Applies the Second Key migration standards while a .NET Framework application is upgraded ..."
metadata:
  discovery: "preload"      # GitHub Copilot upgrade: always available, not only on a description match
  traits: ".NET|CSharp"     # GitHub Copilot upgrade: the technologies it applies to
  version: "0.1.0"          # the standards version this copy came from (the drift check reads it)
  source: "https://github.com/konradcinkusz/letsgolegacy.secondkey-standards"
```

GitHub Copilot upgrade reads custom skills from these places, highest priority first:

| Location | Scope |
|---|---|
| `%UserProfile%/.copilot/skills/` | The person's profile, every repository |
| `.github/upgrades/skills/` | The repository, upgrade-specific |
| `.github/skills/` | The repository, shared with the team — **the one used here** |

Everything the skill needs is inside its directory, so copying the directory is the whole install.

## 2. Prerequisites

- A GitHub Copilot subscription (paid or free) on the GitHub account you sign in with. The cloud
  agent (section 4, last option) needs Copilot Business or Enterprise with coding agents enabled.
- The target repository cloned locally, on a branch for the migration. For P6 that is the
  nopCommerce 3.x source the bench pins (the bench downloads it; commit it to a working repository
  first, so the agent's changes are reviewable as a diff).
- Git, and the .NET 10 SDK (Visual Studio Code's extension installs the SDK if it is missing).

## 3. Install the skill into the target repository

Pick the standards version: a release tag `v<version>` once one exists, or `main` before the first
release (write down the commit you took it from).

**Bash**

```sh
STANDARDS_REF=main            # or a release tag, e.g. v0.1.0
SKILL=applying-dotnet-migration-standards

git clone --depth 1 --branch "$STANDARDS_REF" \
  https://github.com/konradcinkusz/letsgolegacy.secondkey-standards.git /tmp/secondkey-standards

cd /path/to/target-repository
mkdir -p .github/skills
rm -rf ".github/skills/$SKILL"
cp -R "/tmp/secondkey-standards/generated/skills/$SKILL" .github/skills/
git add ".github/skills/$SKILL"
git commit -m "Add Second Key migration standards skill ($STANDARDS_REF)"
```

**PowerShell**

```powershell
$StandardsRef = 'main'        # or a release tag, e.g. 'v0.1.0'
$Skill = 'applying-dotnet-migration-standards'
$Source = Join-Path $env:TEMP 'secondkey-standards'

git clone --depth 1 --branch $StandardsRef `
  https://github.com/konradcinkusz/letsgolegacy.secondkey-standards.git $Source

Set-Location C:\path\to\target-repository
New-Item -ItemType Directory -Force .github\skills | Out-Null
Remove-Item -Recurse -Force ".github\skills\$Skill" -ErrorAction SilentlyContinue
Copy-Item -Recurse "$Source\generated\skills\$Skill" .github\skills\
git add ".github/skills/$Skill"
git commit -m "Add Second Key migration standards skill ($StandardsRef)"
```

Check the result: `.github/skills/applying-dotnet-migration-standards/SKILL.md` exists, and its
front matter's `metadata.version` is the version you meant to take. Do not edit the copy; take a
newer version from this repository instead.

## 4. Install the agent

Use one of these. The steps are Microsoft's; see section 10 for the sources.

**Visual Studio (Windows)** — Visual Studio 2026, or Visual Studio 2022 17.14.17 or later. In the
Visual Studio Installer, in the **.NET desktop development** workload, enable the optional components
**GitHub Copilot** and **GitHub Copilot app modernization**. Sign in to Visual Studio with the GitHub
account that has Copilot. Check: right-click a project in Solution Explorer and see **Modernize**.

**Visual Studio Code** — install the **GitHub Copilot** extension and then the **GitHub Copilot
upgrade** extension from the Extensions view. Check: in Copilot Chat, `@upgrade` answers, or the
agent picker lists **Upgrade**.

**GitHub Copilot CLI** — in a Copilot CLI session:

```text
/plugin marketplace add microsoft/upgrade-agent-plugins
/plugin install upgrade-agent@upgrade-agent-plugins
```

Check: `/agent` lists `upgrade-agent`.

**GitHub Copilot app** — add the `microsoft/upgrade-agent-plugins` marketplace under
**Settings → Plugins** and install the **upgrade-agent** plugin. Check: the agent picker lists
**Upgrade**.

**GitHub.com (cloud agent)** — copy `cloud-agent/upgrade.agent.md` from
[microsoft/upgrade-agent-plugins](https://github.com/microsoft/upgrade-agent-plugins) into the target
repository's `.github/agents/`, and copy `cloud-agent/windows/copilot-setup-steps.yml` (for .NET
Framework) to `.github/workflows/copilot-setup-steps.yml`. The Windows setup also requires turning
off the coding agent's firewall (**Settings → Copilot → Coding agent**), which removes its network
restrictions — follow the plugin's README and weigh that before using it. Skills in
`.github/skills/` are available to the cloud agent as well.

## 5. Run the upgrade so it uses the skill

1. Open the target repository (in Visual Studio, open the solution).
2. Start the agent:
   - Visual Studio: right-click the solution → **Modernize**, or type `@Modernize` in Copilot Chat.
   - Visual Studio Code: `@upgrade` in Copilot Chat, or pick **Upgrade** in the agent picker.
   - Copilot CLI: `/agent`, pick **Upgrade** (or start the prompt with `@upgrade`). Run
     `/skills list` first; if the skill is missing, run `/skills reload` (section 6).
   - Copilot app: pick **Upgrade** in the agent picker.
3. Send this as the first prompt, exactly:

   ```text
   Upgrade this solution to .NET 10. Apply the applying-dotnet-migration-standards skill from
   .github/skills to every task: preserve behaviour, and record every behaviour flag in
   docs/migration/behaviour-flags.md instead of fixing it.
   ```

4. Make it permanent for the whole upgrade, so every task and every later session sees it:

   ```text
   From now on, for all tasks in this upgrade, follow the applying-dotnet-migration-standards skill.
   ```

   The agent saves instructions phrased this way to
   `.github/upgrades/<scenarioId>/scenario-instructions.md`, which it loads into every decision.
5. Work through the agent's stages as usual — **assessment** (`assessment.md`), **planning**
   (`plan.md`), **execution** (`tasks.md`) — reviewing each document before telling it to continue.
   At the assessment, check the legacy baseline from the skill is in it (culture source,
   configuration keys, EF6 lazy loading, JSON endpoints, failing tests); if not, say
   "Record the legacy baseline the applying-dotnet-migration-standards skill asks for before planning".
6. When the agent finishes, the P6 acceptance is Microsoft's own: the candidate builds and the
   existing tests pass. The behaviour flags are what the agent did *not* decide; hand them, with the
   candidate, to P7 (replay and compare) and P8 (the gate).

## 6. Confirm the agent picked the skill up

This is the evidence P6 records. At least one of these must hold, and the first two should:

| Check | How | Expected |
|---|---|---|
| The agent knows the skill | Ask: `Which custom skills from .github/skills are you applying?` (Copilot CLI: `/skills info applying-dotnet-migration-standards`) | It names `applying-dotnet-migration-standards` |
| The instruction persisted | Open `.github/upgrades/<scenarioId>/scenario-instructions.md` | It contains the instruction from step 5.4 |
| The baseline was recorded | Open `docs/migration/behaviour-flags.md` | The header and the legacy culture source are there |
| The rules were applied | The first command below; rule ids (`SK-MIG-`) in the assessment, the plan or the task notes | Flags at the sites the register lists |
| No silent switch | The second command below | Nothing new, or each hit flagged |

```sh
git grep -n "SECONDKEY-FLAG"
git grep -n -E "InvariantGlobalization|UseNls|AppLocalIcu|EnsureCreated"
```

Write down for the P6 notes: the tool and its version, the standards version from the skill's
`metadata.version`, whether the skill was picked up on its own or only after the explicit prompt, and
the number of flags per rule.

## 7. Add the gate configuration to the migrated solution

Not needed for the agent to use the skill; needed when the gate runs on the candidate (bench P8).
The `SecondKey.Standards` NuGet package sets the severity of every diagnostic that checks a standard.
It is not on nuget.org: download the `secondkey-standards-packages` artifact from this repository's CI
run for the version you took, or pack it yourself with `./scripts/verify-package.sh` in a clone of
this repository. Then, in the migrated solution:

```sh
mkdir -p .packages && cp /path/to/SecondKey.Standards.0.1.0.nupkg .packages/
```

```xml
<!-- nuget.config at the repository root: a feed relative to this file -->
<configuration>
  <packageSources>
    <add key="secondkey-local" value=".packages" />
  </packageSources>
</configuration>
```

```xml
<!-- Directory.Packages.props (central package management) -->
<GlobalPackageReference Include="SecondKey.Standards" Version="0.1.0" />

<!-- or, without central package management, in Directory.Build.props -->
<ItemGroup>
  <PackageReference Include="SecondKey.Standards" Version="0.1.0" PrivateAssets="all" />
</ItemGroup>
```

Use the same version as the skill: the agent and the gate must be told the same standards.

## 8. Keep the pin current

The skill copy and the package reference are the target repository's pin: both carry the standards
version. Add one step to its CI, and it fails when a newer release exists (printing the rules and
changelog entries it is missing) or when the skill and the package disagree:

```yaml
- uses: konradcinkusz/letsgolegacy.secondkey-standards/actions/drift-check@main
```

To update, take the newer tag, copy the skill again (section 3) and set the package version to match
(section 7). Until the first release of the standards is tagged, the step reports "no release" and
fails — there is nothing to pin yet; for P6 before a release, leave it out.

## 9. Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| The agent does not mention the skill | Not discovered, or not matched to the request | Check the path is `.github/skills/applying-dotnet-migration-standards/SKILL.md`; ask for it by name (step 5.3); restart Visual Studio or VS Code after adding it; in Copilot CLI run `/skills reload` |
| Still not picked up | A different client version reads a different location | Copy the same directory to `.github/upgrades/skills/` as well (upgrade-specific, higher priority), commit, and restart |
| `/skills info` shows no description or an error | The front matter was edited or mangled on copy (line endings, encoding) | Copy the directory again from the tag; do not edit it |
| The agent enables `InvariantGlobalization` or edits a test's expected value | It followed a generic fix instead of the rule | Point it at the rule: `This violates SK-MIG-007 (or SK-MIG-013). Revert it and record a behaviour flag instead.`, and add the rule to the permanent instruction |
| The build fails with a `PORTCULLIS_` or `CA` error | The gate configuration is installed (section 7) and code violates an error-severity rule | Fix it the rule's way, or suppress the line with a `SECONDKEY-FLAG` justification, as the skill describes |

## 10. What is verified and what is not

Verified against Microsoft's and GitHub's current documentation (read on 2026-09-26):

- Custom skills, their locations and priority, and the `name`, `description`, `metadata.discovery`
  and `metadata.traits` fields: *Customize GitHub Copilot upgrade*
  ([learn.microsoft.com/dotnet/core/porting/github-copilot-upgrade/customization](https://learn.microsoft.com/dotnet/core/porting/github-copilot-upgrade/customization);
  source `dotnet/docs`, `docs/core/porting/github-copilot-upgrade/customization.md`, dated 2026-09-21),
  including that instructions phrased "From now on…" are saved to `scenario-instructions.md`.
- Installation and invocation per environment: *Install GitHub Copilot upgrade* (same folder,
  `install.md`, 2026-09-21) and the `microsoft/upgrade-agent-plugins` README.
- The skill directory format (`<name>/SKILL.md`, name and description constraints): the
  [Agent Skills specification](https://agentskills.io/specification); GitHub's *About agent skills*
  (project skills in `.github/skills`, `.claude/skills` or `.agents/skills`); and the skills Microsoft
  ships in `microsoft/upgrade-agent-plugins`, which use the same layout. The name follows Microsoft's
  authoring rule for upgrade skills (start with a gerund: `applying-`).
- Copilot CLI's `/skills list`, `/skills info` and `/skills reload`: GitHub's *Adding agent skills for
  GitHub Copilot CLI*.

Not verified here, and deliberately left to P6, which a person runs with GitHub Copilot:

- **That the agent picks this skill up in a real upgrade run.** Nothing in this repository can run
  GitHub Copilot. The acceptance criterion of R4 — "the agent picks it up in bench ticket P6" — is
  confirmed or refuted by the checks in section 6.
- How `metadata.discovery: preload` behaves for a repository skill in each client. Microsoft documents
  `preload` as "always available"; if a client treats it differently, the explicit prompt in step 5
  still brings the skill in.
- Microsoft's customization page shows a single-file example path (`.github/skills/my-skill.md`) while
  its shipped skills and GitHub's documentation use the `<name>/SKILL.md` directory form used here. If
  P6 finds the directory form is not discovered, the troubleshooting rows above are the first things to
  try, and the finding belongs in this repository as an issue.
