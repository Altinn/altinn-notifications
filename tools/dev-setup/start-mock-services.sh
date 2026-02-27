#!/usr/bin/env bash
# Starts the local mock services for end-to-end Bruno testing.
# This provides mock implementations for Profile, Register, Authorization,
# SMS, Email, Condition, and Token/OpenID services.
#
# Usage: bash tools/dev-setup/start-mock-services.sh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

dotnet run --project "$SCRIPT_DIR/mock-services/Altinn.Notifications.MockServices.csproj"
