#!/usr/bin/env bash
# Run the full regression test suite with code coverage collection.
#
# Prerequisites:
#   - podman (or docker) + compose plugin
#   - Node.js / npx (for Bruno CLI)
#   - reportgenerator (dotnet tool)
#
# Usage:
#   bash tools/regression/run-regression.sh [--asynch=kafka|asb] [--help]
#
# Options:
#   --asynch=kafka   (default) All async messaging flows over Kafka; Wolverine disabled.
#                    Only 7 containers start (no MSSQL / ASB emulator) — faster.
#   --asynch=asb     Wolverine enabled across all services; email send commands and
#                    email/SMS delivery reports flow over Azure Service Bus (emulator).
#                    9 containers start (adds mssql + servicebus-emulator).
#   --help, -h       Show this usage block.
#
# Results are written to:
#   TestResults/<date>_<seq>/bruno/results.txt             (human-readable stdout)
#   TestResults/<date>_<seq>/bruno/results.json            (Bruno --reporter-json output)
#   TestResults/<date>_<seq>/bruno/results-summary.json    (diff-stable, if jq available)
#   TestResults/<date>_<seq>/code-coverage/index.html

set -euo pipefail

# --- CLI parsing ---
ASYNCH_MODE="kafka"
for arg in "$@"; do
    case "$arg" in
        --asynch=kafka|--asynch=asb) ASYNCH_MODE="${arg#--asynch=}" ;;
        --help|-h)
            sed -n '2,24p' "$0"
            exit 0
            ;;
        *)
            echo "ERROR: unknown arg '$arg' (expected --asynch=kafka or --asynch=asb)" >&2
            exit 2
            ;;
    esac
done

COMPOSE_PROFILE_ARGS=()
if [ "$ASYNCH_MODE" = "asb" ]; then
    export ENABLE_WOLVERINE=true
    COMPOSE_PROFILE_ARGS=(--profile asb)
else
    export ENABLE_WOLVERINE=false
fi
echo "Async transport mode: $ASYNCH_MODE"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
COMPOSE_FILE="$SCRIPT_DIR/docker-compose.yml"

# --- Build a unique output directory: TestResults/yyyy-mm-dd_NNN ---
DATE_PREFIX=$(date +%Y-%m-%d)
SEQ=1
while [ -d "$ROOT_DIR/TestResults/${DATE_PREFIX}_$(printf '%03d' $SEQ)" ]; do
    SEQ=$((SEQ + 1))
done
RUN_DIR="$ROOT_DIR/TestResults/${DATE_PREFIX}_$(printf '%03d' $SEQ)"
BRUNO_DIR="$RUN_DIR/bruno"
COVERAGE_DIR="$RUN_DIR/code-coverage"
mkdir -p "$BRUNO_DIR" "$COVERAGE_DIR"

echo "Results will be written to: $RUN_DIR"

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
    $COMPOSE "${COMPOSE_PROFILE_ARGS[@]}" -f "$COMPOSE_FILE" down --volumes 2>/dev/null || true

    # Keep the bind-mount scratch dir around for diagnostics if something went wrong;
    # it's overwritten at the start of the next run and is .gitignore'd.

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

# Prepare host-side coverage bind mount (API writes cobertura XML here).
# Creating it up front prevents Docker from creating it as root-owned on Linux.
COVERAGE_BIND_DIR="$SCRIPT_DIR/coverage-output"
rm -rf "$COVERAGE_BIND_DIR"
mkdir -p "$COVERAGE_BIND_DIR"

# --- Step 2: Build and start containers ---
# In ASB mode we start the emulator stack FIRST and wait for AMQP, so that
# when the API starts its Wolverine bootstrap can connect immediately.
cd "$SCRIPT_DIR"
if [ "$ASYNCH_MODE" = "asb" ]; then
    echo ""
    echo "=== Starting ASB emulator stack (mssql + servicebus-emulator) ==="
    $COMPOSE "${COMPOSE_PROFILE_ARGS[@]}" -f "$COMPOSE_FILE" up --build -d mssql servicebus-emulator

    echo ""
    echo "=== Waiting for ASB emulator (AMQP port 5672) ==="
    ASB_WAIT=0
    while ! (echo > /dev/tcp/localhost/5672) 2>/dev/null; do
        if [ "$ASB_WAIT" -ge 120 ]; then
            echo "ERROR: ASB emulator did not become ready within 120s"
            $COMPOSE "${COMPOSE_PROFILE_ARGS[@]}" -f "$COMPOSE_FILE" logs servicebus-emulator | tail -20
            exit 1
        fi
        sleep 3
        ASB_WAIT=$((ASB_WAIT + 3))
        printf "."
    done
    echo ""
    echo "ASB emulator ready (took ${ASB_WAIT}s)"
fi

echo ""
echo "=== Building and starting remaining containers ==="
$COMPOSE "${COMPOSE_PROFILE_ARGS[@]}" -f "$COMPOSE_FILE" up --build -d

# --- Step 3: Wait for services to be ready ---

echo ""
echo "=== Waiting for API to be ready ==="
API_URL="http://127.0.0.1:5090/health"
MAX_WAIT=240
ELAPSED=0
while ! curl -sf "$API_URL" >/dev/null 2>&1; do
    if [ "$ELAPSED" -ge "$MAX_WAIT" ]; then
        echo "ERROR: API did not become ready within ${MAX_WAIT}s"
        echo "Checking container logs:"
        $COMPOSE "${COMPOSE_PROFILE_ARGS[@]}" -f "$COMPOSE_FILE" logs api | tail -30
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

JWT=$($COMPOSE "${COMPOSE_PROFILE_ARGS[@]}" -f "$COMPOSE_FILE" exec -T mock-services \
    sh -c 'wget -qO- --header="Authorization: Basic bW9jazptb2Nr" \
    "http://localhost:5101/api/GetEnterpriseToken?env=local&scopes=altinn:serviceowner/notifications.create&org=ttd"' 2>/dev/null || echo "")

if [ -z "$JWT" ] || [ ${#JWT} -lt 100 ]; then
    echo "WARNING: Could not fetch JWT via container exec, trying curl fallback..."
    JWT=$(curl -sf -u mock:mock \
        "http://localhost:5101/api/GetEnterpriseToken?env=local&scopes=altinn:serviceowner/notifications.create&org=ttd" 2>/dev/null || echo "")
fi

if [ -z "$JWT" ] || [ ${#JWT} -lt 100 ]; then
    echo "ERROR: Failed to obtain JWT token"
    echo "Check mock-services logs:"
    $COMPOSE "${COMPOSE_PROFILE_ARGS[@]}" -f "$COMPOSE_FILE" logs mock-services | tail -20
    exit 1
fi
echo "JWT obtained (${#JWT} chars)"

# Run Bruno and tee output to both console and results file.
# --reporter-json emits a structured per-request result file for diffing between runs.
npx @usebruno/cli run regression -r \
    --env "v2 local" \
    --env-var "jwt=$JWT" \
    --env-var "host=http://127.0.0.1:5090" \
    --reporter-json "$BRUNO_DIR/results.json" \
    2>&1 | tee "$BRUNO_DIR/results.txt" && TEST_EXIT=0 || TEST_EXIT=$?

# Produce a diff-stable summary by stripping non-deterministic fields (timings, timestamps).
# Skipped silently if jq isn't installed. Use --reporter-json file directly if you want the raw view.
if command -v jq >/dev/null 2>&1 && [ -f "$BRUNO_DIR/results.json" ]; then
    jq -S 'del(.. |
             .responseTime?,
             .runtime?,
             .duration?,
             .timings?,
             .timestamp?,
             .requestSentTimestamp?,
             .responseReceivedTimestamp?,
             .startTime?,
             .endTime?,
             .startedAt?,
             .completedAt?,
             .iterationData?)' \
        "$BRUNO_DIR/results.json" > "$BRUNO_DIR/results-summary.json" \
        && echo "Diff-friendly summary written: $BRUNO_DIR/results-summary.json" \
        || echo "WARNING: jq summary step failed (raw results.json is still available)"
fi

# --- Step 5: Stop API container to flush coverage ---
# docker-compose stop honours stop_signal (SIGINT) + stop_grace_period (30s) from the compose file.
# ASP.NET Core treats SIGINT as graceful shutdown, which lets dotnet-coverage write /coverage/api.cobertura.xml
# before the container exits. The file lands on the host via bind mount - no docker cp needed.
echo ""
echo "=== Stopping API container (graceful shutdown to flush coverage) ==="
cd "$SCRIPT_DIR"
$COMPOSE "${COMPOSE_PROFILE_ARGS[@]}" -f "$COMPOSE_FILE" stop api
# Give the kernel a moment to sync the bind-mount write
sleep 2

COVERAGE_FILE="$COVERAGE_DIR/regression-coverage.cobertura.xml"
if [ -f "$COVERAGE_BIND_DIR/api.cobertura.xml" ]; then
    mv "$COVERAGE_BIND_DIR/api.cobertura.xml" "$COVERAGE_FILE"
    echo "Coverage file captured: $COVERAGE_FILE"
else
    echo "WARNING: $COVERAGE_BIND_DIR/api.cobertura.xml not found after API stop."
    echo "Contents of $COVERAGE_BIND_DIR:"
    ls -la "$COVERAGE_BIND_DIR" 2>/dev/null || true
    echo "Last 30 lines of API logs:"
    $COMPOSE "${COMPOSE_PROFILE_ARGS[@]}" -f "$COMPOSE_FILE" logs api 2>&1 | tail -30 || true
fi

# Preserve dotnet-coverage diagnostic log alongside the cobertura file (if produced)
if [ -f "$COVERAGE_BIND_DIR/dotnet-coverage.log" ]; then
    mv "$COVERAGE_BIND_DIR/dotnet-coverage.log" "$COVERAGE_DIR/dotnet-coverage.log"
fi

# --- Step 6: Generate report ---
if [ -f "$COVERAGE_FILE" ]; then
    echo ""
    echo "=== Generating coverage report ==="
    reportgenerator \
        -reports:"$COVERAGE_FILE" \
        -targetdir:"$COVERAGE_DIR" \
        -reporttypes:"TextSummary;Html" \
        -assemblyfilters:"+Altinn.Notifications*"

    echo ""
    echo "=== Coverage Summary ==="
    cat "$COVERAGE_DIR/Summary.txt"
else
    echo "WARNING: No coverage file found. Coverage collection may have failed."
fi

echo ""
echo "=== Done ==="
echo "Results:  $RUN_DIR"
echo "  Bruno text:    $BRUNO_DIR/results.txt"
echo "  Bruno JSON:    $BRUNO_DIR/results.json"
if [ -f "$BRUNO_DIR/results-summary.json" ]; then
    echo "  Bruno summary: $BRUNO_DIR/results-summary.json (diff-stable)"
fi
echo "  Coverage:      $COVERAGE_DIR/index.html"
echo "Tests exit code: $TEST_EXIT"
exit $TEST_EXIT
