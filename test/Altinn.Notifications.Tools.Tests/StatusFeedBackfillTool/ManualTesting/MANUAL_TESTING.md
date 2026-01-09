# Manual Testing Guide - Status Feed Backfill Tool

This guide explains how to manually test the Status Feed Backfill Tool using test data created by the `ManualTestSetup` class.

## Overview

The manual test setup creates diverse test scenarios to verify the backfill tool's discovery and insertion logic:
- **2 SendConditionNotMet** orders WITHOUT status feed (SHOULD be found by tool)
- **1 SendConditionNotMet** order WITH status feed (should NOT be found - already has entry)
- **2 Processing** orders WITHOUT status feed (should NOT be found - not in final state)
  - Different email results: "Sending", "New"
- **3 Completed** orders WITH status feed entries (should NOT be found - already have entries)
  - Different email results: "Delivered", "Failed", "Failed_Bounced"
- **2 Completed** orders WITHOUT status feed (SHOULD be found by tool)

**Total: 10 test orders** (4 should be found, 6 should be skipped)

---

## Step 1: Enable Manual Tests

1. Open `ManualTestSetup.cs`
2. Change the flag at the top of the class:
   ```csharp
   // Set to true to enable these tests for manual execution
   private const bool _enableManualTests = true;  // Change from false to true
   ```
3. Save the file

---

## Step 2: Create Test Data

Run the test to create test data:

```bash
cd test/Altinn.Notifications.Tools.Tests
dotnet test --filter "FullyQualifiedName~CreateTestOrders_WithoutStatusFeed_ForManualTesting"
```

The test will output the Order IDs that should/should not be found.

---

## Step 3: Verify Test Data in Database

Use this query in pgAdmin to verify the test data was created correctly:

```sql
SELECT
	O._ID,
	O.ALTERNATEID,
	O.PROCESSEDSTATUS,
	EN._ID AS EMAIL_ID,
	EN.TOADDRESS,
	EN.RESULT,
	SF._ID AS STATUSFEED_ID,
	SF.CREATED AS STATUSFEED_CREATED,
	SF.ORDERSTATUS
FROM
	NOTIFICATIONS.ORDERS O
	LEFT JOIN NOTIFICATIONS.EMAILNOTIFICATIONS EN ON O._ID = EN._ORDERID
	LEFT JOIN NOTIFICATIONS.STATUSFEED SF ON O._ID = SF.ORDERID
WHERE
	O.CREATORNAME = 'ttd'
ORDER BY
	O.PROCESSEDSTATUS,
	O.CREATED DESC;
```

**Expected results:**
- Orders with `processedstatus = 'SendConditionNotMet'` or `'Completed'` and `statusfeed_id IS NULL` → **Should be found by tool**
- Orders with `statusfeed_id IS NOT NULL` → **Should NOT be found (already have entries)**
- Orders with `processedstatus = 'Processing'` → **Should NOT be found (not final state)**

---

## Step 4: Run Discovery Mode

Navigate to the tool and run discovery:

```bash
cd ../../src/Altinn.Notifications.Tools/StatusFeedBackfillTool
dotnet run
```

1. Select option: **1 (Discover)**
2. Leave all filters empty (press Enter for each)
3. Check the output file:

```bash
cat affected-orders.json
```

**Verify:**
- The file contains exactly **4 order IDs** (the ones without status feed)
- The file does NOT contain the 6 orders that already have entries or are in Processing state

---

## Step 5: Run Backfill Mode (Dry Run)

First, ensure DryRun is enabled in `appsettings.json`:

```json
"StatusFeedBackfillConfig": {
  "DryRun": true,
  // ... other settings
}
```

Then run the tool:

```bash
dotnet run
```

1. Select option: **2 (Backfill)**
2. Verify the output shows what would be inserted (without actually inserting)

---

## Step 6: Run Backfill Mode (Insert)

Change DryRun to false in `appsettings.json`:

```json
"StatusFeedBackfillConfig": {
  "DryRun": false,  // Change from true to false
  // ... other settings
}
```

Then run the tool:

```bash
dotnet run
```

1. Select option: **2 (Backfill)**
2. Confirm the insertion when prompted

---

## Step 7: Verify Status Feed Entries Created

Re-run the SQL query from Step 3. Verify that backfill entries were created correctly.

```sql
SELECT
	O._ID,
	O.ALTERNATEID,
	O.PROCESSEDSTATUS,
	EN._ID AS EMAIL_ID,
	EN.TOADDRESS,
	EN.RESULT,
	SF._ID AS STATUSFEED_ID,
	SF.CREATED AS STATUSFEED_CREATED,
	SF.ORDERSTATUS
FROM
	NOTIFICATIONS.ORDERS O
	LEFT JOIN NOTIFICATIONS.EMAILNOTIFICATIONS EN ON O._ID = EN._ORDERID
	LEFT JOIN NOTIFICATIONS.STATUSFEED SF ON O._ID = SF.ORDERID
WHERE
	O.CREATORNAME = 'ttd'
ORDER BY
	O.PROCESSEDSTATUS,
	O.CREATED DESC;
```

**Expected:** 
- **8 orders** with `statusfeed_id IS NOT NULL` (4 of them backfilled)
- **2 Processing orders** with `statusfeed_id IS NULL` (not backfilled - not in final state)

---

## Step 8: Cleanup Test Data

When you're done testing, clean up the test data:

```bash
cd ../../../test/Altinn.Notifications.Tools.Tests
dotnet test --filter "FullyQualifiedName~Cleanup_ManualTestData"
```

This will delete all test orders and status feed entries created by the manual tests.

---

## Step 9: Disable Manual Tests

1. Open `ManualTestSetup.cs`
2. Change the flag back:
   ```csharp
   private const bool _enableManualTests = false;  // Change back to false
   ```
3. Save the file

This ensures the manual tests won't interfere with normal test runs.

---

## Useful SQL Queries

### Manually delete test data (if cleanup test fails)
```sql
DELETE FROM notifications.statusfeed WHERE creatorname = 'ttd';
DELETE FROM notifications.orders WHERE creatorname = 'ttd';
```

---

## Troubleshooting

**Test is skipped even with flag = true:**
- Make sure you saved the file after changing the flag
- Rebuild the project: `dotnet build`

**No orders found in database:**
- Verify you ran the CreateTestOrders test successfully
- Check the test output for any errors

**Tool finds wrong orders:**
- Verify the SQL query shows the expected mix of orders with/without status feed
- Check the tool's discovery logic matches the expected criteria
