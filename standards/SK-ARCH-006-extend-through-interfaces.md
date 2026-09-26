---
id: SK-ARCH-006
title: Add extension points as interfaces registered in DI, not new base classes
severity: suggestion
category: dependency-injection
appliesTo:
  - "*.cs"
portcullisRule: PORTCULLIS_P10_CUSTOM_BASE_CLASS
principle: P10
---

Shared behaviour the migration needs to add — plumbing every controller used to get from the
framework, a hook several services call — is an interface or a filter registered in DI, not a new
base class. Base classes the legacy code already has are kept as they are: the attributes, filters
and overrides they pass down are behaviour.

This restates principle P10 of the
[architecture constitution](https://github.com/konradcinkusz/architecture-standards/blob/main/docs/architecture/00-REFERENCE-ARCHITECTURE.md#p10)
for a migration; the constitution is the authority.

## Rationale

P10 replaces the "core library base class" with an interface plus one registration: a new algorithm,
provider or step is a class and a line of DI, with no framework to satisfy. Migrations invent base
classes easily — somewhere to hold the services that used to come from a static, a place for helpers
that used to live on `HttpContext` — and each one becomes a hierarchy every later controller has to
fit into. The opposite mistake is as costly: flattening a legacy base controller changes which
filters and attributes apply to every action that inherited them.

## Non-compliant

```csharp
// A base class invented during the migration to share plumbing
public abstract class ShopControllerBase(IWorkContext work, IStoreContext store) : Controller
{
    protected Customer CurrentCustomer => work.CurrentCustomer;
}
```

## Compliant

```csharp
// Shared behaviour as a filter registered once; controllers declare what they use
builder.Services.AddControllersWithViews(options => options.Filters.Add<StoreClosedFilter>());

public class CatalogController(IWorkContext work) : Controller
{
    // ...
}
```

## Migration

- Keep legacy base classes and their attributes; port them as they are.
- Put new shared behaviour in filters, middleware or small injected services.

## Flag instead of fixing

- A legacy base class whose behaviour ASP.NET Core cannot reproduce through inheritance, such as a
  base controller that relied on an `OnActionExecuting` ordering that changed.

## References

- Architecture constitution, P10 "Extensibility through interface + registration, not inheritance".
