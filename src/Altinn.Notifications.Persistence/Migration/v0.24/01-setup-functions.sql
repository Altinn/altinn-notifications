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
        SUM(CASE WHEN e.result = 'Succeeded' THEN 1 ELSE 0 END) AS succeeded_emails,
        SUM(CASE WHEN s._id IS NOT NULL THEN 1 ELSE 0 END) AS sent_sms,
        SUM(CASE WHEN s.result = 'Accepted' THEN 1 ELSE 0 END) AS succeeded_sms
    FROM notifications.orders o
    LEFT JOIN notifications.emailnotifications e ON o._id = e._orderid
    LEFT JOIN notifications.smsnotifications s ON o._id = s._orderid
    WHERE EXTRACT(MONTH FROM o.requestedsendtime) = month_input
        AND EXTRACT(YEAR FROM o.requestedsendtime) = year_input
    GROUP BY o.creatorname;
END;
$$ LANGUAGE plpgsql;
