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

DB_NAME=notificationsdb
DB_ADMIN_USER=platform_notifications_admin
DB_ADMIN_PASSWORD=Password
DB_APP_USER=platform_notifications
DB_APP_PASSWORD=Password
CONTAINER_NAME=altinn-notifications-db

# Helper: run psql inside the container
run_psql() {
  podman exec -e PGPASSWORD="$DB_ADMIN_PASSWORD" "$CONTAINER_NAME" \
    psql -U "$DB_ADMIN_USER" -d "$DB_NAME" "$@"
}

# ── 1. Start PostgreSQL container ──────────────────────────────────────────────
echo "==> Starting PostgreSQL container..."
podman compose -f "$SCRIPT_DIR/setup-db.yml" up -d

echo "==> Waiting for PostgreSQL to become ready..."
until podman exec "$CONTAINER_NAME" pg_isready -U "$DB_ADMIN_USER" -d "$DB_NAME" > /dev/null 2>&1; do
  sleep 1
done
echo "    PostgreSQL is ready."

# ── 2. Configure database ─────────────────────────────────────────────────────
echo "==> Configuring database..."

# Increase max connections
run_psql -c "ALTER SYSTEM SET max_connections TO '200';"

# Create application role (idempotent)
run_psql -c "DO \$\$
BEGIN
  CREATE ROLE $DB_APP_USER WITH LOGIN PASSWORD '$DB_APP_PASSWORD';
EXCEPTION WHEN duplicate_object THEN
  RAISE NOTICE 'Role $DB_APP_USER already exists, skipping.';
END \$\$;"

# Restart container to apply max_connections change
echo "==> Restarting PostgreSQL to apply settings..."
podman restart "$CONTAINER_NAME" > /dev/null
until podman exec "$CONTAINER_NAME" pg_isready -U "$DB_ADMIN_USER" -d "$DB_NAME" > /dev/null 2>&1; do
  sleep 1
done

echo ""
echo "==> Database setup complete!"
echo "    Host:     localhost:5432"
echo "    Database: $DB_NAME"
echo "    Admin:    $DB_ADMIN_USER / $DB_ADMIN_PASSWORD"
echo "    App:      $DB_APP_USER / $DB_APP_PASSWORD"
echo ""
echo "    Migrations will be applied automatically when the application starts."
