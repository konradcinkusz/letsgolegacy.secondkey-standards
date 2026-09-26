# SK-MIG-006 — Name the culture for formatting, parsing and casing, and keep the legacy culture source

| Severity | Category | Applies to | Gate diagnostic | Principle |
|---|---|---|---|---|
| warning | globalization | `*.cs` | `CA1304`, `CA1305`, `CA1311` | — |

Contents: Rationale · Non-compliant · Compliant · Migration · Flag instead of fixing · References

Formatting and parsing of numbers, dates and currency, and upper- and lower-casing, name the
culture they use: `CultureInfo.InvariantCulture` for values stored, exchanged or compared by
software — file contents, cache keys, URLs, database text, hand-written JSON — and the request's
culture for values shown to people. Each request runs under the culture it ran under in the
legacy system, taken from the same source.

## Rationale

Two things change at once in a migration.

The culture data changes. ICU and NLS disagree on some of it: for a language-only culture such
as `de`, `string.Format("{0:C}", 100)` renders `100,00 €` under NLS but `100,00 ¤` under ICU,
because ICU treats a currency as a property of a country or region, not of a language.

More often, the *source* of the culture changes. ASP.NET Framework applied
`<globalization culture="…" uiCulture="…" />` from `web.config` to every request. ASP.NET Core does
not read `web.config`: it runs with the process's culture — the host's regional settings on
Windows, an environment variable or nothing at all in a container — unless request localization is
configured. A price formatted with the ambient culture can come back with a different decimal
separator, and a date parsed with it can fail or swap day and month. Naming the culture at each
call site makes the intent visible; configuring request localization reproduces the rest.

## Non-compliant

```csharp
var cacheKey = "price:" + product.Id + ":" + price.ToString();  // separator depends on the host
var shipDate = DateTime.Parse(form["shipDate"]);                  // day/month order depends on the host
var code = couponCode.ToUpper();                                  // "file" becomes "FİLE" under tr-TR
```

## Compliant

```csharp
var cacheKey = string.Create(CultureInfo.InvariantCulture, $"price:{product.Id}:{price}");
var shipDate = DateTime.Parse(form["shipDate"], CultureInfo.CurrentCulture); // the request's culture, as before
var code = couponCode.ToUpperInvariant();
```

```csharp
// web.config had <globalization culture="pl-PL" uiCulture="pl-PL" />; reproduce it
var polish = new CultureInfo("pl-PL");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(polish),
    SupportedCultures = [polish],
    SupportedUICultures = [polish],
});
```

## Migration

- Find the legacy culture source first: the `<globalization>` element in `web.config` and its
  transforms, `culture="auto"` (which followed the browser's `Accept-Language`), and the IIS host's
  regional settings. Write it at the top of the behaviour-flags register; every call-site decision
  below depends on it.
- Reproduce it with `UseRequestLocalization`: a fixed culture becomes the default and only
  supported culture; `culture="auto"` becomes the `Accept-Language` provider with the cultures the
  site actually served.
- At each call site, pick the culture that reproduces what the legacy system produced under that
  culture. Machine text usually becomes `InvariantCulture`; if that gives a different result under
  the legacy culture (a `tr-TR` host upper-casing an `i`), flag it.
- Parsing of user input keeps the request culture it had; parsing of stored or exchanged text
  moves to `InvariantCulture` only if the stored text was written invariantly.

## Flag instead of fixing

- Every value shown to people whose formatting depends on culture data (currency symbols,
  separators, date patterns): ICU data may differ from NLS data for the same culture name.
- Any call site where the explicit culture gives a different result than the legacy ambient one.

## References

- .NET documentation, "Globalization APIs use ICU libraries on Windows" (breaking change in .NET 5)
  — the currency example.
- Code analysis rules CA1304, CA1305 and CA1311.

---

Second Key standards 0.1.0, from [`standards/SK-MIG-006-explicit-culture.md`](https://github.com/konradcinkusz/letsgolegacy.secondkey-standards/blob/v0.1.0/standards/SK-MIG-006-explicit-culture.md). Generated; do not edit.
