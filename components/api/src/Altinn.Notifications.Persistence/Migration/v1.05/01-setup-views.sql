-- https://github.com/Altinn/altinn-notifications/issues/1880
-- Adds the order shipment id (orders.alternateid) and result time to the email metrics export.
-- get_email_metrics changes return type, so it must be dropped before it is recreated in 02-functions-and-procedures.sql.
DROP FUNCTION IF EXISTS notifications.get_email_metrics(integer, integer, integer);

DROP MATERIALIZED VIEW IF EXISTS notifications.email_metrics_recent;

-- Populated on creation, so the scheduled REFRESH ... CONCURRENTLY keeps working after the view is recreated.
CREATE MATERIALIZED VIEW notifications.email_metrics_recent AS
SELECT
    email._id AS email_id,
    email.alternateid AS email_shipmentid,
    orders.alternateid AS order_shipmentid,
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
WITH DATA;

CREATE UNIQUE INDEX IF NOT EXISTS email_metrics_recent_email_id_idx
    ON notifications.email_metrics_recent (email_id);

CREATE INDEX IF NOT EXISTS email_metrics_recent_resulttime_idx
    ON notifications.email_metrics_recent (resulttime);

GRANT SELECT ON notifications.email_metrics_recent TO platform_notifications;
