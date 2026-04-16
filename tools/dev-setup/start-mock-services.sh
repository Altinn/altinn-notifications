#!/usr/bin/env bash
# Starts all local services needed for end-to-end Bruno testing:
#   1. Mock services (WireMock + Token/OIDC + TriggerScheduler)
#   2. Email service (with MockEmailServiceClient in dev mode)
#   3. SMS service (with MockSmsClient in dev mode)
#
# Usage: bash tools/dev-setup/start-mock-services.sh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"

MOCK_PID=""
EMAIL_PID=""
SMS_PID=""

cleanup() {
    echo ""
    echo "Stopping services..."
    [ -n "$MOCK_PID" ]  && kill "$MOCK_PID"  2>/dev/null
    [ -n "$EMAIL_PID" ] && kill "$EMAIL_PID" 2>/dev/null
    [ -n "$SMS_PID" ]   && kill "$SMS_PID"   2>/dev/null
    wait 2>/dev/null
    echo "All services stopped."
}
trap cleanup EXIT

echo "Starting mock services (WireMock + Token/OIDC + TriggerScheduler)..."
dotnet run --project "$SCRIPT_DIR/mock-services/Altinn.Notifications.MockServices.csproj" &
MOCK_PID=$!

# Note: --no-launch-profile is needed so that ASPNETCORE_URLS takes effect
# instead of the port configured in launchSettings.json.

echo "Starting Email service (port 5190, dev mode with MockEmailServiceClient)..."
ASPNETCORE_URLS="http://localhost:5190" \
ASPNETCORE_ENVIRONMENT="Development" \
MockSettings__EnableMockEmailProvider="true" \
dotnet run --no-launch-profile --project "$ROOT_DIR/components/email-service/src/Altinn.Notifications.Email" &
EMAIL_PID=$!

echo "Starting SMS service (port 5170, dev mode with MockSmsClient)..."
ASPNETCORE_URLS="http://localhost:5170" \
ASPNETCORE_ENVIRONMENT="Development" \
MockSettings__EnableMockSmsProvider="true" \
dotnet run --no-launch-profile --project "$ROOT_DIR/components/sms-service/src/Altinn.Notifications.Sms" &
SMS_PID=$!

echo ""
echo "All services started:"
echo "  Mock services:  WireMock (5020/5030/5050/5092/5199) + Token (5101) + TriggerScheduler"
echo "  Email service:  http://localhost:5190"
echo "  SMS service:    http://localhost:5170"
echo ""
echo "Press Ctrl+C to stop all services."
wait
