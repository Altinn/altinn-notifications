# Status Feed Backfill Tool

Tool for backfilling missing status feed entries in the notifications database.

## Usage

Run the tool and select the operation mode interactively:

```bash
cd src/Tools/Altinn.Notifications.Tools.StatusFeedBackfillTool
dotnet run
```

**Interactive Menu:**

```text
Select operation mode:
  1. Discover - Find affected orders and save to file
  2. Backfill - Process orders from file and insert status feed entries
  3. Generate Test Data - Create test orders for manual testing
  4. Cleanup Test Data - Remove all test orders
  5. Exit

Enter choice (1-5):
```

## Two-Step Workflow

### Step 1: Discover Mode (Option 1)

Find affected orders and save to a file for review.

**Before running:**

- Configure filters in `appsettings.json` (see Configuration section below)

**Run:**

```bash
cd src/Tools/Altinn.Notifications.Tools.StatusFeedBackfillTool
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
    "MinProcessedDateTimeFilter": "2024-12-01T00:00:00Z",
    "OrderProcessingStatusFilter": "Completed"
  }
}
```

### Step 2: Backfill Mode (Option 2)

Process the discovered orders and insert missing status feed entries.

**Before running:**

- Review `affected-orders.json`
- Optionally configure `DryRun` default in `appsettings.json`

**Run:**

```bash
dotnet run
# Choose option 2
# When prompted "Run in DRY RUN mode?", enter y for testing or n for actual insertion
```

**Interactive Dry Run Prompt:**

- The tool prompts you to run in dry run mode
- Default is taken from `appsettings.json` (`BackfillSettings.DryRun`)
- Press Enter to use the default, or type `y`/`n` to override

**DryRun = true**: Simulates processing without database changes. "Would Be Inserted" is an upper bound; actual inserts may be lower due to errors during real execution.
**DryRun = false**: Actually inserts missing status feed entries.

## Complete Workflow Example

```bash
# Navigate to the tool directory
cd src/Tools/Altinn.Notifications.Tools.StatusFeedBackfillTool

# 1. Configure discovery filters in appsettings.json
# Edit: MinProcessedDateTimeFilter = "2024-12-01T00:00:00Z", CreatorNameFilter = "digdir"

# 2. Discover affected orders
dotnet run
# Choose option 1 (Discover)
# Output: affected-orders.json created with 700,000 order IDs

# 3. Review the list (optional)
cat affected-orders.json | jq '. | length'  # Check count
cat affected-orders.json | head -20         # Preview first orders

# 4. Test with dry run
dotnet run
# Choose option 2 (Backfill)
# When prompted "Run in DRY RUN mode?", press Enter or type 'y'
# Output: Shows what would be inserted (no database changes)
# NOTE: In dry run mode, "Would Be Inserted" is an upper bound; actual inserts may be lower due to errors during real execution.

# 5. Run actual backfill
dotnet run
# Choose option 2 (Backfill)
# When prompted "Run in DRY RUN mode?", type 'n'
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

| Setting                       | Type           | Description                                                                                                                                                      |
| ----------------------------- | -------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `OrderIdsFilePath`            | string         | Path to JSON file for storing discovered order IDs (default: "affected-orders.json")                                                                             |
| `MaxOrders`                   | int            | Maximum number of orders to retrieve from discovery query (default: 100)                                                                                         |
| `CreatorNameFilter`           | string\|null   | Filter by creator, e.g., "digdir", or null for all creators                                                                                                      |
| `MinProcessedDateTimeFilter`  | DateTime\|null | Only discover orders processed after this timestamp (ISO 8601 format, e.g., "2024-12-01T14:30:00Z"), or null to use oldest status feed entry date                |
| `OrderProcessingStatusFilter` | enum\|null     | Filter by final status: "Completed" or "SendConditionNotMet", or null for all final statuses. Processing and other non-final statuses are automatically excluded |

### BackfillSettings

| Setting            | Type   | Description                                                                                 |
| ------------------ | ------ | ------------------------------------------------------------------------------------------- |
| `OrderIdsFilePath` | string | Path to JSON file for reading order IDs to process (default: "affected-orders.json")        |
| `DryRun`           | bool   | Default dry run mode. Can be overridden interactively when running backfill (default: true) |

## Database Connection

### Local Development

The default `appsettings.json` is configured for local development. Update the connection string if needed:

```json
{
  "PostgreSQLSettings": {
    "ConnectionString": "Host=localhost;Port=5432;Username=platform_notifications;Password={0};Database=notificationsdb",
    "NotificationsDbPwd": "Password"
  }
}
```

### Targeting Other Environments

To target test, staging, or production environments, use [.NET user secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) to avoid committing credentials:

```bash
# Navigate to the tool directory
cd src/Tools/Altinn.Notifications.Tools.StatusFeedBackfillTool

# Set connection string for target environment
dotnet user-secrets set "PostgreSQLSettings:ConnectionString" "Host=your-host;Port=5432;Username=platform_notifications;Password={0};Database=notificationsdb"
dotnet user-secrets set "PostgreSQLSettings:NotificationsDbPwd" "your-actual-password"

# Verify configuration
dotnet user-secrets list

# Run the tool (user secrets automatically override appsettings.json)
dotnet run
```

User secrets are stored locally and never committed to source control, keeping environment credentials secure.
