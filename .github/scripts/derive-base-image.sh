#!/usr/bin/env bash
#
# Derive the deployed base image reference from a Dockerfile.
#
# The scanned artifact is the final build stage (docker build with no --target),
# so the *last* FROM line identifies the base image that actually ships. This
# script parses that line and derives the floating channel tag Microsoft
# publishes (major.minor + OS suffix), which always points at the newest patch.
# Nothing is hardcoded, so bumping the Dockerfile to a new .NET or OS version is
# picked up automatically.
#
# Usage: derive-base-image.sh <Dockerfile>
# Output: KEY=VALUE lines (suitable for appending to $GITHUB_OUTPUT).
#
#   BASE_REPO     e.g. mcr.microsoft.com/dotnet/aspnet
#   BASE_TAG      e.g. 10.0.9-alpine3.23
#   BASE_DIGEST   e.g. sha256:...            (empty if the FROM line is not pinned)
#   BASE_VERSION  e.g. 10.0.9
#   BASE_CHANNEL  e.g. 10.0
#   OS_SUFFIX     e.g. alpine3.23            (empty if the tag has no OS suffix)
#   FLOATING_TAG  e.g. 10.0-alpine3.23
#
# BASE_DIGEST is both compared against the floating tag's live digest (to decide
# whether a newer base exists) and shown in the mitigation report, so a rebuild
# published under an unchanged version tag is still detected and surfaced.

set -euo pipefail

dockerfile="${1:?usage: derive-base-image.sh <Dockerfile>}"

if [[ ! -f "$dockerfile" ]]; then
  echo "derive-base-image.sh: file not found: $dockerfile" >&2
  exit 1
fi

# Last FROM line = the final stage = the image that gets tagged and scanned.
from_line=$(grep -iE '^[[:space:]]*FROM[[:space:]]' "$dockerfile" | tail -n1)
if [[ -z "$from_line" ]]; then
  echo "derive-base-image.sh: no FROM line found in $dockerfile" >&2
  exit 1
fi

# Extract the image reference (repo:tag@digest) from the FROM line. A FROM line
# may carry flags, e.g. `FROM --platform=$BUILDPLATFORM image AS build`, so we
# tokenise: skip the FROM keyword and any --flag tokens, then take the first
# remaining token -- the image reference. The optional `AS <stage>` alias comes
# after the image, so it is never reached.
ref=$(printf '%s\n' "$from_line" | awk '{
  for (i = 1; i <= NF; i++) {
    if (toupper($i) == "FROM") continue   # the FROM keyword
    if ($i ~ /^--/)            continue   # a flag, e.g. --platform=linux/amd64
    print $i                              # first non-flag token = image reference
    exit
  }
}')

# Split off the optional @sha256:... digest.
case "$ref" in
  *@*) digest="${ref#*@}" ;;
  *)   digest="" ;;  # reference is not pinned by digest
esac
image_and_tag="${ref%@*}"

# repo is everything before the last ':', tag is everything after it.
repo="${image_and_tag%:*}"
tag="${image_and_tag##*:}"
if [[ "$repo" = "$image_and_tag" ]]; then
  # No ':' present -> untagged reference; treat the whole thing as the repo.
  repo="$image_and_tag"
  tag=""
fi

# Derive the floating channel tag: reduce the version to major.minor and keep
# the OS suffix verbatim.  10.0.9-alpine3.23 -> 10.0-alpine3.23
version="${tag%%-*}"
case "$tag" in
  *-*) os_suffix="${tag#*-}" ;;   # e.g. 10.0.9-alpine3.23 -> alpine3.23
  *)   os_suffix="" ;;            # tag has no OS suffix, e.g. 10.0.9
esac
channel=$(printf '%s\n' "$version" | awk -F. '{ if (NF>=2) print $1"."$2; else print $1 }')

if [[ -n "$os_suffix" ]]; then
  floating="${channel}-${os_suffix}"
else
  floating="${channel}"
fi

printf 'BASE_REPO=%s\n' "$repo"
printf 'BASE_TAG=%s\n' "$tag"
printf 'BASE_DIGEST=%s\n' "$digest"
printf 'BASE_VERSION=%s\n' "$version"
printf 'BASE_CHANNEL=%s\n' "$channel"
printf 'OS_SUFFIX=%s\n' "$os_suffix"
printf 'FLOATING_TAG=%s\n' "$floating"
