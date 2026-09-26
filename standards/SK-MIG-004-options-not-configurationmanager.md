---
id: SK-MIG-004
title: Read settings through IOptions<T> or IConfiguration, never ConfigurationManager
severity: error
category: configuration
appliesTo:
  - "*.cs"
portcullisRule: PORTCULLIS_MIG_CONFIGURATION_MANAGER
principle: P5
---

Settings are bound to options classes (`IOptions<T>`) from `IConfiguration`, which reads
`appsettings.json`, environment variables and the platform's secret store. No code reads
`System.Configuration.ConfigurationManager.AppSettings` or `ConfigurationManager.ConnectionStrings`.
Every key, default and value the legacy system had in `web.config` or `app.config` has an explicit
counterpart, and each value means what it meant before.

## Rationale

The `System.Configuration.ConfigurationManager` package makes the legacy calls compile on modern
.NET, and that is what makes it dangerous: ASP.NET Core does not read `web.config`, so
`ConfigurationManager.AppSettings["Shipping.Surcharge"]` quietly returns `null` at run time and the
code falls through to whatever default it had. The build is green, the tests that stub
configuration pass, and the system runs with different settings. Binding to options classes makes
the shape of the configuration explicit — one class per section, validated at start-up — and is
the configuration model the constitution prescribes: hierarchical keys bound to options classes,
delivered through the environment (P5).

## Non-compliant

```csharp
public class ShippingCalculator
{
    // Compiles with the compatibility package, returns null on ASP.NET Core, and the surcharge
    // silently becomes 0
    public decimal Surcharge =>
        decimal.TryParse(ConfigurationManager.AppSettings["Shipping.Surcharge"], out var value)
            ? value
            : 0m;
}
```

## Compliant

```csharp
public sealed class ShippingOptions
{
    public const string Section = "Shipping";

    public decimal Surcharge { get; init; }
}

// Registration, next to the service that uses it
builder.Services.AddOptions<ShippingOptions>()
    .Bind(builder.Configuration.GetSection(ShippingOptions.Section))
    .ValidateOnStart();

public class ShippingCalculator(IOptions<ShippingOptions> options)
{
    public decimal Surcharge => options.Value.Surcharge;
}
```

The value moves from `<add key="Shipping.Surcharge" value="4.99" />` in `web.config` to
`appsettings.json`:

```json
{
  "Shipping": {
    "Surcharge": 4.99
  }
}
```

## Migration

- Inventory every `<appSettings>` key and `<connectionStrings>` entry, including the values each
  `web.config` transform sets per environment, and map each one to a configuration key. Keep the
  mapping table in the migration notes: operators set these values by name.
- The configuration binder converts values with the invariant culture. Legacy code often parsed
  with the server's culture (`decimal.Parse` with no provider). Carry numeric and date values over
  in invariant format, and check each one parses to the same value.
- Use `IOptions<T>`: it is read once, which matches `web.config`, whose edits restarted the
  application. `IOptionsMonitor<T>` and `IOptionsSnapshot<T>` introduce live reload, which is a
  behaviour change.
- Read connection strings from the `ConnectionStrings` section with `GetConnectionString`. A
  credential never moves into a committed file (SK-ARCH-003).
- Keep each legacy default exactly. Use `ValidateOnStart` only for values the legacy system could
  not run without.

## Flag instead of fixing

- A key that the legacy code read but no `web.config` defined, or that was defined but never read.
- A value whose parsed result differs under the invariant culture.
- Any move to live reload.

## References

- ASP.NET Core documentation, "Migrate configuration to ASP.NET Core" (`migration/fx-to-core`).
- .NET documentation, "Options pattern in .NET".
