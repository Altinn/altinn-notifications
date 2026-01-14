# Manual Testing Guide - Status Feed Backfill Tool

This guide explains how to manually test the Status Feed Backfill Tool using generated test data.

## Overview

The tool includes test data generation that creates diverse test scenarios to verify the backfill tool's discovery and insertion logic:

- **2 SendConditionNotMet** orders WITHOUT status feed (SHOULD be found by tool)
- **1 SendConditionNotMet** order WITH status feed (should NOT be found - already has entry)
- **2 Processing** orders WITHOUT status feed (should NOT be found - not in final state)
  - Different email results: "Sending", "New"
- **3 Completed** orders WITH status feed entries (should NOT be found - already have entries)
  - Different email results: "Delivered", "Failed", "Failed_Bounced"
- **2 Completed** orders WITHOUT status feed (SHOULD be found by tool)

**Total: 10 test orders** (4 should be found, 6 should be skipped)

---

## Step 1: Generate Test Data

Navigate to the tool directory and run it:

```bash
cd src/Tools/Altinn.Notifications.Tools.StatusFeedBackfillTool
dotnet run
```

Select option: **3 (Generate Test Data)**

The tool will create all test orders and output the Order IDs that should/should not be found.

---

## Step 2: Verify Test Data in Database

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
	AND O.SENDERSREFERENCE LIKE 'backfill-tool-test-%'
ORDER BY
	O.PROCESSEDSTATUS,
	O.CREATED DESC;
```

**Expected results:**

- Orders with `processedstatus = 'SendConditionNotMet'` or `'Completed'` and `statusfeed_id IS NULL` → **Should be found by tool**
- Orders with `statusfeed_id IS NOT NULL` → **Should NOT be found (already have entries)**
- Orders with `processedstatus = 'Processing'` → **Should NOT be found (not final state)**

---

## Step 3: Run Discovery Mode

Run the tool again:

```bash
dotnet run
```

Select option: **1 (Discover)**

The tool will use the filters from `appsettings.json` (DiscoverySettings) and discover affected orders.

Check the output file:

```bash
cat affected-orders.json
```

**Verify:**

- The file contains exactly **4 order IDs** (the ones without status feed)
- The file does NOT contain the 6 orders that already have entries or are in Processing state

---

## Step 4: Run Backfill Mode (Dry Run)

Run the tool:

```bash
dotnet run
```

1. Select option: **2 (Backfill)**
2. When prompted "Run in DRY RUN mode?", enter **y** (or just press Enter for default yes)
3. Verify the output shows what would be inserted (without actually inserting)

---

## Step 5: Run Backfill Mode (Insert)

Run the tool:

```bash
dotnet run
```

1. Select option: **2 (Backfill)**
2. When prompted "Run in DRY RUN mode?", enter **n**
3. Verify the actual insertion happens

---

## Step 6: Verify Status Feed Entries Created

Re-run the SQL query from Step 2. Verify that backfill entries were created correctly.

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
	AND O.SENDERSREFERENCE LIKE 'backfill-tool-test-%'
ORDER BY
	O.PROCESSEDSTATUS,
	O.CREATED DESC;
```

**Expected:**

- **8 orders** with `statusfeed_id IS NOT NULL` (4 of them backfilled)
- **2 Processing orders** with `statusfeed_id IS NULL` (not backfilled - not in final state)

---

## Step 7: Cleanup Test Data

When you're done testing, clean up the test data using the tool:

```bash
dotnet run
```

1. Select option: **4 (Cleanup Test Data)**
2. Confirm the deletion when prompted

This will delete all test orders and status feed entries created by this tool (identified by sender reference prefix 'backfill-tool-test-').

---

## Useful SQL Queries

### Manually delete test data (if cleanup fails)

```sql
DELETE FROM notifications.statusfeed
WHERE orderid IN (
    SELECT _id FROM notifications.orders
    WHERE sendersreference LIKE 'backfill-tool-test-%'
);
DELETE FROM notifications.orders WHERE sendersreference LIKE 'backfill-tool-test-%';
```

---

## Troubleshooting

**No orders found in database:**

- Verify you ran option 3 (Generate Test Data) successfully
- Check the tool output for any errors

**Tool finds wrong orders:**

- Verify the SQL query shows the expected mix of orders with/without status feed
- Check the tool's discovery logic matches the expected criteria
