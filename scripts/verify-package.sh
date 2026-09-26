#!/usr/bin/env bash
#
# verify-package.sh — pack the SecondKey.Standards NuGet package and prove a consuming build
# picks its analyzer configuration up. The local mirror of CI's "package" job
# (REPO-BASELINE.md §4: a script that reproduces a CI job 1:1).
#
# Usage:
#   ./scripts/verify-package.sh [output-directory]     default: artifacts/packages
#
# What "works" means, checked against a throwaway consumer project outside this repository (so
# none of this repository's own build settings leak into it):
#   1. without the package, CA1310 is silent (the .NET SDK ships it disabled);
#   2. with the package, CA1310 is reported at the severity SK-MIG-005 gives it;
#   3. with SecondKeyStandardsGlobalConfig=false, the package's configuration is switched off.
# CA1310 stands in for every mapped diagnostic: Portcullis's analyzers are a separate package, but
# the severities reach the compiler through the same global config.

set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "${REPO_ROOT}"

OUT="${1:-${REPO_ROOT}/artifacts/packages}"
PACKAGE_ID="$(sed -n 's/.*"id": "\([^"]*\)".*/\1/p' catalog/pack.json | head -1)"
VERSION="$(sed -n 's/^  "version": "\([^"]*\)".*/\1/p' catalog/pack.json)"
PROJECT="generated/nuget/${PACKAGE_ID}.csproj"

red()   { printf '\033[0;31m%s\033[0m\n' "$*"; }
green() { printf '\033[0;32m%s\033[0m\n' "$*"; }
fail()  { red "verify-package: $*"; exit 1; }

[ -f "${PROJECT}" ] || fail "${PROJECT} is missing; run the generator first."

echo "Packing ${PACKAGE_ID} ${VERSION} → ${OUT}"
dotnet pack "${PROJECT}" --configuration Release --output "${OUT}" --nologo
NUPKG="${OUT}/${PACKAGE_ID}.${VERSION}.nupkg"
[ -f "${NUPKG}" ] || fail "expected ${NUPKG}"

echo "Checking the package layout"
CONTENTS="$(unzip -Z1 "${NUPKG}")"
for entry in "build/${PACKAGE_ID}.props" "build/${PACKAGE_ID}.globalconfig" "README.md" "LICENSE"; do
  grep -qx "${entry}" <<<"${CONTENTS}" || fail "the package has no ${entry}"
done
if grep -q '^lib/' <<<"${CONTENTS}"; then
  fail "the package ships an assembly under lib/; it must be configuration only"
fi

CONSUMER="$(mktemp -d)"
trap 'rm -rf "${CONSUMER}"' EXIT
cd "${CONSUMER}"

cat > nuget.config <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="${OUT}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
EOF

cat > Consumer.csproj <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
EOF

cat > Pricing.cs <<'EOF'
public static class Pricing
{
    // Culture-sensitive by default: SK-MIG-005 wants the StringComparison stated.
    public static int CompareNames(string left, string right) => string.Compare(left, right);
}
EOF

build() { dotnet build --nologo --no-incremental "$@" 2>&1; }

echo "1. Without the package"
if build | grep -q 'CA1310'; then
  fail "CA1310 is reported without the package; the check below would prove nothing"
fi

dotnet add package "${PACKAGE_ID}" --version "${VERSION}" >/dev/null

echo "2. With the package"
OUTPUT="$(build)"
grep -q 'warning CA1310' <<<"${OUTPUT}" || { echo "${OUTPUT}"; fail "CA1310 was not reported as a warning with the package"; }

echo "3. With the package switched off"
if build -p:SecondKeyStandardsGlobalConfig=false | grep -q 'CA1310'; then
  fail "SecondKeyStandardsGlobalConfig=false did not switch the configuration off"
fi

green "verify-package: ${PACKAGE_ID} ${VERSION} delivers its analyzer configuration to a consuming build."
