# SK-MIG-002 — Pass request data explicitly instead of reading HttpContext.Current

| Severity | Category | Applies to | Gate diagnostic | Principle |
|---|---|---|---|---|
| error | hosting | `*.cs` | `PORTCULLIS_MIG_HTTPCONTEXT_CURRENT` | — |

Contents: Rationale · Non-compliant · Compliant · Migration · Flag instead of fixing · References

No code reads the ambient `HttpContext.Current`. Controllers and endpoints use the `HttpContext`
they are given; services receive the values they need — a user id, a store host, a culture — as
parameters. `IHttpContextAccessor` is injected only into edge infrastructure that has no other way
to reach the request, such as a logging enricher, and nothing keeps a reference to `HttpContext`
beyond the request that owns it.

## Rationale

`HttpContext.Current` is a static, ambient dependency: any class, however deep, can reach into the
request, which hides the dependency from readers and makes the class impossible to test without a
web server. ASP.NET Core removes the static. The mechanical replacement — inject
`IHttpContextAccessor` wherever `HttpContext.Current` was — keeps the hidden coupling behind an
interface and adds a failure mode: `HttpContext` is not thread-safe, and reading it outside the
request that owns it (a background task, a continuation that runs after the response completed,
an `async void` method) returns another request's data or throws `NullReferenceException`. The
legacy code had the same hazards under different rules, since ASP.NET Framework tied the context
to a thread, so the migration is the moment to make the data flow explicit.

## Non-compliant

```csharp
public class OrderNumberService
{
    public string NextNumber()
    {
        // Ambient request access from a domain service
        var store = HttpContext.Current.Request.Url.Host;
        return $"{store}-{_sequence.Next()}";
    }
}
```

```csharp
// The mechanical replacement: the same hidden dependency, now also escaping the request
public class AuditService(IHttpContextAccessor accessor, IAuditLog log)
{
    public void RecordLater(string action) =>
        _ = Task.Run(() => log.Write(accessor.HttpContext!.User.Identity!.Name, action));
}
```

## Compliant

```csharp
public class OrderNumberService
{
    public string NextNumber(string storeHost) => $"{storeHost}-{_sequence.Next()}";
}

// The controller reads the request once and passes plain values down
public IActionResult PlaceOrder(OrderForm form)
{
    var number = _orderNumbers.NextNumber(Request.Host.Host);
    // ...
}
```

```csharp
// Copy what background work needs while the request is still in scope
public IActionResult Checkout()
{
    var userName = User.Identity?.Name;
    _queue.Enqueue(() => _audit.Write(userName, "checkout"));
    return RedirectToAction("Complete");
}
```

## Migration

- In controllers, replace `HttpContext.Current` with the controller's `HttpContext`, `Request`,
  `Response` and `User` properties.
- In services, add the value the service actually uses as a parameter, and read it once at the
  edge. Prefer a narrow abstraction (`ICurrentCustomer`) over `IHttpContextAccessor` when many
  services need the same value.
- Register `IHttpContextAccessor` with `builder.Services.AddHttpContextAccessor()` only for the
  infrastructure that needs it.
- Before background work starts, copy the values it needs out of the context.

## Flag instead of fixing

- A value whose meaning changes between the two hosting models. For example, an anonymous user's
  name was an empty string under ASP.NET Framework's default authentication module and is null
  under ASP.NET Core; code that relied on the empty string now throws or branches differently.
- Legacy code that read `HttpContext.Current` from a background thread and relied on it being null
  there.

## References

- ASP.NET Core documentation, "Access HttpContext in ASP.NET Core" — thread safety and background
  work.

---

Second Key standards 0.1.0, from [`standards/SK-MIG-002-no-httpcontext-current.md`](https://github.com/konradcinkusz/letsgolegacy.secondkey-standards/blob/v0.1.0/standards/SK-MIG-002-no-httpcontext-current.md). Generated; do not edit.
