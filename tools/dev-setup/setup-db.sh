#!/bin/bash
#
# Sets up a local PostgreSQL database in Podman for Altinn Notifications.
# Handles only infrastructure: container, database, and roles.
# Migrations are applied by the application itself on startup (Yuniql).
#
# Usage: bash tools/dev-setup/setup-db.sh
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# On Windows (Git Bash / MSYS2), use podman.exe and convert Unix paths
# to Windows paths so the compose provider can find the files.
if command -v podman.exe &>/dev/null; then
  PODMAN=podman.exe
  # Convert /mnt/c/foo or /c/foo to C:/foo for Windows tools
  SCRIPT_DIR="$(echo "$SCRIPT_DIR" | sed -e 's|^/mnt/\([a-zA-Z]\)/|\U\1:/|' -e 's|^/\([a-zA-Z]\)/|\U\1:/|')"
else
  PODMAN=podman
fi

DB_NAME=notificationsdb
DB_ADMIN_USER=platform_notifications_admin
DB_ADMIN_PASSWORD="${DB_ADMIN_PASSWORD:-Password}"
DB_APP_USER=platform_notifications
DB_APP_PASSWORD="${DB_APP_PASSWORD:-Password}"
CONTAINER_NAME=altinn-notifications-db

# Helper: run psql inside the container
run_psql() {
  $PODMAN exec -e PGPASSWORD="$DB_ADMIN_PASSWORD" "$CONTAINER_NAME" \
    psql -U "$DB_ADMIN_USER" -d "$DB_NAME" "$@"
}

# Helper: wait for PostgreSQL to become ready (max 60 seconds)
wait_for_postgres() {
  local elapsed=0
  local timeout=60
  until $PODMAN exec "$CONTAINER_NAME" pg_isready -U "$DB_ADMIN_USER" -d "$DB_NAME" > /dev/null 2>&1; do
    if [ "$elapsed" -ge "$timeout" ]; then
      echo "ERROR: PostgreSQL did not become ready within ${timeout}s." >&2
      exit 1
    fi
    sleep 1
    elapsed=$((elapsed + 1))
  done
}

# ── 1. Start PostgreSQL container ──────────────────────────────────────────────
echo "==> Starting PostgreSQL container..."
$PODMAN compose -f "$SCRIPT_DIR/setup-db.yml" up -d

echo "==> Waiting for PostgreSQL to become ready..."
wait_for_postgres
echo "    PostgreSQL is ready."

# ── 2. Configure database ─────────────────────────────────────────────────────
echo "==> Configuring database..."

# Increase max connections
run_psql -c "ALTER SYSTEM SET max_connections TO '200';"

# Create or update application role (idempotent, injection-safe)
run_psql -c "DO \$\$
DECLARE
  app_user text := '$DB_APP_USER';
  app_pwd  text := '$DB_APP_PASSWORD';
BEGIN
  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = app_user) THEN
    EXECUTE format('ALTER ROLE %I WITH LOGIN PASSWORD %L', app_user, app_pwd);
  ELSE
    EXECUTE format('CREATE ROLE %I WITH LOGIN PASSWORD %L', app_user, app_pwd);
  END IF;
END \$\$;"

# Restart container to apply max_connections change
echo "==> Restarting PostgreSQL to apply settings..."
$PODMAN restart "$CONTAINER_NAME" > /dev/null
wait_for_postgres

echo ""
echo "==> Database setup complete!"
echo "    Host:     localhost:5432"
echo "    Database: $DB_NAME"
echo "    Admin:    $DB_ADMIN_USER / $DB_ADMIN_PASSWORD"
echo "    App:      $DB_APP_USER / $DB_APP_PASSWORD"
echo ""
echo "    Migrations will be applied automatically when the application starts."
