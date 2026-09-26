# SecondKey.Standards

Analyzer configuration for the Second Key migration standards: sets the severity of every Portcullis and .NET SDK diagnostic that checks a standard, through a global analyzer config that the package's build props add to every build.

Second Key standards version 0.1.0. The rules themselves, and the skill that tells the migration agent about them, are in <https://github.com/konradcinkusz/letsgolegacy.secondkey-standards>.

## Install

With central package management, in `Directory.Packages.props`:

```xml
<GlobalPackageReference Include="SecondKey.Standards" Version="0.1.0" />
```

Or in each project (or a `Directory.Build.props`):

```xml
<PackageReference Include="SecondKey.Standards" Version="0.1.0" PrivateAssets="all" />
```

The diagnostics are reported by the analyzers that own them: Portcullis for the `PORTCULLIS_` ids, the .NET SDK for the `CA` ids. This package sets their severity; it does not add analyzers.

## Severities

| Diagnostic | Severity | Standard |
|---|---|---|
| `PORTCULLIS_P4_ENSURE_CREATED_OUTSIDE_TEST` | error | SK-ARCH-001 Treat the existing schema as the contract; migrate it, never EnsureCreated it |
| `PORTCULLIS_P4_SEED_DATA_IN_MODEL` | warning | SK-ARCH-002 Move EF6 seed data to a versioned seeding step, not HasData in the model |
| `PORTCULLIS_P9_CONTROLLER_NO_DBCONTEXT` | warning | SK-ARCH-005 Introduce no DbContext into a controller; record the ones carried over |
| `PORTCULLIS_P10_CUSTOM_BASE_CLASS` | suggestion | SK-ARCH-006 Add extension points as interfaces registered in DI, not new base classes |
| `PORTCULLIS_P15_MISSING_SERVICE_DEFAULTS` | suggestion | SK-ARCH-007 Wire the solution's shared service defaults into the new host; invent none mid-migration |
| `PORTCULLIS_MIG_SYSTEM_WEB` | error | SK-MIG-001 Replace System.Web hosting types with ASP.NET Core |
| `PORTCULLIS_MIG_HTTPCONTEXT_CURRENT` | error | SK-MIG-002 Pass request data explicitly instead of reading HttpContext.Current |
| `PORTCULLIS_MIG_SYNC_OVER_ASYNC` | error | SK-MIG-003 Keep asynchronous code asynchronous all the way; never block on a task |
| `PORTCULLIS_MIG_CONFIGURATION_MANAGER` | error | SK-MIG-004 Read settings through IOptions<T> or IConfiguration, never ConfigurationManager |
| `CA1310` | warning | SK-MIG-005 State the StringComparison of every culture-sensitive string comparison and sort |
| `CA1304` | warning | SK-MIG-006 Name the culture for formatting, parsing and casing, and keep the legacy culture source |
| `CA1305` | warning | SK-MIG-006 Name the culture for formatting, parsing and casing, and keep the legacy culture source |
| `CA1311` | warning | SK-MIG-006 Name the culture for formatting, parsing and casing, and keep the legacy culture source |

## Overriding

An entry in the repository's `.editorconfig` always wins:

```ini
[*.cs]
dotnet_diagnostic.CA1305.severity = suggestion
```

Set `<SecondKeyStandardsGlobalConfig>false</SecondKeyStandardsGlobalConfig>` in a project to switch the package's configuration off there.
