-- FUNCTION: notifications.get_metrics_v2(integer, integer)
CREATE OR REPLACE FUNCTION notifications.get_metrics_v2(
	month_input integer,
	year_input integer)
    RETURNS TABLE(org text, placed_orders bigint, sent_emails bigint, succeeded_emails bigint, sent_sms bigint, succeeded_sms bigint) 
    LANGUAGE 'plpgsql'
    COST 100
    VOLATILE PARALLEL UNSAFE
    ROWS 1000

AS $BODY$

BEGIN

  RETURN QUERY
    WITH email_per_order AS (
        SELECT
            e._orderid,
            COUNT(*)::bigint AS sent_emails,
            COALESCE(SUM(CASE WHEN e.result IN ('Delivered', 'Succeeded') THEN 1 ELSE 0 END), 0)::bigint AS succeeded_emails
        FROM notifications.emailnotifications e
        GROUP BY e._orderid
    ),
    sms_per_order AS (
        SELECT
            s._orderid,
            COALESCE(SUM(s.smscount), 0)::bigint AS sent_sms,
            COALESCE(SUM(CASE WHEN s.result IN ('Delivered', 'Accepted') THEN 1 ELSE 0 END), 0)::bigint AS succeeded_sms
        FROM notifications.smsnotifications s
        GROUP BY s._orderid
    )
    SELECT
        o.creatorname AS org,
        COUNT(DISTINCT o._id) AS placed_orders,
        COALESCE(SUM(eo.sent_emails), 0) AS sent_emails,
        COALESCE(SUM(eo.succeeded_emails), 0) AS succeeded_emails,
        COALESCE(SUM(so.sent_sms), 0) AS sent_sms,
        COALESCE(SUM(so.succeeded_sms), 0) AS succeeded_sms
    FROM notifications.orders o
    LEFT JOIN email_per_order eo ON eo._orderid = o._id
    LEFT JOIN sms_per_order so ON so._orderid = o._id
    WHERE EXTRACT(MONTH FROM o.requestedsendtime) = month_input
      AND EXTRACT(YEAR  FROM o.requestedsendtime) = year_input
    GROUP BY o.creatorname;
   
END;
$BODY$;

ALTER FUNCTION notifications.get_metrics_v2(integer, integer)
    OWNER TO postgres;
