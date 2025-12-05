#!/bin/bash
#
# Update SQL cancellation scripts with configuration from config.json
#
# Usage:
#     ./update-scripts.sh
#
# This will read config.json and update both SQL scripts with the configuration values.
#

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG_FILE="$SCRIPT_DIR/config.json"
ANALYZE_SCRIPT="$SCRIPT_DIR/analyze-orders-for-cancellation.sql"
CANCEL_SCRIPT="$SCRIPT_DIR/cancel-orders-by-sendersreferences.sql"

# Check if config.json exists
if [ ! -f "$CONFIG_FILE" ]; then
    echo "Error: config.json not found in $SCRIPT_DIR"
    echo ""
    echo "First time setup:"
    echo "  1. Copy the example config: cp config.json.example config.json"
    echo "  2. Edit config.json with your values"
    echo "  3. Run this script again"
    exit 1
fi

# Check if jq is available
if ! command -v jq &> /dev/null; then
    echo "Error: jq is required but not installed."
    echo "Install it with: brew install jq (macOS) or apt-get install jq (Linux)"
    exit 1
fi

echo "Loading configuration from config.json..."

# Parse JSON using jq
SENDER_REFS_JSON=$(jq -r '.sendersreferences | @json' "$CONFIG_FILE")
CREATOR_NAME=$(jq -r '.creatorname' "$CONFIG_FILE")
CREATED_AFTER=$(jq -r '.created_after' "$CONFIG_FILE")

# Convert JSON array to PostgreSQL ARRAY format
# e.g., ["ref-001","ref-002"] -> ARRAY['ref-001', 'ref-002']
SENDER_REFS_ARRAY=$(echo "$SENDER_REFS_JSON" | jq -r '. | map("'\''" + . + "'\''") | "ARRAY[" + join(", ") + "]"')

echo ""
echo "Configuration loaded:"
echo "  Sender references: $SENDER_REFS_ARRAY"
echo "  Creator name: $CREATOR_NAME"
echo "  Created after: $CREATED_AFTER"
echo ""

# Function to safely replace text in file using sed (macOS and Linux compatible)
safe_sed() {
    local pattern=$1
    local file=$2

    # Use different sed syntax for macOS vs Linux
    if [[ "$OSTYPE" == "darwin"* ]]; then
        # macOS
        sed -i '' -e "$pattern" "$file"
    else
        # Linux
        sed -i -e "$pattern" "$file"
    fi
}

echo "Updating SQL scripts..."

# Update analyze-orders-for-cancellation.sql
echo "  Updating analyze-orders-for-cancellation.sql..."

# Escape special characters for sed
SENDER_REFS_ESCAPED=$(echo "$SENDER_REFS_ARRAY" | sed 's/[&/\]/\\&/g')
CREATOR_NAME_ESCAPED=$(echo "$CREATOR_NAME" | sed 's/[&/\]/\\&/g')
CREATED_AFTER_ESCAPED=$(echo "$CREATED_AFTER" | sed 's/[&/\]/\\&/g')

# Update DO block variables
safe_sed "s/v_sendersreferences text\[\] := ARRAY\[.*\];/v_sendersreferences text[] := $SENDER_REFS_ESCAPED;/" "$ANALYZE_SCRIPT"
safe_sed "s/v_creatorname text := '.*';/v_creatorname text := '$CREATOR_NAME_ESCAPED';/" "$ANALYZE_SCRIPT"
safe_sed "s/v_created_after timestamptz := '.*'::timestamptz;/v_created_after timestamptz := '$CREATED_AFTER_ESCAPED'::timestamptz;/" "$ANALYZE_SCRIPT"

# Update standalone queries
safe_sed "s/WHERE o\.sendersreference = ANY(ARRAY\[.*\])  -- UPDATE THIS/WHERE o.sendersreference = ANY($SENDER_REFS_ESCAPED)  -- UPDATE THIS/" "$ANALYZE_SCRIPT"
safe_sed "s/AND o\.creatorname = '.*'  -- UPDATE THIS/AND o.creatorname = '$CREATOR_NAME_ESCAPED'  -- UPDATE THIS/" "$ANALYZE_SCRIPT"
safe_sed "s/AND o\.created >= '.*'::timestamptz  -- UPDATE THIS/AND o.created >= '$CREATED_AFTER_ESCAPED'::timestamptz  -- UPDATE THIS/" "$ANALYZE_SCRIPT"

echo "  ✓ Updated: analyze-orders-for-cancellation.sql"

# Update cancel-orders-by-sendersreferences.sql
echo "  Updating cancel-orders-by-sendersreferences.sql..."

# Update DO block variables
safe_sed "s/v_sendersreferences text\[\] := ARRAY\[.*\];  -- UPDATE: Your sender references/v_sendersreferences text[] := $SENDER_REFS_ESCAPED;  -- UPDATE: Your sender references/" "$CANCEL_SCRIPT"
safe_sed "s/v_creatorname text := '.*';  -- UPDATE: Your creator name/v_creatorname text := '$CREATOR_NAME_ESCAPED';  -- UPDATE: Your creator name/" "$CANCEL_SCRIPT"
safe_sed "s/v_created_after timestamptz := '.*'::timestamptz;  -- UPDATE: Only consider orders created after this date/v_created_after timestamptz := '$CREATED_AFTER_ESCAPED'::timestamptz;  -- UPDATE: Only consider orders created after this date/" "$CANCEL_SCRIPT"

# Update function calls
safe_sed "s/ARRAY\[.*\],  -- UPDATE: Your sender references/$SENDER_REFS_ESCAPED,  -- UPDATE: Your sender references/" "$CANCEL_SCRIPT"
safe_sed "s/'.*',  -- UPDATE: Your creator name/'$CREATOR_NAME_ESCAPED',  -- UPDATE: Your creator name/" "$CANCEL_SCRIPT"
safe_sed "s/'.*'::timestamptz  -- UPDATE: Your time window/'$CREATED_AFTER_ESCAPED'::timestamptz  -- UPDATE: Your time window/" "$CANCEL_SCRIPT"

echo "  ✓ Updated: cancel-orders-by-sendersreferences.sql"

echo ""
echo "✅ Both scripts have been updated successfully!"
echo ""
echo "Next steps:"
echo "  1. Review the updated SQL scripts"
echo "  2. Run analyze-orders-for-cancellation.sql in pgAdmin"
echo "  3. If results look correct, run cancel-orders-by-sendersreferences.sql"
