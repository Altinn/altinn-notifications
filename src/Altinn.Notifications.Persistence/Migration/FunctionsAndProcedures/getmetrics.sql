CREATE OR REPLACE FUNCTION notifications.getmetrics(
    month_input int,
    year_input int
)
RETURNS TABLE (
    org text,
    placed_orders bigint,
    sent_emails bigint,
    succeeded_emails bigint,
    sent_sms bigint,
    succeeded_sms bigint
)
AS $$
BEGIN
    RETURN QUERY
    SELECT
        o.creatorname,
        COUNT(DISTINCT o._id) AS placed_orders,
        SUM(CASE WHEN e._id IS NOT NULL THEN 1 ELSE 0 END) AS sent_emails,
        SUM(CASE WHEN e.result IN ('Delivered', 'Succeeded') THEN 1 ELSE 0 END) AS succeeded_emails, 
        SUM(CASE WHEN s._id IS NOT NULL THEN s.smscount ELSE 0 END) AS sent_sms,
        SUM(CASE WHEN s.result = 'Accepted' THEN 1 ELSE 0 END) AS succeeded_sms
    FROM notifications.orders o
    LEFT JOIN notifications.emailnotifications e ON o._id = e._orderid
    LEFT JOIN notifications.smsnotifications s ON o._id = s._orderid
    WHERE EXTRACT(MONTH FROM o.requestedsendtime) = month_input
        AND EXTRACT(YEAR FROM o.requestedsendtime) = year_input
    GROUP BY o.creatorname;
END;
$$ LANGUAGE plpgsql;

-- FUNCTION: notifications.get_metrics_v2(integer, integer)
CREATE OR REPLACE FUNCTION notifications.get_metrics_v2(
	month_input integer,
	year_input integer)
    RETURNS TABLE(org text, placed_orders bigint, sent_emails bigint, succeeded_emails bigint, sent_sms bigint, succeeded_sms bigint) 
    LANGUAGE 'plpgsql'
    COST 100
    STABLE PARALLEL SAFE
    ROWS 1000

AS $BODY$
DECLARE
  start_date DATE;
BEGIN
  start_date = MAKE_DATE(year_input, month_input, 1);

  RETURN QUERY
    WITH filtered_orders AS (
		SELECT o._id, o.creatorname
		FROM Notifications.orders o
		WHERE o.requestedsendtime >= start_date
			AND o.requestedsendtime < start_date + INTERVAL '1 month'
	),
    email_per_order AS (
        SELECT
            e._orderid,
            COUNT(e._id)::bigint AS sent_emails,
            COALESCE(SUM(CASE WHEN e.result IN ('Delivered', 'Succeeded') THEN 1 ELSE 0 END), 0)::bigint AS succeeded_emails
        FROM notifications.emailnotifications e
		JOIN filtered_orders fo ON fo._id = e._orderid
        GROUP BY e._orderid
    ),
    sms_per_order AS (
        SELECT
            s._orderid,
            COALESCE(SUM(s.smscount), 0)::bigint AS sent_sms,
            COALESCE(SUM(CASE WHEN s.result IN ('Delivered', 'Accepted') THEN 1 ELSE 0 END), 0)::bigint AS succeeded_sms
        FROM notifications.smsnotifications s
		JOIN filtered_orders fo ON fo._id = s._orderid
        GROUP BY s._orderid
    )
    SELECT
        fo.creatorname AS org,
        COUNT(fo._id) AS placed_orders,
        COALESCE(SUM(eo.sent_emails), 0)::bigint AS sent_emails,
        COALESCE(SUM(eo.succeeded_emails), 0)::bigint AS succeeded_emails,
        COALESCE(SUM(so.sent_sms), 0)::bigint AS sent_sms,
        COALESCE(SUM(so.succeeded_sms), 0)::bigint AS succeeded_sms
    FROM filtered_orders fo
    LEFT JOIN email_per_order eo ON eo._orderid = fo._id
    LEFT JOIN sms_per_order so ON so._orderid = fo._id
    GROUP BY fo.creatorname;
   
END;
$BODY$;

ALTER FUNCTION notifications.get_metrics_v2(integer, integer)
    OWNER TO platform_notifications_admin;

COMMENT ON FUNCTION notifications.get_metrics_v2(integer, integer) IS 
'This function aggregates data by creator name, returning the total order count and the sum of notifications sent, as well as with a successful status (Succeeded or Delivered for emails, Accepted or Delivered for SMS).'
