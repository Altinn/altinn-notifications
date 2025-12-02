# SQL Scripts for Manual Database Operations

This folder contains SQL scripts for manual database operations that need to be run independently in specific environments (test, staging, production).

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

```bash
psql -h <host> -U <user> -d <database> -f analyze-orders-for-cancellation.sql
```

Before running:
1. Open `analyze-orders-for-cancellation.sql`
2. Update the configuration variables:
   - `v_sendersreferences`: Array of sender reference strings
   - `v_creatorname`: The creator name (service owner)

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

```bash
psql -h <host> -U <user> -d <database> -f cancel-orders-by-sendersreferences.sql
```

Before running:
1. Open `cancel-orders-by-sendersreferences.sql`
2. Update the configuration variables (in multiple places):
   - `v_sendersreferences`: Same array as in analysis script
   - `v_creatorname`: Same creator name as in analysis script

The script will:
1. Start a transaction
2. Create a temporary function (only exists within the transaction)
3. Execute the cancellation
4. Show the results
5. Drop the temporary function
6. Wait for you to COMMIT or ROLLBACK

#### Step 3: Review and Commit

After running the cancellation script:
1. Review the results shown in the output
2. If correct, run: `COMMIT;`
3. If incorrect, run: `ROLLBACK;`

**Important:** The transaction remains open until you explicitly commit or rollback. If you close the connection without committing, all changes will be rolled back automatically.

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

1. Edit `analyze-orders-for-cancellation.sql`:
```sql
v_sendersreferences text[] := ARRAY['test-ref-001', 'test-ref-002', 'test-ref-003'];
v_creatorname text := 'ttd';
```

2. Run analysis:
```bash
psql -h testdb.example.com -U notifications_admin -d notificationsdb -f analyze-orders-for-cancellation.sql
```

3. Review output and verify it's correct

4. Edit `cancel-orders-by-sendersreferences.sql` with same values

5. Run cancellation:
```bash
psql -h testdb.example.com -U notifications_admin -d notificationsdb -f cancel-orders-by-sendersreferences.sql
```

6. Review the results

7. In the psql session:
```sql
-- If everything looks good:
COMMIT;

-- Or if you want to undo:
ROLLBACK;
```

### Function Details

The function `notifications.cancelordersbysendersreferences` is created by the cancellation script. It:
- Takes an array of sender references and a creator name
- Finds all matching orders
- Applies cancellation rules
- Updates orders that can be cancelled
- Returns detailed results for each order

**Function signature:**
```sql
notifications.cancelordersbysendersreferences(
    _sendersreferences text[],  -- Array of sender references
    _creatorname text           -- Creator name for authorization
)
```

**Returns:**
- `sendersreference`: The sender reference
- `alternateid`: The order UUID
- `cancelallowed`: TRUE if cancelled/already cancelled, FALSE if cannot cancel
- `processedstatus`: Current processing status
- `requestedsendtime`: Scheduled send time
- `message`: Human-readable result message

### Troubleshooting

**No orders found:**
- Verify sender references are correct
- Check that creator name matches the order owner
- Ensure orders exist in the database

**Cannot cancel orders:**
- Check if send time is within 5 minutes
- Verify order status is 'Registered'
- Orders in 'Processing', 'Processed', or 'Completed' status cannot be cancelled

**Permission denied:**
- Ensure database user has UPDATE permissions on `notifications.orders`
- Ensure database user can create functions

### Environment-Specific Considerations

#### Test Environment
- Safe to experiment
- Can test the full workflow

#### Staging Environment
- Should mirror production data
- Use for final validation before production

#### Production Environment
- Always run analysis first
- Double-check sender references
- Have a rollback plan
- Consider running during maintenance windows
- Document the cancellation reason and timestamp

### Maintenance

The function is stored in:
- **Source:** `src/Altinn.Notifications.Persistence/Migration/FunctionsAndProcedures/cancelordersbysendersreferences.sql`
- **Created by:** The cancellation script (includes the function definition)

If the function needs to be updated:
1. Update the source file in `FunctionsAndProcedures/`
2. Update the function definition in `cancel-orders-by-sendersreferences.sql`
3. Test in a non-production environment first
