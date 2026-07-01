-- Email metrics materialized view
CREATE MATERIALIZED VIEW IF NOT EXISTS notifications.email_metrics_recent AS
SELECT
    email._id AS email_id,
    email.alternateid AS shipmentid,
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

CREATE UNIQUE INDEX IF NOT EXISTS email_metrics_recent_email_id_idx
    ON notifications.email_metrics_recent (email_id);

CREATE INDEX IF NOT EXISTS email_metrics_recent_resulttime_idx
    ON notifications.email_metrics_recent (resulttime);

-- SMS metrics materialized view
CREATE MATERIALIZED VIEW IF NOT EXISTS notifications.sms_metrics_recent AS
SELECT
    sms._id AS sms_id,
    sms.alternateid AS shipmentid,
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
    left(sms.mobilenumber, 4) AS mobilenumber_prefix,
    sms.smscount AS altinn_sms_count,
    length(sms.customizedbody) AS altinn_sms_custom_body_length,
    length(sms_text.body) AS altinn_sms_body_length
FROM notifications.smsnotifications AS sms
INNER JOIN notifications.orders orders ON orders._id = sms._orderid
LEFT JOIN notifications.smstexts sms_text ON orders._id = sms_text._orderid
WHERE sms.resulttime >= now() - interval '2 days'
  AND sms.result NOT IN ('New', 'Sending', 'Accepted')
WITH NO DATA;

CREATE UNIQUE INDEX IF NOT EXISTS sms_metrics_recent_sms_id_idx
    ON notifications.sms_metrics_recent (sms_id);

CREATE INDEX IF NOT EXISTS sms_metrics_recent_resulttime_idx
    ON notifications.sms_metrics_recent (resulttime);


-- Grant permissions
GRANT SELECT ON notifications.email_metrics_recent TO platform_notifications;
GRANT SELECT ON notifications.sms_metrics_recent TO platform_notifications;

-- Initial populate
REFRESH MATERIALIZED VIEW notifications.email_metrics_recent;
REFRESH MATERIALIZED VIEW notifications.sms_metrics_recent;