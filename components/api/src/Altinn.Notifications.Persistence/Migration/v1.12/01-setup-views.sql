-- https://github.com/Altinn/altinn-notifications/issues/1880
-- v2 of the daily metrics views used by the DWH parquet export (get_email_metrics_v2 / get_sms_metrics_v2).
-- Expand/contract: the v1 views and functions are left untouched so pods from the previous release keep
-- working during the rolling deploy and a rollback stays possible. They are removed in a later release.
--
-- Changes from v1:
--   * shipment ids are exposed as text, and the order shipment id (orders.alternateid) is added
--   * resulttime is exposed
--   * sms: sms count and body lengths are removed, so the join to smstexts is gone
--
-- The views are created WITH NO DATA so the migration stays fast at pod startup.
-- They must be populated (and the refresh jobs scheduled) outside Yuniql, see the block at the end of this file.

CREATE MATERIALIZED VIEW IF NOT EXISTS notifications.email_metrics_recent_v2 AS
SELECT
    email._id AS email_id,
    email.alternateid::text AS email_shipmentid,
    orders.alternateid::text AS order_shipmentid,
    orders.sendersreference AS senders_reference,
    orders.requestedsendtime,
    email.resulttime,
    orders.creatorname,
    orders.notificationorder ->> 'ResourceId' AS resourceid,
    email.result::text AS result,
    email.operationid
FROM notifications.emailnotifications AS email
INNER JOIN notifications.orders orders ON orders._id = email._orderid
WHERE email.resulttime >= now() - interval '2 days'
  AND email.result NOT IN ('New', 'Sending', 'Succeeded')
WITH NO DATA;

CREATE UNIQUE INDEX IF NOT EXISTS email_metrics_recent_v2_email_id_idx
    ON notifications.email_metrics_recent_v2 (email_id);

CREATE INDEX IF NOT EXISTS email_metrics_recent_v2_resulttime_idx
    ON notifications.email_metrics_recent_v2 (resulttime);

CREATE MATERIALIZED VIEW IF NOT EXISTS notifications.sms_metrics_recent_v2 AS
SELECT
    sms._id AS sms_id,
    sms.alternateid::text AS sms_shipmentid,
    orders.alternateid::text AS order_shipmentid,
    orders.sendersreference AS senders_reference,
    orders.requestedsendtime,
    sms.resulttime,
    orders.creatorname,
    orders.notificationorder ->> 'ResourceId' AS resourceid,
    sms.result::text AS result,
    sms.gatewayreference,
    CASE 
        WHEN sms.mobilenumber IS NULL OR sms.mobilenumber = '' THEN 'n/a'
        WHEN sms.mobilenumber ~ '^(\+|00) *47' THEN 'innland'
        ELSE 'utland'
    END AS rate,
    left(sms.mobilenumber, 4) AS mobilenumber_prefix
FROM notifications.smsnotifications AS sms
INNER JOIN notifications.orders orders ON orders._id = sms._orderid
WHERE sms.resulttime >= now() - interval '2 days'
  AND sms.result NOT IN ('New', 'Sending', 'Accepted')
WITH NO DATA;

CREATE UNIQUE INDEX IF NOT EXISTS sms_metrics_recent_v2_sms_id_idx
    ON notifications.sms_metrics_recent_v2 (sms_id);

CREATE INDEX IF NOT EXISTS sms_metrics_recent_v2_resulttime_idx
    ON notifications.sms_metrics_recent_v2 (resulttime);

GRANT SELECT ON notifications.email_metrics_recent_v2 TO platform_notifications;
GRANT SELECT ON notifications.sms_metrics_recent_v2 TO platform_notifications;

-- Run manually in each environment after this migration has been applied, by someone with pg_cron rights.
-- The initial populate must be non-concurrent (REFRESH ... CONCURRENTLY requires a populated view), and must
-- be done before the DWH export calls the new endpoint version (until then get_*_metrics_v2 fails with
-- "materialized view ... has not been populated").
/*
-- Initial populate
REFRESH MATERIALIZED VIEW notifications.email_metrics_recent_v2;
REFRESH MATERIALIZED VIEW notifications.sms_metrics_recent_v2;

-- Schedule refresh jobs (idempotent: unschedule existing job with the same name first)
SELECT cron.unschedule(jobid) FROM cron.job WHERE jobname = 'refresh_email_metrics_recent_v2';
SELECT cron.schedule_in_database(
    'refresh_email_metrics_recent_v2',
    '5 0 * * *',
    $$REFRESH MATERIALIZED VIEW CONCURRENTLY notifications.email_metrics_recent_v2$$,
    'notificationsdb'
);

SELECT cron.unschedule(jobid) FROM cron.job WHERE jobname = 'refresh_sms_metrics_recent_v2';
SELECT cron.schedule_in_database(
    'refresh_sms_metrics_recent_v2',
    '10 0 * * *',
    $$REFRESH MATERIALIZED VIEW CONCURRENTLY notifications.sms_metrics_recent_v2$$,
    'notificationsdb'
);
*/
