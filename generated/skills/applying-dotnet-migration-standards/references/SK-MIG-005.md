# SK-MIG-005 — State the StringComparison of every culture-sensitive string comparison and sort

| Severity | Category | Applies to | Gate diagnostic | Principle |
|---|---|---|---|---|
| warning | globalization | `*.cs` | `CA1310` | — |

Contents: Rationale · Non-compliant · Compliant · Migration · Flag instead of fixing · References

Every string comparison, search and sort whose default is culture-sensitive states its intent:
`StringComparison.Ordinal` or `OrdinalIgnoreCase` for machine text — identifiers, keys, codes,
URLs, file paths, protocol tokens — and `StringComparison.CurrentCulture` (or a named culture) for
text shown to people. This covers `string.Compare`, `CompareTo`, `StartsWith(string)`,
`EndsWith(string)`, `IndexOf(string)`, `LastIndexOf(string)`, and the default string comparer that
`OrderBy`, `List<string>.Sort`, `Array.Sort`, `SortedList` and `SortedDictionary` fall back to.

## Rationale

.NET Framework performs culture-sensitive string operations with Windows' National Language
Support (NLS). .NET 5 and later use ICU, on Windows as well as on Linux. The two disagree:

- ICU treats `\0` as a zero-weight character, so `"Hel\0lo".IndexOf("\0")` returns `0` where NLS
  returned `3`, and `"abc".EndsWith("\0")` is `true`.
- ICU's default sort behaves like `CompareOptions.StringSort`, which sorts punctuation before
  letters: "bill's" sorts before "bills", and a list of names can come back in a different order.
- NLS treats ligatures as equal to their letters ("oeuf" and "œuf"); ICU does not.

None of this is a compile error, and a unit test usually passes on whichever library the build
machine happens to use. Stating the comparison does two things. For machine text, an ordinal
comparison gives the same answer on every platform and removes the dependency on either library.
For human text, it marks the call site as linguistic, so it can be listed and checked as a place
where NLS → ICU may change a result. CA1310 finds the calls whose default is culture-sensitive; the
comparers that sorting APIs fall back to are not reported by any analyzer, so they need a search.

## Non-compliant

```csharp
if (sku.StartsWith("GIFT-"))                                   // culture-sensitive by default
{
    ApplyGiftCardRules(order);
}

products.Sort((a, b) => string.Compare(a.Name, b.Name));       // NLS order assumed
var byName = products.OrderBy(p => p.Name);                     // default comparer: current culture
```

## Compliant

```csharp
if (sku.StartsWith("GIFT-", StringComparison.Ordinal))          // a product code, not language
{
    ApplyGiftCardRules(order);
}

// Shown to people: still linguistic, now visibly so, and recorded as a behaviour flag
products.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
var byName = products.OrderBy(p => p.Name, StringComparer.CurrentCulture);
```

## Migration

- Decide per call site whether the strings are machine text or human text. Machine text gets
  `Ordinal` or `OrdinalIgnoreCase`; human text gets `CurrentCulture`, which keeps the legacy
  semantics and makes them explicit.
- Ordinal and linguistic comparisons agree on most ASCII identifiers, but not on every input:
  zero-weight and ignorable characters (`\0`, a soft hyphen) and case rules differ. Under the
  `tr-TR` culture, "FILE" and "file" are not equal ignoring case linguistically, but are equal
  under `OrdinalIgnoreCase`. When the input is not controlled — codes typed by users, imported
  data — or the legacy server ran with such a culture, record the choice as a flag.
- LINQ queries that Entity Framework translates to SQL are compared by the database collation,
  not by .NET. Do not add a `StringComparer` to them (it cannot be translated); they are not
  affected by NLS → ICU.
- `Dictionary<string, T>`, `HashSet<string>` and `string.Equals(string)` already compare
  ordinally; leave them alone.

## Flag instead of fixing

- Every linguistic sort or comparison over data shown to people. ICU may order or match
  differently from NLS even though the code is unchanged; the flag names the call site and the
  data, for example "product list sorted by name with CurrentCulture".

## References

- .NET documentation, "Globalization and ICU" — behavioural differences between NLS and ICU.
- .NET documentation, "Best practices for comparing strings in .NET".
- Code analysis rule CA1310, "Specify StringComparison for correctness".

---

Second Key standards 0.1.0, from [`standards/SK-MIG-005-explicit-string-comparison.md`](https://github.com/konradcinkusz/letsgolegacy.secondkey-standards/blob/v0.1.0/standards/SK-MIG-005-explicit-string-comparison.md). Generated; do not edit.
