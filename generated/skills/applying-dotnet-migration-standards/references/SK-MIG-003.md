# SK-MIG-003 — Keep asynchronous code asynchronous all the way; never block on a task

| Severity | Category | Applies to | Gate diagnostic | Principle |
|---|---|---|---|---|
| error | concurrency | `*.cs` | `PORTCULLIS_MIG_SYNC_OVER_ASYNC` | — |

Contents: Rationale · Non-compliant · Compliant · Migration · Flag instead of fixing · References

No code blocks on asynchronous work with `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`,
`Task.WaitAll` or `Task.WaitAny`. A method that calls an asynchronous API is itself asynchronous,
up to the controller action or endpoint that started the request.

## Rationale

ASP.NET Framework's synchronization context made blocking on a task a deadlock waiting to happen.
ASP.NET Core has no such context, so the deadlock usually disappears — which is exactly why
sync-over-async survives a migration unnoticed. It still holds a thread-pool thread for the whole
of the I/O it waits on, and under load the pool starves: requests queue, latency climbs and health
checks time out, all without an error in the log. Kestrel refuses synchronous request and response
body I/O by default (`AllowSynchronousIO` is `false`) for the same reason.

Blocking also changes what exceptions look like. `.Result` and `.Wait()` wrap a failure in an
`AggregateException`; `await` rethrows the original exception. Converting one into the other
changes which `catch` block runs, so the conversion is a behaviour change to check, not a
mechanical edit.

## Non-compliant

```csharp
public IActionResult Rates(string country)
{
    // Bridging an async-only API by blocking a thread-pool thread
    var rates = _taxApi.GetRatesAsync(country).Result;
    return Json(rates);
}
```

## Compliant

```csharp
public async Task<IActionResult> Rates(string country)
{
    var rates = await _taxApi.GetRatesAsync(country);
    return Json(rates);
}
```

## Migration

- Propagate `async` and `await` up the call chain to the action. Add the `Async` suffix to new
  asynchronous methods; renaming a public API consumed outside the solution is a breaking change
  and is flagged instead.
- Where the legacy chain is synchronous end to end and the API it calls still offers a synchronous
  overload, keeping it synchronous is allowed. Converting it to asynchronous is preferred when the
  migration already touches every method in the chain.
- Review each `catch (AggregateException)` that unwrapped a blocked task: after the conversion the
  inner exception arrives unwrapped. Rewrite the handler so the same failures reach the same
  handling.
- Do not wrap synchronous code in `Task.Run` to make it look asynchronous inside a request, and use
  `async void` only for event handlers.

## Flag instead of fixing

- A chain that cannot become asynchronous without changing a signature consumed outside the
  solution. The bridge stays, suppressed on that line with a justification that cites this rule.
- `AllowSynchronousIO = true`, added to keep a synchronous stream consumer working.
- Any change in which exception type reaches a caller or an error page.

## References

- ASP.NET Core documentation, "Kestrel options" — `AllowSynchronousIO` defaults to `false`.

---

Second Key standards 0.1.0, from [`standards/SK-MIG-003-async-all-the-way.md`](https://github.com/konradcinkusz/letsgolegacy.secondkey-standards/blob/v0.1.0/standards/SK-MIG-003-async-all-the-way.md). Generated; do not edit.
