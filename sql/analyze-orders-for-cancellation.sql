-- =====================================================================
-- ANALYSIS SCRIPT: Preview Orders for Cancellation by Sender References
-- =====================================================================
--
-- Purpose: Analyze which orders would be affected by cancellation
--          without actually canceling them.
--
-- Usage:
--   1. Update the variables in the DO block below
--   2. Run this script to see affected orders
--   3. Review the results before running the actual cancellation script
--
-- =====================================================================

DO $$
DECLARE
    -- ============ CONFIGURATION - UPDATE THESE VALUES ============
    v_sendersreferences text[] := ARRAY['ref-001', 'ref-002', 'ref-003']; -- Add your sender references here
    v_creatorname text := 'ttd';  -- Update with the creator name
    v_created_after timestamptz := '2025-12-01 00:00:00+00'::timestamptz;  -- Only consider orders created after this date
    -- =============================================================

    v_total_matched int;
    v_can_cancel int;
    v_already_cancelled int;
    v_cannot_cancel int;
BEGIN
    -- Display configuration
    RAISE NOTICE '========================================';
    RAISE NOTICE 'CANCELLATION ANALYSIS';
    RAISE NOTICE '========================================';
    RAISE NOTICE 'Creator Name: %', v_creatorname;
    RAISE NOTICE 'Sender References: %', array_to_string(v_sendersreferences, ', ');
    RAISE NOTICE 'Time Window: Only orders created after %', v_created_after;
    RAISE NOTICE '';

    -- Count totals
    SELECT COUNT(*) INTO v_total_matched
    FROM notifications.orders o
    WHERE o.sendersreference = ANY(v_sendersreferences)
      AND o.creatorname = v_creatorname
      AND o.created >= v_created_after;

    RAISE NOTICE 'Total orders matched: %', v_total_matched;
    RAISE NOTICE '';

    IF v_total_matched = 0 THEN
        RAISE NOTICE 'No orders found matching the provided sender references and creator name.';
        RETURN;
    END IF;

    -- Count by cancellation status
    SELECT
        COUNT(*) FILTER (WHERE processedstatus = 'Cancelled'),
        COUNT(*) FILTER (WHERE requestedsendtime > NOW() + INTERVAL '5 minutes'
                         AND processedstatus = 'Registered'),
        COUNT(*) FILTER (WHERE NOT (processedstatus = 'Cancelled' OR
                         (requestedsendtime > NOW() + INTERVAL '5 minutes'
                          AND processedstatus = 'Registered')))
    INTO v_already_cancelled, v_can_cancel, v_cannot_cancel
    FROM notifications.orders o
    WHERE o.sendersreference = ANY(v_sendersreferences)
      AND o.creatorname = v_creatorname
      AND o.created >= v_created_after;

    RAISE NOTICE '----------------------------------------';
    RAISE NOTICE 'SUMMARY';
    RAISE NOTICE '----------------------------------------';
    RAISE NOTICE 'Already cancelled:     %', v_already_cancelled;
    RAISE NOTICE 'Can be cancelled:      %', v_can_cancel;
    RAISE NOTICE 'Cannot be cancelled:   %', v_cannot_cancel;
    RAISE NOTICE '';

    -- Display detailed results
    RAISE NOTICE '----------------------------------------';
    RAISE NOTICE 'DETAILED RESULTS';
    RAISE NOTICE '----------------------------------------';
END $$;

-- Show detailed order information
SELECT
    o.sendersreference,
    o.alternateid,
    o.processedstatus,
    o.requestedsendtime,
    o.created,
    CASE
        WHEN o.processedstatus = 'Cancelled' THEN 'Already cancelled'
        WHEN o.requestedsendtime > NOW() + INTERVAL '5 minutes'
             AND o.processedstatus = 'Registered' THEN 'Can be cancelled'
        ELSE 'Cannot be cancelled (too close to send time or already processing)'
    END AS cancellation_status,
    CASE
        WHEN o.requestedsendtime <= NOW() + INTERVAL '5 minutes'
        THEN 'Send time is within 5 minutes'
        WHEN o.processedstatus != 'Registered' AND o.processedstatus != 'Cancelled'
        THEN 'Status is ' || o.processedstatus::text
        ELSE NULL
    END AS reason_if_cannot_cancel,
    -- Show notification counts
    (SELECT COUNT(*) FROM notifications.emailnotifications e WHERE e._orderid = o._id) as email_count,
    (SELECT COUNT(*) FROM notifications.smsnotifications s WHERE s._orderid = o._id) as sms_count
FROM notifications.orders o
WHERE o.sendersreference = ANY(ARRAY['ref-001', 'ref-002', 'ref-003'])  -- UPDATE THIS to match your sender references
  AND o.creatorname = 'ttd'  -- UPDATE THIS to match your creator name
  AND o.created >= '2025-12-01 00:00:00+00'::timestamptz  -- UPDATE THIS to match your time window
ORDER BY
    CASE
        WHEN o.processedstatus = 'Cancelled' THEN 1
        WHEN o.requestedsendtime > NOW() + INTERVAL '5 minutes'
             AND o.processedstatus = 'Registered' THEN 2
        ELSE 3
    END,
    o.sendersreference;

-- Show notification details for matched orders
\echo '\n=== Email Notifications for Matched Orders ===\n'
SELECT
    o.sendersreference,
    e.alternateid as email_id,
    e.toaddress,
    e.result as email_status,
    e.resulttime
FROM notifications.orders o
JOIN notifications.emailnotifications e ON e._orderid = o._id
WHERE o.sendersreference = ANY(ARRAY['ref-001', 'ref-002', 'ref-003'])  -- UPDATE THIS to match your sender references
  AND o.creatorname = 'ttd'  -- UPDATE THIS to match your creator name
  AND o.created >= '2025-12-01 00:00:00+00'::timestamptz  -- UPDATE THIS to match your time window
ORDER BY o.sendersreference, e._id;

\echo '\n=== SMS Notifications for Matched Orders ===\n'
SELECT
    o.sendersreference,
    s.alternateid as sms_id,
    s.mobilenumber,
    s.result as sms_status,
    s.resulttime
FROM notifications.orders o
JOIN notifications.smsnotifications s ON s._orderid = o._id
WHERE o.sendersreference = ANY(ARRAY['ref-001', 'ref-002', 'ref-003'])  -- UPDATE THIS to match your sender references
  AND o.creatorname = 'ttd'  -- UPDATE THIS to match your creator name
  AND o.created >= '2025-12-01 00:00:00+00'::timestamptz  -- UPDATE THIS to match your time window
ORDER BY o.sendersreference, s._id;
