# SK-MIG-012 — Keep the HTTP contract — routes, status codes, headers, cookies and JSON shape

| Severity | Category | Applies to | Gate diagnostic | Principle |
|---|---|---|---|---|
| error | behaviour | `*.cs`, `*.cshtml` | — | — |

Sections: Rationale · Non-compliant · Compliant · Migration · Flag instead of fixing · References

Every request the legacy system answered is answered the same way after the migration: the same
URL and HTTP method reach the same action, and the response carries the same status code, redirect
target, content type, headers, cookie names and attributes, and — for JSON — the same property names
and casing, date and number formats, and treatment of nulls and fields.

## Rationale

Browsers, bookmarks, search engines, payment callbacks and other systems depend on the wire format,
not on the code. ASP.NET Core changes several defaults at once:

- JSON output is camelCase by default, while ASP.NET MVC's `JsonResult` and Web API wrote property
  names as declared.
- `System.Text.Json` ignores public fields, which `Newtonsoft.Json` and MVC's JSON serializer
  wrote, and MVC 5's `Controller.Json` wrote dates as `\/Date(…)\/` rather than ISO 8601.
- Cookie names differ: forms authentication used `.ASPXAUTH` and session state `ASP.NET_SessionId`;
  ASP.NET Core's defaults are `.AspNetCore.Cookies` and `.AspNetCore.Session`.

Each of these is invisible in a code review and obvious to a client. Second Key replays recorded
traffic against both systems and compares the responses field by field, so each one is also a
reported difference.

## Non-compliant

```csharp
// Default ASP.NET Core JSON:  {"orderId":7,"totalPrice":10.5}
// The legacy response was:    {"OrderId":7,"TotalPrice":10.5}
builder.Services.AddControllersWithViews();
```

## Compliant

```csharp
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        // Keep property names as declared, as MVC 5 and Web API did
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });
```

```csharp
// Web API endpoints built on Newtonsoft.Json can keep it, with its settings, during the migration
builder.Services.AddControllers().AddNewtonsoftJson();
```

## Migration

- Carry routes over with the same templates, defaults and constraints, in the same order; convention
  routes stay convention routes.
- For each JSON endpoint, compare a legacy response with the migrated one: names, casing, dates,
  numbers, nulls, public fields, enum representation. Configure the serializer, or add a converter,
  until they match.
- Keep status codes and redirects, including the error responses: an unhandled exception that
  produced a 500 with a custom error page still does.
- Keep header and cookie names the clients read. Where a name must change (authentication cookies
  cannot be shared across the two formats), flag it.

## Flag instead of fixing

- A cookie, header or response difference the migration cannot avoid, such as authentication
  cookies that log every user out at cut-over.
- A legacy response that looks wrong: an error returned with status 200, a misspelled property name.
  Keep it and record it (SK-MIG-011).

## References

- ASP.NET Core documentation, "Format response data in ASP.NET Core Web API" — camelCase by default,
  `PropertyNamingPolicy = null` for declared names.
- .NET documentation, "Migrate from Newtonsoft.Json to System.Text.Json" — fields, escaping and
  case sensitivity.

---

Second Key standards 0.1.0, from [`standards/SK-MIG-012-keep-http-contract.md`](https://github.com/konradcinkusz/letsgolegacy.secondkey-standards/blob/v0.1.0/standards/SK-MIG-012-keep-http-contract.md). Generated; do not edit.
