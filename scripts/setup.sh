#!/usr/bin/env bash
#
# setup.sh — one-command onboarding (REPO-BASELINE.md §3 in architecture-standards).
#
# Usage:
#   ./scripts/setup.sh            check prerequisites, install the pre-commit hook, verify
#   ./scripts/setup.sh --check    strict: a missing secret scanner is a failure
#
# The baseline's service-repository steps — initialise a secret store, generate the one
# mandatory secret, offer optional integrations — do not apply here and are omitted rather
# than faked: this repository has no secrets and no runtime. What applies is the rest: name the
# prerequisites with install pointers, install the hook P5 makes mandatory, and verify the tree.

set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "${REPO_ROOT}"

STRICT=0
[ "${1:-}" = "--check" ] && STRICT=1

red()   { printf '\033[0;31m%s\033[0m\n' "$*"; }
green() { printf '\033[0;32m%s\033[0m\n' "$*"; }
amber() { printf '\033[0;33m%s\033[0m\n' "$*"; }
dim()   { printf '\033[0;90m%s\033[0m\n' "$*"; }
step()  { printf '\n\033[1m%s\033[0m\n' "$*"; }

DEGRADED=0

# ── 1. Prerequisites ─────────────────────────────────────────────────────────
step "1. Prerequisites"

if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks | grep -q '^10\.'; then
  green "  .NET 10 SDK found — the generator and its tests need it."
else
  red   "  No .NET 10 SDK on PATH. The generator will not build without it."
  echo  "  Install: https://dotnet.microsoft.com/download/dotnet/10.0"
  exit 1
fi

# ── 2. Secret scanner ────────────────────────────────────────────────────────
step "2. Secret scanner"

if command -v gitleaks >/dev/null 2>&1; then
  green "  gitleaks on PATH — the hook will use it directly."
elif command -v docker >/dev/null 2>&1 && docker info >/dev/null 2>&1; then
  green "  gitleaks not on PATH, but Docker is running — the hook falls back to the"
  dim   "  same container image CI uses (ghcr.io/gitleaks/gitleaks)."
else
  DEGRADED=1
  amber "  No secret scanner available."
  echo  "  The hook installed below REFUSES TO COMMIT without one — that is by design"
  echo  "  (P5), not a bug to work around. Install one of:"
  echo  "    • gitleaks   https://github.com/gitleaks/gitleaks#installing"
  echo  "    • Docker     https://docs.docker.com/get-docker/"
fi

# ── 3. Git hooks ─────────────────────────────────────────────────────────────
step "3. Git hooks"

HOOK_SRC="${REPO_ROOT}/scripts/hooks/pre-commit"
HOOK_DST="$(git rev-parse --git-path hooks)/pre-commit"
mkdir -p "$(dirname "${HOOK_DST}")"

if [ -e "${HOOK_DST}" ] && ! cmp -s "${HOOK_SRC}" "${HOOK_DST}"; then
  cp "${HOOK_DST}" "${HOOK_DST}.backup"
  dim "  Existing hook backed up to pre-commit.backup"
fi
cp "${HOOK_SRC}" "${HOOK_DST}"
chmod +x "${HOOK_DST}"
green "  pre-commit installed → ${HOOK_DST}"

# ── 4. Verify ────────────────────────────────────────────────────────────────
step "4. Standards and generated tree"

if dotnet run --project src/SecondKey.Standards.Generator -- validate >/dev/null 2>&1; then
  green "  Every rule in standards/ is valid."
else
  amber "  The standards do not validate. See the problems with:"
  echo  "    dotnet run --project src/SecondKey.Standards.Generator -- validate"
fi

if dotnet run --project src/SecondKey.Standards.Generator -- generate --check >/dev/null 2>&1; then
  green "  generated/ is up to date."
else
  amber "  generated/ is stale, or the version gate is unsatisfied. Run:"
  echo  "    dotnet run --project src/SecondKey.Standards.Generator -- generate"
  dim   "  See README \"Versioning\" if it asks for a version bump."
fi

# ── Summary ──────────────────────────────────────────────────────────────────
step "Ready"

echo "  dotnet test                                                     run the tests"
echo "  dotnet run --project src/SecondKey.Standards.Generator -- validate   check the rules"
echo "  dotnet run --project src/SecondKey.Standards.Generator -- generate   regenerate generated/"
echo "  ./scripts/verify-package.sh                                     pack and verify the NuGet package"
echo "  ./scripts/scan-secrets.sh                                       mirror the CI secret scan"

if [ "${DEGRADED}" -eq 1 ]; then
  echo
  if [ "${STRICT}" -eq 1 ]; then
    red "setup --check: a secret scanner is required and none was found."
    exit 1
  fi
  amber "Setup finished, but committing will be refused until a scanner is installed."
fi
