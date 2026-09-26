# SK-MIG-008 — Resolve dependencies by constructor injection, not static singletons or service locators

| Severity | Category | Applies to | Gate diagnostic | Principle |
|---|---|---|---|---|
| warning | dependency-injection | `*.cs` | — | [P10](https://github.com/konradcinkusz/architecture-standards/blob/main/docs/architecture/00-REFERENCE-ARCHITECTURE.md#p10) |

Sections: Rationale · Non-compliant · Compliant · Migration · Flag instead of fixing · References

Services are registered in `Microsoft.Extensions.DependencyInjection` (`IServiceCollection`) and
received through constructors. The migration adds no static singleton (a static `Instance`
property, a static field holding a service) and no service-locator call
(`DependencyResolver.Current.GetService`, `IServiceProvider.GetService` inside business code, a
container's static `Resolve`). Every registration keeps the lifetime the legacy object had.

## Rationale

A static singleton or a service locator hides a class's dependencies and fixes them for the life of
the process: a reader cannot see them and a test cannot replace them. ASP.NET Core is built on the
built-in container. Every capability the constitution describes is an extension method over
`IServiceCollection`, extension is an interface plus one registration (P10), and the constitution
rules out a custom container. The migration rewrites the application's wiring anyway, and writing
it as registrations costs little more than porting the locator calls one by one.

The part that is not mechanical is lifetime. A per-request object registered as a singleton leaks
one customer's state into another customer's request; a process-wide cache registered as scoped
silently loses what it was holding after every request. Lifetimes are behaviour.

## Non-compliant

```csharp
public class CheckoutController : Controller
{
    public IActionResult Confirm()
    {
        var orders = DependencyResolver.Current.GetService<IOrderService>(); // service locator
        var prices = PriceCache.Instance;                                     // static singleton
        // ...
    }
}
```

## Compliant

```csharp
// The legacy lifetimes, kept: the price cache was process-wide, the order service per request
builder.Services.AddSingleton<IPriceCache, PriceCache>();
builder.Services.AddScoped<IOrderService, OrderService>();

public class CheckoutController(IOrderService orders, IPriceCache prices) : Controller
{
    public IActionResult Confirm()
    {
        // ...
    }
}
```

## Migration

- Map each legacy registration to the same lifetime. For Autofac: `SingleInstance` becomes
  `AddSingleton`, `InstancePerLifetimeScope` and `InstancePerRequest` in a web application become
  `AddScoped`, and `InstancePerDependency` becomes `AddTransient`.
- Property injection, decorators and modules have no one-to-one equivalent in the built-in
  container; keyed registrations do (keyed services, .NET 8 and later). Keeping the legacy
  container for the migration through its ASP.NET Core integration is a decision to record, not a
  default; new registrations still go through `IServiceCollection`.
- A static class that holds no state — a pure function — may stay static.
- Where code cannot take constructor parameters (an attribute, a static extension method), pass in
  what it needs. If that is not possible without changing behaviour, flag it.

## Flag instead of fixing

- A lifetime the built-in container cannot reproduce, or a legacy defect such as a per-request
  object captured by a singleton. Preserve the behaviour and record it.
- Replacing the legacy container, if the migration does it.

## References

- Architecture constitution, P10 "Extensibility through interface + registration, not inheritance",
  and §4 "Deliberate non-goals" (no custom DI container).

---

Second Key standards 0.1.0, from [`standards/SK-MIG-008-constructor-injection.md`](https://github.com/konradcinkusz/letsgolegacy.secondkey-standards/blob/v0.1.0/standards/SK-MIG-008-constructor-injection.md). Generated; do not edit.
