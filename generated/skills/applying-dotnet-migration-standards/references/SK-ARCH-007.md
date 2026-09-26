# SK-ARCH-007 — Wire the solution's shared service defaults into the new host; invent none mid-migration

| Severity | Category | Applies to | Gate diagnostic | Principle |
|---|---|---|---|---|
| suggestion | observability | `*.cs` | `PORTCULLIS_P15_MISSING_SERVICE_DEFAULTS` | [P15](https://github.com/konradcinkusz/architecture-standards/blob/main/docs/architecture/00-REFERENCE-ARCHITECTURE.md#p15) |

Sections: Rationale · Non-compliant · Compliant · Migration · Flag instead of fixing · References

If the solution already has shared service defaults — an `AddServiceDefaults()` extension that wires
OpenTelemetry, health checks and service discovery — the migrated host calls it, so traces, metrics
and health endpoints exist from the first deployment. If the solution has none, the migration does
not create one: it records the gap for a follow-up change.

This restates principle P15 of the
[architecture constitution](https://github.com/konradcinkusz/architecture-standards/blob/main/docs/architecture/00-REFERENCE-ARCHITECTURE.md#p15)
for a migration; the constitution is the authority.

## Rationale

P15 makes observability a build-time decision rather than an afterthought, and for a migrated system
telemetry is also how an operator compares the old and new systems once both are in production. A
new `Program.cs` that builds a host without the solution's defaults ships a service that is
invisible when it misbehaves. Creating a service-defaults project during the migration is the
opposite mistake: new infrastructure, new dependencies and new endpoints in a change whose claim is
that nothing but the platform moved.

## Non-compliant

```csharp
// The solution has ServiceDefaults; the migrated host never calls it
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
```

## Compliant

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();                 // telemetry, health, discovery (P2, P15)
builder.Services.AddControllersWithViews();

var app = builder.Build();
app.MapDefaultEndpoints();                    // /health and /alive
```

## Migration

- Call the solution's existing defaults from every migrated host.
- Carry over the legacy logging targets and levels as they were; moving to a new logging pipeline is
  a separate change.

## Flag instead of fixing

- A migrated host with no observability wiring because the solution has no shared defaults: record
  it as the follow-up P15 asks for.

## References

- Architecture constitution, P15 "Observability is a build-time decision, not an afterthought", and
  P2a "every service calls AddServiceDefaults()".

---

Second Key standards 0.1.0, from [`standards/SK-ARCH-007-wire-observability.md`](https://github.com/konradcinkusz/letsgolegacy.secondkey-standards/blob/v0.1.0/standards/SK-ARCH-007-wire-observability.md). Generated; do not edit.
