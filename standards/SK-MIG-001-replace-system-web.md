---
id: SK-MIG-001
title: Replace System.Web hosting types with ASP.NET Core
severity: error
category: hosting
appliesTo:
  - "*.cs"
portcullisRule: PORTCULLIS_MIG_SYSTEM_WEB
---

Migrated code does not depend on the ASP.NET Framework hosting model: no reference to the
`System.Web` assembly and no use of the `System.Web.*` types that exist to run inside IIS's
ASP.NET pipeline — `HttpApplication`, `HttpContext` and `HttpContextBase`, `HttpRequest`,
`HttpResponse`, `IHttpModule`, `IHttpHandler`, `System.Web.Mvc`, `System.Web.Http`,
`System.Web.Optimization`, `System.Web.Security`, `System.Web.SessionState`. Requests are
handled with ASP.NET Core: middleware, controllers or endpoints, and
`Microsoft.AspNetCore.Http.HttpContext`.

## Rationale

`System.Web.dll` is not part of modern .NET, so a project that targets `net10.0` cannot reference
it. Code that still names these types either stops compiling or compiles against the
`Microsoft.AspNetCore.SystemWebAdapters` compatibility layer, which re-implements part of the
`System.Web` surface on top of ASP.NET Core. The adapters are a legitimate bridge for an
incremental, side-by-side migration, but they differ from the original in documented details —
request buffering, response buffering and session state are opt-in per endpoint, and
`HttpContext.Current` no longer has thread affinity — so every call routed through them is a place
where legacy and migrated behaviour can diverge. The migration is finished only when the hosting
model is ASP.NET Core's own, and the gate reports what is left.

One `System.Web` type is outside this rule: `System.Web.HttpUtility` ships with modern .NET, and
its encoders produce different bytes from the alternatives, as the examples show.

## Non-compliant

```csharp
// Global.asax.cs: an ASP.NET Framework pipeline event carried into the migrated code
public class ShopApplication : System.Web.HttpApplication
{
    protected void Application_BeginRequest()
    {
        HttpContext.Current.Response.AddHeader("X-Shop-Node", Environment.MachineName);
    }
}
```

```csharp
// "Modernising" an encoder during the migration. HttpUtility produced "%2fcart%3fid%3d7";
// WebUtility produces "%2Fcart%3Fid%3D7" — different bytes in every redirect URL.
var returnUrl = WebUtility.UrlEncode("/cart?id=7");
```

## Compliant

```csharp
// Program.cs: the same header, set by middleware before the response starts
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Shop-Node"] = Environment.MachineName;
    await next(context);
});
```

```csharp
// System.Web.HttpUtility is part of modern .NET, so the output stays byte-identical
var returnUrl = System.Web.HttpUtility.UrlEncode("/cart?id=7"); // "%2fcart%3fid%3d7"
```

## Migration

- Map each pipeline event (`Application_BeginRequest`, `Application_EndRequest`, handlers of an
  `IHttpModule`) to middleware, registered in the order the events ran. Map each `IHttpHandler`
  and `.ashx` file to an endpoint.
- Move MVC and Web API controllers to ASP.NET Core controllers, keeping routes and action names
  exactly as they were (SK-MIG-012).
- If the System.Web adapters are used as a bridge, record every adapter-dependent area as a
  behaviour flag, with the step that will remove it.
- Keep `HttpUtility` calls as they are. If the gate reports one, suppress that line with a
  justification that cites this rule, rather than switching encoder.

## Flag instead of fixing

- Request validation. ASP.NET Framework rejected requests containing markup ("A potentially
  dangerous Request.Form value was detected"); ASP.NET Core has no equivalent, so the migrated
  system accepts input the legacy system refused. Record it; do not add ad-hoc filtering to
  imitate it.
- Any encoding, pipeline-order or buffering behaviour the migrated code cannot reproduce exactly.

## References

- ASP.NET Core documentation, "Migrate from ASP.NET Framework to ASP.NET Core" — the
  `migration/fx-to-core` section, including the System.Web adapters and the `HttpContext` area
  (buffering and thread affinity).
