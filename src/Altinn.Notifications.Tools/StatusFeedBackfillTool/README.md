# Status Feed Backfill Tool

Tool for backfilling missing status feed entries in the notifications database.

## Usage

Run the tool and select the operation mode interactively:

```bash
cd src/Altinn.Notifications.Tools/StatusFeedBackfillTool
dotnet run
```

**Interactive Menu:**
```
Select operation mode:
  1. Discover - Find affected orders and save to file
  2. Backfill - Process orders from file and insert status feed entries
  3. Exit

Enter choice (1-3):
```

## Two-Step Workflow

### Step 1: Discover Mode (Option 1)

Find affected orders and save to a file for review.

**Before running:**
- Configure filters in `appsettings.json` (see Configuration section below)

**Run:**
```bash
cd src/Altinn.Notifications.Tools/StatusFeedBackfillTool
dotnet run
# Choose option 1
```

**Result:** Creates `affected-orders.json` with list of order GUIDs missing status feed entries.

#### Discovery Options

**Configure Filters** (recommended for large-scale backfill)
```json
{
  "DiscoverySettings": {
    "CreatorNameFilter": "ttd",
    "MinProcessedDateFilter": "2025-06-01",
    "OrderProcessingStatusFilter": "Completed"
  }
}
```

### Step 2: Backfill Mode (Option 2)

Process the discovered orders and insert missing status feed entries.

**Before running:**
- Review `affected-orders.json`
- Set `DryRun` to `true` for testing, `false` for actual backfill

**Run:**
```bash
cd src/Altinn.Notifications.Tools/StatusFeedBackfillTool
dotnet run
# Choose option 2
```

**DryRun = true**: Simulates processing without database changes  
**DryRun = false**: Actually inserts missing status feed entries

## Complete Workflow Example

```bash
# Navigate to the tool directory
cd src/Altinn.Notifications.Tools/StatusFeedBackfillTool

# 1. Configure discovery filters in appsettings.json
# Edit: MinProcessedDateFilter = "2025-06-01", CreatorNameFilter = "digdir"

# 2. Discover affected orders
dotnet run
# Choose option 1 (Discover)
# Output: affected-orders.json created with 700,000 order IDs

# 3. Review the list (optional)
cat affected-orders.json | jq '. | length'  # Check count
cat affected-orders.json | head -20         # Preview first orders

# 4. Test with dry run
# Edit appsettings.json: DryRun = true
dotnet run
# Choose option 2 (Backfill)
# Output: Shows what would be inserted (no database changes)

# 5. Run actual backfill
# Edit appsettings.json: DryRun = false
dotnet run
# Choose option 2 (Backfill)
# Output: Inserts missing status feed entries
```

## File Format

The `affected-orders.json` file is a simple JSON array of GUIDs:

```json
[
  "550e8400-e29b-41d4-a716-446655440000",
  "6ba7b810-9dad-11d1-80b4-00c04fd430c8",
  "7c9e6679-7425-40de-944b-e07fc1f90ae7"
]
```

You can manually edit this file to:
- Remove specific orders you don't want to backfill
- Add additional order IDs
- Split into multiple files for batched processing

## Configuration Reference

### DiscoverySettings

| Setting | Type | Description |
|---------|------|-------------|
| `OrderIdsFilePath` | string | Path to JSON file for storing discovered order IDs (default: "affected-orders.json") |
| `MaxOrders` | int | Maximum number of orders to retrieve from discovery query (default: 100) |
| `CreatorNameFilter` | string | Filter by creator, e.g., "digdir" |
| `MinProcessedDateFilter` | DateTime | Only discover orders processed after this date |
| `OrderProcessingStatusFilter` | enum | Filter by final status: either `Completed` or `SendConditionNotMet`. Processing and other non-final statuses are automatically excluded |

### BackfillSettings

| Setting | Type | Description |
|---------|------|-------------|
| `OrderIdsFilePath` | string | Path to JSON file for reading order IDs to process (default: "affected-orders.json") |
| `DryRun` | bool | If true, simulates without database changes (default: true) |

## Database Connection

Update `PostgreSQLSettings.ConnectionString` with your database credentials:

```json
{
  "PostgreSQLSettings": {
    "ConnectionString": "Host=localhost;Port=5432;Username=platform_notifications;Password=your_password;Database=notificationsdb"
  }
}
```
