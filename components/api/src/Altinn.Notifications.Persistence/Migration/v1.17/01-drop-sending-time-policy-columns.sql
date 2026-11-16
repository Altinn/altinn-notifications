DROP FUNCTION IF EXISTS notifications.claim_anytime_sms_batch_v2(integer);
DROP FUNCTION IF EXISTS notifications.claim_daytime_sms_batch_v2(integer);

DROP FUNCTION IF EXISTS notifications.insertorder_v2(uuid, text, text, timestamp with time zone, timestamp with time zone, jsonb, integer, text, text, bigint);
DROP FUNCTION IF EXISTS notifications.insertorder_v3(uuid, text, text, timestamp with time zone, timestamp with time zone, jsonb, integer, text, text, bigint, integer);

ALTER TABLE notifications.orders
    DROP COLUMN IF EXISTS smssendingtimepolicy,
    DROP COLUMN IF EXISTS emailsendingtimepolicy;
