# ADR 0002 — The generator is a .NET 10 tool, and it validates from the first pull request

- **Status:** accepted (C4-a)
- **Date:** 2026-09-26

## Context

The standards need a program: to validate them now, to compile them into skills, analyzer
configuration and a NuGet package (C4-b), and to check a consumer's pinned version (C4-c). The
estate's other generator (`architecture-standards/scripts/build-marketplace.mjs`) is a Node
script. Second Key's product principle 3 is "only technologies the buyer already runs": .NET, SQL
Server, Entra ID, GitHub or Azure DevOps.

## Decisions

1. **A .NET 10 console application, packable as a dotnet tool** (`secondkey-standards`). The people
   who run it already have the .NET SDK; nothing else is needed. The same binary runs in this
   repository's CI and, for the drift check, in a consumer's.

2. **Two dependencies, both declared** (`Directory.Packages.props`, and the README's dependency
   table):
   - `YamlDotNet` for front matter. A hand-rolled YAML subset would be small, but YAML's edge cases
     (quoting, aliases, duplicate keys) are exactly where a hand-rolled parser misreads a rule
     silently; the library is a single assembly with no dependencies, and its node model gives the
     line numbers the error messages need.
   - `System.CommandLine` (2.0, stable) for the command surface: help, parsing and errors for three
     commands without hand-written argument handling.
   Markdown sections are parsed by hand: the grammar a rule needs is headings and fenced code
   blocks, and the one subtlety — a `## heading` inside a code fence is not a heading — is handled
   by tracking fences explicitly. A Markdown library would be a larger dependency for less control.

3. **The validator lands in C4-a, with the standards.** The C4-a acceptance criterion — every rule
   has an id, a severity, a rationale, a compliant and a non-compliant example — is enforced by CI
   in the same pull request that introduces the rules, rather than by review until C4-b. C4-b adds
   emission and `--check` to the same tool.

4. **Problems are collected, not thrown.** One run reports every broken rule with its file and line;
   inside GitHub Actions each problem is also emitted as an annotation on the diff.

5. **Exit codes are shared by every command**: 0 success, 1 the check ran and failed, 2 the command
   could not run. A pipeline can tell a failed check from a broken invocation.

6. **Tests use xUnit v3 on Microsoft.Testing.Platform**, opted into through `global.json`, which is
   what xUnit v3 requires on the .NET 10 SDK's `dotnet test`.

7. **The repository holds itself to its own globalization rules.** `.editorconfig` raises CA1304,
   CA1305, CA1307, CA1309, CA1310 and CA1311 to warnings, and `TreatWarningsAsErrors` makes them
   build errors: the generator's own string handling is ordinal and culture-explicit.

## Consequences

- `dotnet run --project src/SecondKey.Standards.Generator -- <command>` works from a clone with only
  the SDK installed; `dotnet pack` produces the tool package.
- Every command takes its streams, working directory and environment as parameters, so the tests
  run the real command surface in-process against throwaway repositories.
