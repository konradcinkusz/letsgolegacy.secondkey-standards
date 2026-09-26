# Changelog

What changed in each version of the Second Key standards. A released version is a git tag
`v<version>`, and its content never changes afterwards: the generator refuses a content change to a
version whose entry below carries a release date. A consuming repository pins a released version, and
the drift check prints the entries between its pin and the latest release.

What each kind of version bump means is in the README ("Versioning").

## [0.1.0] — Unreleased

### Added

- Thirteen migration standards (`SK-MIG-001` … `SK-MIG-013`): System.Web, `HttpContext.Current`,
  sync-over-async, `ConfigurationManager`, explicit string comparison and culture, the NLS → ICU
  globalization mode, constructor injection, `IHttpClientFactory`, EF6 behaviour, behaviour
  preservation and the flag protocol, the HTTP contract, and tests as evidence.
- Seven principles of the architecture constitution restated for a migration (`SK-ARCH-001` …
  `SK-ARCH-007`): P4, P5, P9, P10 and P15.
- The generated outputs: the `applying-dotnet-migration-standards` agent skill, `.editorconfig` and
  `.globalconfig` severities for eleven mapped rules, and the `SecondKey.Standards` NuGet package.
- The skill's instructions for GitHub Copilot's modernization agent: the legacy baseline to record
  before the first task, the rules during each task, the gate and the tests after each, the flag
  format, success criteria and error handling.
- The drift check for consuming repositories: `secondkey-standards drift-check` and the
  `actions/drift-check` composite action, which fail when the pinned version is behind the latest
  release (printing the rules and changelog entries it is missing) or when the skill and the package
  pin different versions.
