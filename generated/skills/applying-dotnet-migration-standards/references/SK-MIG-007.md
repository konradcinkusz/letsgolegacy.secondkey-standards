# SK-MIG-007 — Do not switch the globalization mode; flag every result NLS to ICU can change

| Severity | Category | Applies to | Gate diagnostic | Principle |
|---|---|---|---|---|
| error | globalization | `*.csproj`, `*.props`, `*.json`, `Dockerfile` | — | — |

Sections: Rationale · Non-compliant · Compliant · Migration · Flag instead of fixing · References

The migrated system runs with ICU, the modern .NET default, and the migration does not change that
behind anyone's back. It does not set `InvariantGlobalization`, `System.Globalization.UseNls` or
`System.Globalization.AppLocalIcu` — in a project file, `runtimeconfig.template.json` or an
environment variable — and it does not choose a container base image that ships without ICU. Every
behaviour the switch from NLS to ICU can change is recorded as a behaviour flag, not patched.

## Rationale

The globalization library is the one migration difference that no compiler, analyzer or unit test
reports reliably: the code is identical and the results are not. Each switch that changes it is a
legitimate tool with a cost the migration agent is not in a position to judge:

- `InvariantGlobalization=true` removes culture data altogether. Every culture behaves like the
  invariant culture, and since .NET 6 creating any other culture throws `CultureNotFoundException`,
  so sorting, formatting and casing all change at once. The native-AOT project templates enable it,
  and some container images (Alpine, for example) run in invariant mode by default.
- `UseNls=true` restores NLS, but only on Windows: the same build behaves differently on Linux, and
  the difference stays hidden until the platform moves.
- App-local ICU pins one ICU version everywhere. Consistent, but still ICU rather than NLS.

Choosing one is a decision with an owner. Made silently, it hides exactly the kind of difference an
independent verification exists to find.

## Non-compliant

```xml
<!-- Added to make a failing test pass, with no record of why -->
<PropertyGroup>
  <InvariantGlobalization>true</InvariantGlobalization>
</PropertyGroup>
```

```xml
<ItemGroup>
  <RuntimeHostConfigurationOption Include="System.Globalization.UseNls" Value="true" />
</ItemGroup>
```

## Compliant

```xml
<!-- No globalization switch: the migrated system runs with ICU, the .NET default -->
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
</PropertyGroup>
```

The sort that may change is recorded, not patched (`docs/migration/behaviour-flags.md`):

```markdown
| SK-MIG-007 | Catalog/ProductSorter.cs:41 | Products sorted by name with the current culture under NLS | Unchanged; ICU may order names with punctuation differently | Accept ICU order, or pin NLS on Windows |
```

## Migration

- If a test fails because of an ICU result, keep the test as it is (SK-MIG-013) and record the
  difference; do not reach for a switch.
- If the target platform is a container, check that the base image carries ICU; if it does not,
  record it as a decision to make.
- If a person has decided on one of the switches, it is written into the project with a comment
  pointing at the flag that records the decision.

## Flag instead of fixing

- Every linguistic sort, search or comparison over human text (SK-MIG-005), and every culture-data
  dependent format shown to people (SK-MIG-006).
- A failing test whose expected value depends on NLS behaviour.

## References

- .NET documentation, "Globalization and ICU" — the NLS and app-local ICU switches.
- .NET documentation, "Runtime configuration options for globalization".
- .NET breaking change, "Culture creation and case mapping in globalization-invariant mode"
  (.NET 6).

---

Second Key standards 0.1.0, from [`standards/SK-MIG-007-keep-globalization-mode.md`](https://github.com/konradcinkusz/letsgolegacy.secondkey-standards/blob/v0.1.0/standards/SK-MIG-007-keep-globalization-mode.md). Generated; do not edit.
