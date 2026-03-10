#!/usr/bin/env bash
# Run the full regression test suite with code coverage collection.
#
# Prerequisites:
#   - podman (or docker) + compose plugin
#   - Node.js / npx (for Bruno CLI)
#   - reportgenerator (dotnet tool)
#
# Usage:
#   bash tools/regression/run-regression.sh
#
# The script will:
#   1. Swap .dockerignore for a regression-permissive version
#   2. Build and start all containers
#   3. Wait for services to be ready
#   4. Run Bruno regression tests
#   5. Stop containers (flushes coverage data)
#   6. Generate coverage report
#   7. Restore original .dockerignore

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
COMPOSE_FILE="$SCRIPT_DIR/docker-compose.yml"
REPORT_DIR="$ROOT_DIR/TestResults/regression-report"

# Detect container runtime
if command -v podman-compose &>/dev/null; then
    COMPOSE="podman-compose"
elif command -v podman &>/dev/null && podman compose version &>/dev/null 2>&1; then
    COMPOSE="podman compose"
elif command -v docker-compose &>/dev/null; then
    COMPOSE="docker-compose"
elif command -v docker &>/dev/null; then
    COMPOSE="docker compose"
else
    echo "ERROR: No container compose tool found (podman-compose, docker-compose, etc.)"
    exit 1
fi
echo "Using compose tool: $COMPOSE"

cleanup() {
    echo ""
    echo "=== Stopping containers (coverage data will be flushed) ==="
    cd "$SCRIPT_DIR"
    $COMPOSE -f "$COMPOSE_FILE" down --volumes 2>/dev/null || true

    # Restore original .dockerignore
    if [ -f "$ROOT_DIR/.dockerignore.prod" ]; then
        mv "$ROOT_DIR/.dockerignore.prod" "$ROOT_DIR/.dockerignore"
        echo "Restored original .dockerignore"
    fi
}
trap cleanup EXIT

# --- Step 1: Swap .dockerignore ---
echo "=== Preparing build context ==="
cp "$ROOT_DIR/.dockerignore" "$ROOT_DIR/.dockerignore.prod"
cp "$SCRIPT_DIR/.dockerignore" "$ROOT_DIR/.dockerignore"
echo "Swapped .dockerignore for regression build"

# --- Step 2: Build and start containers ---
echo ""
echo "=== Building and starting containers ==="
cd "$SCRIPT_DIR"
$COMPOSE -f "$COMPOSE_FILE" up --build -d

# --- Step 3: Wait for API to be ready ---
echo ""
echo "=== Waiting for API to be ready ==="
API_URL="http://localhost:5090/health"
MAX_WAIT=120
ELAPSED=0
while ! curl -sf "$API_URL" >/dev/null 2>&1; do
    if [ "$ELAPSED" -ge "$MAX_WAIT" ]; then
        echo "ERROR: API did not become ready within ${MAX_WAIT}s"
        echo "Checking container logs:"
        $COMPOSE -f "$COMPOSE_FILE" logs api | tail -30
        exit 1
    fi
    sleep 2
    ELAPSED=$((ELAPSED + 2))
    printf "."
done
echo ""
echo "API is ready (took ${ELAPSED}s)"

# Give email/sms services a moment to connect to Kafka
sleep 5

# --- Step 4: Fetch JWT and run Bruno tests ---
echo ""
echo "=== Running Bruno regression tests ==="
cd "$ROOT_DIR/components/api/test/bruno"

# Mock-services token endpoint is only accessible through the API's forwarded port
# We need to reach mock-services directly — it's exposed through the compose network
# but not port-mapped. Instead, fetch a token from the host side.
# The mock-services Kestrel is NOT port-mapped, so we generate a token via curl to the
# API health endpoint first, then use the mock-services container directly.
#
# Simpler approach: port-map the token endpoint temporarily, or inject JWT as env var.
# For now, use docker exec to fetch the token from inside the network:
JWT=$($COMPOSE -f "$COMPOSE_FILE" exec -T mock-services \
    sh -c 'wget -qO- --header="Authorization: Basic bW9jazptb2Nr" \
    "http://localhost:5101/api/GetEnterpriseToken?env=local&scopes=altinn:serviceowner/notifications.create&org=ttd"' 2>/dev/null || echo "")

if [ -z "$JWT" ] || [ ${#JWT} -lt 100 ]; then
    echo "WARNING: Could not fetch JWT via container exec, trying curl fallback..."
    # Fallback: try if mock-services port is somehow reachable
    JWT=$(curl -sf -u mock:mock \
        "http://localhost:5101/api/GetEnterpriseToken?env=local&scopes=altinn:serviceowner/notifications.create&org=ttd" 2>/dev/null || echo "")
fi

if [ -z "$JWT" ] || [ ${#JWT} -lt 100 ]; then
    echo "ERROR: Failed to obtain JWT token"
    echo "Check mock-services logs:"
    $COMPOSE -f "$COMPOSE_FILE" logs mock-services | tail -20
    exit 1
fi
echo "JWT obtained (${#JWT} chars)"

npx @usebruno/cli run regression -r --env "v2 local" --env-var "jwt=$JWT"
TEST_EXIT=$?

# --- Step 5: Stop containers to flush coverage ---
echo ""
echo "=== Stopping containers ==="
cd "$SCRIPT_DIR"

# Send SIGTERM to api container to flush coverage gracefully
$COMPOSE -f "$COMPOSE_FILE" stop api
sleep 3

# Extract coverage file from volume
COVERAGE_FILE="$ROOT_DIR/TestResults/regression-coverage.cobertura.xml"
mkdir -p "$ROOT_DIR/TestResults"

# Copy coverage from the named volume
CONTAINER_ID=$($COMPOSE -f "$COMPOSE_FILE" ps -q api 2>/dev/null || echo "")
if [ -n "$CONTAINER_ID" ]; then
    # podman/docker cp from stopped container
    if command -v podman &>/dev/null; then
        podman cp "$CONTAINER_ID:/coverage/api.cobertura.xml" "$COVERAGE_FILE" 2>/dev/null || true
    else
        docker cp "$CONTAINER_ID:/coverage/api.cobertura.xml" "$COVERAGE_FILE" 2>/dev/null || true
    fi
fi

# --- Step 6: Generate report ---
if [ -f "$COVERAGE_FILE" ]; then
    echo ""
    echo "=== Generating coverage report ==="
    mkdir -p "$REPORT_DIR"
    reportgenerator \
        -reports:"$COVERAGE_FILE" \
        -targetdir:"$REPORT_DIR" \
        -reporttypes:"TextSummary;Html" \
        -assemblyfilters:"+Altinn.Notifications*"

    echo ""
    echo "=== Coverage Summary ==="
    cat "$REPORT_DIR/Summary.txt"
    echo ""
    echo "Full HTML report: $REPORT_DIR/index.html"
else
    echo "WARNING: No coverage file found. Coverage collection may have failed."
fi

echo ""
echo "=== Done ==="
echo "Tests exit code: $TEST_EXIT"
exit $TEST_EXIT
