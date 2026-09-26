# SK-MIG-009 — Create outbound HTTP clients through IHttpClientFactory, never per call

| Severity | Category | Applies to | Gate diagnostic | Principle |
|---|---|---|---|---|
| warning | outbound-http | `*.cs` | — | [P2](https://github.com/konradcinkusz/architecture-standards/blob/main/docs/architecture/00-REFERENCE-ARCHITECTURE.md#p2) |

Contents: Rationale · Non-compliant · Compliant · Migration · Flag instead of fixing · References

Outbound HTTP calls use clients created by `IHttpClientFactory` — typed clients registered with
`AddHttpClient<TClient>()`, or named clients — configured once at registration with the base
address, headers and timeout the legacy client used. No code creates an `HttpClient`, `WebClient`
or `HttpWebRequest` per call. The migration keeps outbound behaviour as it was: it adds no retries,
no circuit breaker and no changed timeout.

## Rationale

An `HttpClient` owns a connection pool. Creating and disposing one per call discards the pool every
time, and under load the closed connections linger in `TIME_WAIT` until the host runs out of ports —
a failure that surfaces as intermittent socket errors far from the code that caused it.
`IHttpClientFactory` pools and recycles the underlying handlers, which also picks up DNS changes,
and turns each client's configuration into a registration a reader can find. `WebClient` and
`HttpWebRequest` are obsolete on modern .NET (SYSLIB0014).

Retries are the trap on the other side. The constitution's end state is a standard resilience
handler on every client (P2), but a retry sends the request again: a `POST` to a payment or
shipping provider that timed out can be executed twice. That changes the outbound contract, which
Second Key checks, so it belongs in its own change with its own review, after the migration.

`ServicePointManager` settings (`SecurityProtocol`, `DefaultConnectionLimit`,
`ServerCertificateValidationCallback`) configured .NET Framework's networking stack. On modern .NET
they do not apply to `HttpClient`, so each one either moves to the client's handler explicitly or
stops having an effect.

## Non-compliant

```csharp
public async Task<Rate[]?> GetRatesAsync(string zip)
{
    // A new connection pool per call
    using var client = new HttpClient { BaseAddress = new Uri("https://rates.example.com/") };
    return await client.GetFromJsonAsync<Rate[]>($"v1/rates/{zip}");
}
```

```csharp
// Retries added during the migration: a label purchase that times out may be bought twice
builder.Services.AddHttpClient<ShippingLabelClient>()
    .AddStandardResilienceHandler();
```

## Compliant

```csharp
builder.Services.AddHttpClient<RatesClient>(client =>
{
    client.BaseAddress = new Uri("https://rates.example.com/");
    client.Timeout = TimeSpan.FromSeconds(30); // the legacy client's timeout, carried over
});

public class RatesClient(HttpClient client)
{
    public Task<Rate[]?> GetRatesAsync(string zip) =>
        client.GetFromJsonAsync<Rate[]>($"v1/rates/{zip}");
}
```

## Migration

- Give each external system one typed client. Carry over its base address, default headers,
  credentials handling and timeout (`HttpWebRequest.Timeout`, `WebClient` subclasses that set it).
- Move each `ServicePointManager` setting the legacy code relied on to the handler
  (`ConfigurePrimaryHttpMessageHandler`), or record that it no longer applies.
- Keep request and response bodies byte-compatible: serializer settings, encoding of query values,
  and header casing as the legacy client produced them.
- Keep the call count: no retry, hedging or circuit-breaker policy in the migration change.

## Flag instead of fixing

- A certificate-validation callback or TLS setting that no longer takes effect.
- A timeout, redirect or decompression default that differs between the legacy client and
  `HttpClient`, when the migration cannot reproduce it exactly.
- Adding resilience, recorded as the follow-up change the constitution asks for.

## References

- .NET documentation, "HttpClient guidelines for .NET" — pooling, DNS and `IHttpClientFactory`.
- Architecture constitution, P2 — the resilience handler as the end state.

---

Second Key standards 0.1.0, from [`standards/SK-MIG-009-httpclient-factory.md`](https://github.com/konradcinkusz/letsgolegacy.secondkey-standards/blob/v0.1.0/standards/SK-MIG-009-httpclient-factory.md). Generated; do not edit.
