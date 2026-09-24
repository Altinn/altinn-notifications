#!/usr/bin/env bash
#
# Classify Trivy findings for the application image into actionable mitigations
# and render a markdown table to the GitHub Actions job summary.
#
# For every CRITICAL/HIGH finding it decides one of:
#   - App dependency          -> fix in the .csproj (not a base-image concern)
#   - Base image bump         -> a newer base image already ships the fix
#   - Not yet fixed upstream   -> present in the latest base too; needs a
#                                 Dockerfile workaround or an upstream fix
#
# "Base-origin" (i.e. comes from the base image, not your app) is determined
# from the app scan alone: OS packages (Class == os-pkgs) and .NET assemblies
# under the shared framework path (usr/share/dotnet/...). The app's own NuGet
# packages -- whose deps.json Target is under app/ -- are app dependencies.
# (Trivy labels both the runtime and the app's deps.json as Type dotnet-core,
# so the location, not the type, is what discriminates them.)
# Whether a base-origin finding is already fixed is answered by diffing its
# vulnerability ID against a scan of the latest base image, which the caller
# supplies only when a newer base actually exists.
#
# Usage: analyze-base-fixes.sh <app-trivy.json> [base-latest-trivy.json]
#
# Environment (all optional, used for wording only):
#   HAS_NEW_BASE   "true" when a newer base image was found and scanned
#   FLOATING_TAG   floating channel tag, e.g. 10.0-alpine3.23
#   LATEST_VERSION concrete latest patch, e.g. 10.0.11-alpine3.23
#   LATEST_DIGEST  digest the floating tag currently resolves to, e.g. sha256:...
#   BASE_TAG       currently pinned tag, e.g. 10.0.9-alpine3.23
#   BASE_DIGEST    digest currently pinned in the Dockerfile, e.g. sha256:...
#
# The report shows the full tag@digest for the deployed and available images
# (so the digest can be copied straight into the Dockerfile) because a rebuilt
# base is often published under an unchanged version tag -- the digest is then
# the only thing that changed. Table cells use the short (12-char) digest to
# stay narrow.
#
# Output goes to $GITHUB_STEP_SUMMARY when set, otherwise to stdout.

set -euo pipefail

app_json="${1:?usage: analyze-base-fixes.sh <app-trivy.json> [base-latest-trivy.json]}"
base_json="${2:-}"

if [[ ! -f "$app_json" ]]; then
  echo "analyze-base-fixes.sh: file not found: $app_json" >&2
  exit 1
fi

has_new_base="${HAS_NEW_BASE:-false}"
floating_tag="${FLOATING_TAG:-the latest base image}"
latest_version="${LATEST_VERSION:-}"
base_tag="${BASE_TAG:-}"
base_digest="${BASE_DIGEST:-}"
latest_digest="${LATEST_DIGEST:-}"

# Join a tag/version with its digest for display: "tag@sha256:..." (or just
# "tag" when the digest is unknown). The digest is the only thing that
# distinguishes a rebuilt image published under an unchanged version tag.
ref_with_digest() {
  local ref="$1" digest="$2"
  if [[ -n "$digest" ]]; then
    printf '%s@%s' "$ref" "$digest"
  else
    printf '%s' "$ref"
  fi
}

# Abbreviate a digest to its first 12 hex chars (docker-style), e.g.
# sha256:f03685b2735e... -> sha256:f03685b2735e. Used in the table so cells
# stay narrow; the header keeps the full, copy-pasteable digest.
short_digest() {
  local digest="$1"
  if [[ "$digest" == sha256:* ]]; then
    local hex="${digest#sha256:}"
    printf 'sha256:%s' "${hex:0:12}"
  else
    printf '%s' "$digest"
  fi
}

# Set of vulnerability IDs still present in the latest base image (if scanned).
declare -A latest_base_ids=()
if [[ -n "$base_json" ]] && [[ -f "$base_json" ]]; then
  if ! base_ids="$(jq -r '[.Results[]?.Vulnerabilities[]?.VulnerabilityID] | unique[]' "$base_json")"; then
    echo "analyze-base-fixes.sh: invalid JSON in $base_json" >&2
    exit 1
  fi
  while IFS= read -r id; do
    [[ -n "$id" ]] && latest_base_ids["$id"]=1
  done < <(printf '%s\n' "$base_ids" | tr -d '\r')
elif [[ "$has_new_base" = "true" ]]; then
  echo "analyze-base-fixes.sh: HAS_NEW_BASE=true but no base scan file provided" >&2
  exit 1
fi

# Emit each finding as a tab-separated row from the app scan.
# Fields: id, pkg, installed, fixed, severity, class, location
# `location` is the package path when Trivy provides one, otherwise the result
# Target. For .deps.json findings PkgPath is null, so the Target is what tells
# the runtime shared framework apart from the app's own packages.
extract() {
  jq -r '
    .Results[]? as $r
    | ($r.Class // "")  as $class
    | ($r.Target // "") as $target
    | ($r.Vulnerabilities // [])[]
    | [ .VulnerabilityID,
        .PkgName,
        (.InstalledVersion // ""),
        (.FixedVersion // "-"),
        (.Severity // ""),
        $class,
        (.PkgPath // $target) ]
    | @tsv
  ' "$app_json"
}

is_base_origin() {
  local class="$1" location="$2"
  [[ "$class" = "os-pkgs" ]] && return 0
  case "$location" in
    usr/share/dotnet/*|/usr/share/dotnet/*|usr/lib/dotnet/*|/usr/lib/dotnet/*) return 0 ;;
    *) return 1 ;;  # app-level dependency, e.g. app/Altinn.Profile.deps.json
  esac
}

rows=""
count_total=0
count_bump=0
count_upstream=0
count_appdep=0

while IFS=$'\t' read -r id pkg installed fixed severity class location; do
  [[ -z "$id" ]] && continue
  count_total=$((count_total + 1))

  if is_base_origin "$class" "$location"; then
    if [[ "$has_new_base" = "true" ]] && [[ -n "${latest_base_ids[$id]:-}" ]]; then
      verdict="⏳ Not yet fixed upstream — Dockerfile workaround or wait"
      count_upstream=$((count_upstream + 1))
    elif [[ "$has_new_base" = "true" ]]; then
      target="$(ref_with_digest "${latest_version:-$floating_tag}" "$(short_digest "$latest_digest")")"
      verdict="✅ Base image bump — update Dockerfile to \`$target\`"
      count_bump=$((count_bump + 1))
    else
      verdict="⏳ Already on latest base — Dockerfile workaround or wait"
      count_upstream=$((count_upstream + 1))
    fi
  else
    verdict="🔧 App dependency — update the package in its .csproj"
    count_appdep=$((count_appdep + 1))
  fi

  rows+="| ${severity} | ${id} | \`${pkg}\` | ${installed} | ${fixed} | ${verdict} |"$'\n'
done < <(extract | tr -d '\r')

# Render.
{
  echo "## 🐳 Base image mitigation analysis"
  echo
  if [[ -n "$base_tag" ]]; then
    echo "Deployed base image: \`$(ref_with_digest "$base_tag" "$base_digest")\`"
  fi
  if [[ "$has_new_base" = "true" ]]; then
    echo "A newer base image is available: \`$(ref_with_digest "${latest_version:-$floating_tag}" "$latest_digest")\`"
  else
    echo "No newer base image is published for \`${floating_tag}\` — you are already on the latest."
  fi
  echo

  if [[ "$count_total" -eq 0 ]]; then
    echo "No CRITICAL/HIGH findings to analyze. ✅"
  else
    echo "**${count_total}** finding(s): **${count_bump}** fixable by a base image bump, **${count_upstream}** awaiting an upstream fix, **${count_appdep}** app dependencies."
    echo
    echo "| Severity | CVE | Package | Installed | Fixed in | Mitigation |"
    echo "| --- | --- | --- | --- | --- | --- |"
    printf '%s' "$rows"
    echo
    echo "**Always consider the exploitability of the findings:**"
    echo
    echo "- For exploitable vulnerabilities, patch and release ASAP."
    echo "- For non-exploitable vulnerabilities:"
    echo "  - When there is an upstream fix for the base image or an app dependency, merge the patch to main and let the release follow the normal cadence cycle."
    echo "  - When awaiting the upstream fix, silence the finding by adding the CVE to \`.trivyignore.yaml\`. Include a reason that summarizes why it's not exploitable, add a reasonable expiry time, and merge to main."
  fi
} >> "${GITHUB_STEP_SUMMARY:-/dev/stdout}"
