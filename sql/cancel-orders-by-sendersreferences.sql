-- =====================================================================
-- CANCELLATION SCRIPT: Cancel Orders by Sender References
-- =====================================================================
--
-- Purpose: Cancel notification orders identified by their sender references
--
-- Prerequisites:
--   1. Run analyze-orders-for-cancellation.sql first to preview changes
--   2. Verify that the orders to be cancelled are correct
--   3. Have appropriate database permissions
--
-- Usage:
--   1. Update the configuration variables in the transaction block below
--   2. Run this script
--   3. Review the results shown
--   4. Decide whether to COMMIT or ROLLBACK
--   5. Uncomment either COMMIT or ROLLBACK at the end
--
-- Safety:
--   - This script is wrapped in a transaction
--   - By default, nothing is committed (you must explicitly COMMIT)
--   - If you close the connection or encounter an error, changes are rolled back
--
-- =====================================================================

BEGIN; -- Start transaction

DO $$ BEGIN RAISE NOTICE ''; RAISE NOTICE '=== TRANSACTION STARTED ==='; RAISE NOTICE ''; END $$;

-- Step 1: Create temporary function (inside transaction)
-- =====================================================================
CREATE OR REPLACE FUNCTION notifications.cancelordersbysendersreferences(
    _sendersreferences text[],
    _creatorname text,
    _created_after timestamptz
)
RETURNS TABLE(
    sendersreference text,
    alternateid uuid,
    cancelallowed boolean,
    processedstatus orderprocessingstate,
    requestedsendtime timestamp with time zone,
    message text
)
LANGUAGE plpgsql
AS $$
BEGIN
    -- Validate inputs
    IF _sendersreferences IS NULL OR array_length(_sendersreferences, 1) IS NULL THEN
        RAISE EXCEPTION 'Sender references array cannot be null or empty';
    END IF;

    IF _creatorname IS NULL OR LENGTH(TRIM(_creatorname)) = 0 THEN
        RAISE EXCEPTION 'Creator name cannot be null or empty';
    END IF;

    IF _created_after IS NULL THEN
        RAISE EXCEPTION 'Created after timestamp cannot be null';
    END IF;

    -- Return query that processes all matching orders
    RETURN QUERY
    WITH matching_orders AS (
        -- Find all orders that match the sender references and creator
        SELECT
            o._id,
            o.alternateid,
            o.sendersreference,
            o.requestedsendtime,
            o.processedstatus,
            o.creatorname
        FROM notifications.orders o
        WHERE o.sendersreference = ANY(_sendersreferences)
          AND o.creatorname = _creatorname
          AND o.created >= _created_after
    ),
    classified_orders AS (
        -- Classify each order based on cancellation rules
        SELECT
            mo._id,
            mo.alternateid,
            mo.sendersreference,
            mo.requestedsendtime,
            mo.processedstatus,
            CASE
                -- Already cancelled
                WHEN mo.processedstatus = 'Cancelled' THEN 'already_cancelled'
                -- Can be cancelled (same rules as cancelorder function)
                WHEN mo.requestedsendtime > NOW() + INTERVAL '5 minutes'
                     AND mo.processedstatus = 'Registered' THEN 'can_cancel'
                -- Cannot be cancelled (too close to send time or not in correct status)
                ELSE 'cannot_cancel'
            END AS cancel_status
        FROM matching_orders mo
    ),
    updated_orders AS (
        -- Update orders that can be cancelled
        UPDATE notifications.orders o
        SET processedstatus = 'Cancelled', processed = NOW()
        FROM classified_orders co
        WHERE o._id = co._id
          AND co.cancel_status = 'can_cancel'
        RETURNING o._id, o.alternateid, o.sendersreference, o.requestedsendtime, o.processedstatus
    )
    -- Return results for all matched orders
    SELECT
        co.sendersreference,
        co.alternateid,
        CASE
            WHEN co.cancel_status = 'can_cancel' THEN TRUE
            WHEN co.cancel_status = 'already_cancelled' THEN TRUE
            ELSE FALSE
        END AS cancelallowed,
        COALESCE(uo.processedstatus, co.processedstatus) AS processedstatus,
        co.requestedsendtime,
        CASE
            WHEN co.cancel_status = 'can_cancel' THEN 'Order cancelled successfully'
            WHEN co.cancel_status = 'already_cancelled' THEN 'Order was already cancelled'
            ELSE 'Order cannot be cancelled - too close to send time or already processing'
        END AS message
    FROM classified_orders co
    LEFT JOIN updated_orders uo ON co._id = uo._id;
END;
$$;

DO $$ BEGIN RAISE NOTICE 'Temporary function created (will be dropped before commit/rollback)'; RAISE NOTICE ''; END $$;

-- Step 2: Execute Cancellation
-- =====================================================================

-- ============ CONFIGURATION - UPDATE THESE VALUES ============
DO $$
DECLARE
    v_sendersreferences text[] := ARRAY['ref-001', 'ref-002', 'ref-003']; -- UPDATE: Your sender references
    v_creatorname text := 'ttd';  -- UPDATE: Your creator name
    v_created_after timestamptz := '2025-12-01 00:00:00+00'::timestamptz;  -- UPDATE: Only consider orders created after this date
BEGIN
    RAISE NOTICE '========================================';
    RAISE NOTICE 'EXECUTING CANCELLATION';
    RAISE NOTICE '========================================';
    RAISE NOTICE 'Creator Name: %', v_creatorname;
    RAISE NOTICE 'Sender References: %', array_to_string(v_sendersreferences, ', ');
    RAISE NOTICE 'Time Window: Only orders created after %', v_created_after;
    RAISE NOTICE '';
END $$;
-- =============================================================

-- Execute the cancellation function with your parameters
SELECT
    sendersreference,
    alternateid,
    cancelallowed,
    processedstatus,
    requestedsendtime,
    message
FROM notifications.cancelordersbysendersreferences(
    ARRAY['ref-001', 'ref-002', 'ref-003'],  -- UPDATE: Your sender references
    'ttd',  -- UPDATE: Your creator name
    '2025-12-01 00:00:00+00'::timestamptz  -- UPDATE: Your time window
)
ORDER BY cancelallowed DESC, sendersreference;

-- Show summary statistics
DO $$ BEGIN RAISE NOTICE ''; RAISE NOTICE '=== SUMMARY ==='; RAISE NOTICE ''; END $$;

SELECT
    COUNT(*) as total_processed,
    COUNT(*) FILTER (WHERE cancelallowed = true) as successfully_cancelled,
    COUNT(*) FILTER (WHERE cancelallowed = false) as could_not_cancel
FROM notifications.cancelordersbysendersreferences(
    ARRAY['ref-001', 'ref-002', 'ref-003'],  -- UPDATE: Your sender references
    'ttd',  -- UPDATE: Your creator name
    '2025-12-01 00:00:00+00'::timestamptz  -- UPDATE: Your time window
);

DO $$
BEGIN
    RAISE NOTICE '';
    RAISE NOTICE '========================================';
    RAISE NOTICE 'REVIEW THE RESULTS ABOVE';
    RAISE NOTICE '========================================';
    RAISE NOTICE '';
END $$;

-- Step 3: Clean up temporary function
-- =====================================================================
DROP FUNCTION IF EXISTS notifications.cancelordersbysendersreferences;

DO $$
BEGIN
    RAISE NOTICE 'Temporary function dropped';
    RAISE NOTICE '';
    RAISE NOTICE 'If the results look correct, run: COMMIT;';
    RAISE NOTICE 'If you want to undo the changes, run: ROLLBACK;';
    RAISE NOTICE '';
    RAISE NOTICE 'IMPORTANT: The transaction is still open.';
    RAISE NOTICE 'No changes have been saved to the database yet.';
    RAISE NOTICE '';
END $$;

-- Step 4: Decision Point
-- =====================================================================
-- Uncomment ONE of the following lines after reviewing the results:

-- COMMIT;     -- Uncomment this line to save the changes (function is already dropped)
-- ROLLBACK;   -- Uncomment this line to undo all changes (function will be rolled back)

-- =====================================================================
-- NOTES:
-- =====================================================================
-- - If you close your database connection without committing, all changes
--   will be automatically rolled back
-- - After COMMIT, the cancellations are permanent and cannot be undone
-- - You can run the analysis script again after COMMIT to verify
-- =====================================================================
