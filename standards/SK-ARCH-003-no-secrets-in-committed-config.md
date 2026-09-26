---
id: SK-ARCH-003
title: Move no credential from web.config into a committed file
severity: error
category: configuration
appliesTo:
  - "*.json"
  - "*.config"
  - "*.cs"
principle: P5
---

No credential moves from `web.config` — a connection-string password, an API key, an SMTP password,
a machine key — into a committed file: `appsettings.json`, `appsettings.<Environment>.json`,
`launchSettings.json`, source code or a comment. Secrets come from the environment: `dotnet
user-secrets` on a developer machine, the platform's secret store in every deployed environment. The
committed file keeps the key, with an empty value.

This restates principle P5 of the
[architecture constitution](https://github.com/konradcinkusz/architecture-standards/blob/main/docs/architecture/00-REFERENCE-ARCHITECTURE.md#p5)
for a migration; the constitution is the authority.

## Rationale

A `web.config` in production was often kept off the repository, encrypted, or rewritten by a
deployment transform. `appsettings.json` is committed, copied into every build output and container
image, and kept forever in history. A migration that copies `<connectionStrings>` into it
mechanically publishes the password to everyone who can read the repository. P5 is explicit that no
secret is ever a literal in a committed configuration file, and that this is enforced by a secret
scanner in CI and in a pre-commit hook, not by review.

## Non-compliant

```json
{
  "ConnectionStrings": {
    "Shop": "Server=sql01;Database=Shop;User Id=shop_app;Password=<copied from web.config>"
  }
}
```

## Compliant

```json
{
  "ConnectionStrings": {
    "Shop": ""
  }
}
```

```sh
# A developer machine: the value lives outside the repository
dotnet user-secrets set "ConnectionStrings:Shop" "<connection string>"

# A deployed environment: an environment variable filled from the secret store
ConnectionStrings__Shop=<from the secret store>
```

## Migration

- For each `<connectionStrings>` entry and each secret `<appSettings>` value, create the key in
  `appsettings.json` with an empty value, and document where the value comes from.
- Keep the name of every key stable: the environment variable an operator sets is derived from it
  (`ConnectionStrings__Shop`).
- If the target repository has no secret scanner, flag it; P5 treats a repository without one as
  containing credentials until proven otherwise.

## Flag instead of fixing

- A credential that is already in the legacy repository's history. It needs rotating, which is a
  person's decision and a person's job; removing it from the file does not undo the disclosure.

## References

- Architecture constitution, P5 "Configuration through the environment; secrets through the
  platform".
