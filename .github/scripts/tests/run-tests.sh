#!/usr/bin/env bash
#
# Fixture-based tests for the container-scan helper scripts. No Docker or
# network access required — pure data in, verdicts out.
#
# Usage: .github/scripts/tests/run-tests.sh

set -uo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
scripts="$(cd "$here/.." && pwd)"
fixtures="$here/fixtures"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

pass=0
fail=0

ok() {
  local label="$1"
  printf '  \033[32mPASS\033[0m %s\n' "$label"
  pass=$((pass + 1))
  return 0
}

bad() {
  local label="$1"
  printf '  \033[31mFAIL\033[0m %s\n' "$label"
  fail=$((fail + 1))
  return 0
}

# assert_kv <output> <KEY> <expected-value> <label>
assert_kv() {
  local out="$1" key="$2" want="$3" label="$4"
  local got
  got="$(printf '%s\n' "$out" | sed -n "s/^${key}=//p")"
  if [[ "$got" = "$want" ]]; then ok "$label ($key=$got)"; else bad "$label ($key: want '$want', got '$got')"; fi
  return 0
}

# assert_contains <haystack> <needle> <label>
assert_contains() {
  local haystack="$1" needle="$2" label="$3"
  case "$haystack" in
    *"$needle"*) ok "$label" ;;
    *)           bad "$label (missing: $needle)" ;;
  esac
  return 0
}

# assert_row <output> <cve> <verdict-substring> <label>
# Isolates the table row for <cve> and checks its verdict, so a verdict from a
# different row can't accidentally satisfy the assertion.
assert_row() {
  local out="$1" cve="$2" verdict="$3" label="$4"
  local row
  row="$(printf '%s\n' "$out" | grep -F "| $cve |")"
  case "$row" in
    *"$verdict"*) ok "$label" ;;
    *)            bad "$label (row: ${row:-<none>})" ;;
  esac
  return 0
}

echo "== derive-base-image.sh =="

df="$tmp/Dockerfile.aspnet"
cat > "$df" <<'EOF'
FROM mcr.microsoft.com/dotnet/sdk:10.0.301-alpine3.23@sha256:aaa AS build
FROM mcr.microsoft.com/dotnet/aspnet:10.0.9-alpine3.23@sha256:bbb AS final
EOF
out="$(bash "$scripts/derive-base-image.sh" "$df")"
assert_kv "$out" BASE_REPO    "mcr.microsoft.com/dotnet/aspnet" "picks the final (last) FROM"
assert_kv "$out" BASE_TAG     "10.0.9-alpine3.23"               "reads full tag"
assert_kv "$out" BASE_DIGEST  "sha256:bbb"                      "reads pinned digest"
assert_kv "$out" BASE_VERSION "10.0.9"                          "extracts version"
assert_kv "$out" BASE_CHANNEL "10.0"                            "reduces to major.minor"
assert_kv "$out" OS_SUFFIX    "alpine3.23"                      "extracts OS suffix"
assert_kv "$out" FLOATING_TAG "10.0-alpine3.23"                 "derives floating tag"

df="$tmp/Dockerfile.future"
cat > "$df" <<'EOF'
FROM mcr.microsoft.com/dotnet/aspnet:11.0.3-alpine3.25@sha256:ccc AS final
EOF
out="$(bash "$scripts/derive-base-image.sh" "$df")"
assert_kv "$out" FLOATING_TAG "11.0-alpine3.25" "generalizes to a future major/OS bump"

df="$tmp/Dockerfile.noble"
cat > "$df" <<'EOF'
FROM mcr.microsoft.com/dotnet/aspnet:10.0.9-noble@sha256:ddd AS final
EOF
out="$(bash "$scripts/derive-base-image.sh" "$df")"
assert_kv "$out" OS_SUFFIX    "noble"       "handles non-Alpine OS suffix"
assert_kv "$out" FLOATING_TAG "10.0-noble"  "derives floating tag for non-Alpine"

df="$tmp/Dockerfile.nodigest"
cat > "$df" <<'EOF'
FROM mcr.microsoft.com/dotnet/aspnet:10.0.9-alpine3.23 AS final
EOF
out="$(bash "$scripts/derive-base-image.sh" "$df")"
assert_kv "$out" BASE_DIGEST  ""                "tolerates an unpinned FROM"
assert_kv "$out" FLOATING_TAG "10.0-alpine3.23" "still derives floating tag when unpinned"

df="$tmp/Dockerfile.platform"
cat > "$df" <<'EOF'
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:10.0.9-alpine3.23@sha256:eee AS final
EOF
out="$(bash "$scripts/derive-base-image.sh" "$df")"
assert_kv "$out" BASE_REPO    "mcr.microsoft.com/dotnet/aspnet" "skips a --platform flag to find the image"
assert_kv "$out" BASE_TAG     "10.0.9-alpine3.23"               "reads tag past a FROM flag"
assert_kv "$out" BASE_DIGEST  "sha256:eee"                      "reads digest past a FROM flag"
assert_kv "$out" FLOATING_TAG "10.0-alpine3.23"                 "derives floating tag past a FROM flag"

echo "== analyze-base-fixes.sh =="

# A newer base exists and was scanned. Mirror the real "same version tag, new
# digest" case: the digest is the only thing that changed.
new_digest="sha256:abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"
old_digest="sha256:1111111111111111111111111111111111111111111111111111111111111111"
out="$(HAS_NEW_BASE=true FLOATING_TAG=10.0-alpine3.23 \
  LATEST_VERSION=10.0.9-alpine3.23 LATEST_DIGEST="$new_digest" \
  BASE_TAG=10.0.9-alpine3.23 BASE_DIGEST="$old_digest" \
  bash "$scripts/analyze-base-fixes.sh" "$fixtures/app-findings.json" "$fixtures/base-latest-findings.json")"
assert_contains "$out" "**4** finding(s): **2** fixable by a base image bump, **1** awaiting an upstream fix, **1** app dependencies." "counts add up with a newer base"
assert_row "$out" CVE-OS-FIXED      "Base image bump"        "OS pkg absent from latest base -> base bump"
assert_row "$out" CVE-OS-STILL      "Not yet fixed upstream" "OS pkg present in latest base -> awaiting upstream"
assert_row "$out" CVE-RUNTIME-FIXED "Base image bump"        "runtime assembly (usr/share/dotnet) is base-origin -> base bump"
# Regression: an app NuGet package is reported by Trivy as Type dotnet-core with
# a null PkgPath (Target under app/). It must NOT be mistaken for base-origin.
assert_row "$out" CVE-APP-DEP       "App dependency"         "app dep (dotnet-core, app/ target) -> app dependency, not base"
assert_contains "$out" "Deployed base image: \`10.0.9-alpine3.23@${old_digest}\`"          "deployed line shows full pinned digest"
assert_contains "$out" "A newer base image is available: \`10.0.9-alpine3.23@${new_digest}\`" "available line shows full latest digest"
assert_contains "$out" "update Dockerfile to \`10.0.9-alpine3.23@sha256:abcdef012345\`"    "bump verdict shows the short target digest"
case "$out" in *"update Dockerfile to \`10.0.9-alpine3.23@${new_digest}\`"*) bad "verdict must NOT carry the full digest" ;; *) ok "verdict omits the full digest" ;; esac

# No newer base published: base-origin findings should all read 'already on latest'.
out="$(HAS_NEW_BASE=false FLOATING_TAG=10.0-alpine3.23 BASE_TAG=10.0.9-alpine3.23 \
  bash "$scripts/analyze-base-fixes.sh" "$fixtures/app-findings.json")"
assert_contains "$out" "No newer base image is published" "reports we are on the latest base"
assert_contains "$out" "Already on latest base"           "base-origin -> already on latest"
assert_contains "$out" "**0** fixable by a base image bump" "nothing marked as base-bump without a newer base"
assert_contains "$out" "App dependency"                    "app dep still flagged without a newer base"

echo
echo "Passed: $pass  Failed: $fail"
[[ "$fail" -eq 0 ]]
