# SQL Scripts for Manual Database Operations

This folder contains SQL scripts for manual database operations that need to be run independently in specific environments (test, staging, production).

**How to use:** Connect to the database using pgAdmin on the VM (image), then copy and execute these scripts in the query tool.

## Cancel Orders by Sender References

### Overview

These scripts allow you to cancel multiple notification orders by providing a list of sender references. This is useful when you need to cancel a batch of notifications without knowing their internal alternate IDs.

### Files

1. **`analyze-orders-for-cancellation.sql`** - Preview script (read-only)
2. **`cancel-orders-by-sendersreferences.sql`** - Cancellation script (writes to database)

### Workflow

Follow these steps in order:

#### Step 1: Analysis (Preview)

Run the analysis script first to see what would be affected:

1. Connect to the database using pgAdmin on the VM (image)
2. Open `analyze-orders-for-cancellation.sql` in an editor
3. Update the configuration variables:
   - `v_sendersreferences`: Array of sender reference strings
   - `v_creatorname`: The creator name (service owner)
4. Copy the entire script content
5. Paste and execute it in pgAdmin's query tool

The script will show:
- Total number of orders matched
- How many can be cancelled
- How many are already cancelled
- How many cannot be cancelled (and why)
- Detailed information for each order
- Associated email and SMS notifications

**Example output:**
```
CANCELLATION ANALYSIS
========================================
Creator Name: ttd
Sender References: ref-001, ref-002, ref-003

Total orders matched: 5

----------------------------------------
SUMMARY
----------------------------------------
Already cancelled:     1
Can be cancelled:      3
Cannot be cancelled:   1
```

#### Step 2: Cancellation (Write)

After reviewing the analysis results, run the cancellation script:

1. Open `cancel-orders-by-sendersreferences.sql` in an editor
2. Update the configuration variables:
   - `v_sendersreferences`: Same array as in analysis script
   - `v_creatorname`: Same creator name as in analysis script
3. Copy the entire script content
4. Paste and execute it in pgAdmin's query tool

The script will:
1. Start a transaction
2. Create a temporary function (only exists within the transaction)
3. Execute the cancellation
4. Show the results
5. Drop the temporary function
6. Wait for you to COMMIT or ROLLBACK

#### Step 3: Review and Commit

After running the cancellation script in pgAdmin:
1. Review the results shown in the output pane
2. In the query tool, execute one of the following:
   - If results look correct: `COMMIT;`
   - If you want to undo: `ROLLBACK;`

**Important:** The transaction remains open until you explicitly commit or rollback. If you close pgAdmin or the connection without committing, all changes will be rolled back automatically.

### Cancellation Rules

Orders can only be cancelled if:
- `requestedsendtime > NOW() + 5 minutes` (more than 5 minutes until scheduled send)
- `processedstatus = 'Registered'` (not yet processing or completed)

Orders cannot be cancelled if:
- Too close to send time (within 5 minutes)
- Already processing
- Already sent
- Already completed

Orders already cancelled will be reported as successfully cancelled (idempotent).

### Safety Features

1. **Read-only analysis**: Preview before making changes
2. **Transaction control**: All changes wrapped in BEGIN/COMMIT
3. **Explicit commit required**: Nothing is saved until you run COMMIT
4. **Auto-rollback**: If connection closes, changes are rolled back
5. **Temporary function**: Function is dropped before commit/rollback (no database pollution)
6. **Authorization**: Only affects orders owned by the specified creator

### Example Usage

#### Scenario: Cancel test notifications

1. Connect to the test database using pgAdmin on the VM

2. Edit `analyze-orders-for-cancellation.sql`:
```sql
v_sendersreferences text[] := ARRAY['test-ref-001', 'test-ref-002', 'test-ref-003'];
v_creatorname text := 'ttd';
```

3. Copy the script and run it in pgAdmin's query tool

4. Review the output and verify the orders to be cancelled are correct

5. Edit `cancel-orders-by-sendersreferences.sql` with the same values

6. Copy and run the cancellation script in pgAdmin

7. Review the results in the output pane

8. Execute in pgAdmin's query tool:
```sql
-- If everything looks good:
COMMIT;

-- Or if you want to undo:
ROLLBACK;
```

### Troubleshooting

**No orders found:**
- Verify sender references are correct
- Check that creator name matches the order owner
- Ensure orders exist in the database

**Cannot cancel orders:**
- Check if send time is within 5 minutes
- Verify order status is 'Registered'
- Orders in 'Processing', 'Processed', or 'Completed' status cannot be cancelled
