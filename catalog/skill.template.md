# Applying the Second Key migration standards

The standards a .NET Framework → .NET 10 migration is held to (version {{version}}, {{rule-count}}
rules). Read the rule table before changing code, and open a rule's reference file before applying
it: the reference holds the rationale, a non-compliant and a compliant example, and what to flag
instead of fixing.

## Rules

{{rule-table}}

## What the gate checks

The gate reports these diagnostics at these severities:

{{gate-table}}

Behaviour flags are recorded in `{{flag-register}}` (see SK-MIG-011).
