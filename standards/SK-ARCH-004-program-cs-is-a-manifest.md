---
id: SK-ARCH-004
title: Keep Program.cs a manifest; move Global.asax and App_Start wiring into extension methods
severity: suggestion
category: layering
appliesTo:
  - "*.cs"
principle: P9
---

The new `Program.cs` reads as a list of capabilities. Wiring that lived in `Global.asax`
(`Application_Start`), in `App_Start/*Config.cs` (routes, filters, bundles), in a `Startup` class or
in a container module moves into extension methods owned by the feature it wires —
`builder.Services.AddCatalog()`, `app.MapShopRoutes()` — keeping the order in which registrations
and middleware ran.

This restates principle P9 of the
[architecture constitution](https://github.com/konradcinkusz/architecture-standards/blob/main/docs/architecture/00-REFERENCE-ARCHITECTURE.md#p9)
for a migration; the constitution is the authority.

## Rationale

A migration produces the application's wiring all at once. Landing it inline makes `Program.cs` the
several-hundred-line file P9 warns about — the constitution's worked example compares a 130-line
manifest with a 399-line inline equivalent, and the second is the harder one to change. Named
extension methods cost nothing extra while the wiring is being rewritten anyway, and they make the
one thing that is behaviour here — order — visible in a few lines.

## Non-compliant

```csharp
// Program.cs: every registration, route and filter inline
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
// ... 120 more registrations ...
app.MapControllerRoute("product", "p/{productId}/{seName}",
    new { controller = "Product", action = "ProductDetails" });
// ... 60 more routes ...
```

## Compliant

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCatalog();   // was App_Start plus a container module
builder.Services.AddCheckout();

var app = builder.Build();
app.UseShopPipeline();           // middleware in the order the Global.asax events ran
app.MapShopRoutes();             // RouteConfig.RegisterRoutes: same templates, same order
app.Run();
```

## Migration

- One extension method per feature or legacy configuration class, in the project that owns it.
- Keep registration order where it matters (the last registration of a service wins) and middleware
  order exactly.

## Flag instead of fixing

- Legacy wiring whose order cannot be reproduced, such as a module relying on the order in which a
  container scanned assemblies.

## References

- Architecture constitution, P9 "Program.cs is a manifest; wiring lives in extension methods".
