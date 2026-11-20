CREATE FUNCTION notifications.get_pod_count(pod_id uuid, heartbeat_timeout interval)
RETURNS integer
LANGUAGE plpgsql
AS $$
DECLARE
	refreshed_at timestamptz;
BEGIN
	-- Keep concurrent heartbeats and stale-row cleanup in the same order across pods.
	LOCK TABLE notifications.podstate IN SHARE ROW EXCLUSIVE MODE;
	refreshed_at := clock_timestamp();

	INSERT INTO notifications.podstate (id, created, lastrefreshed)
	VALUES (pod_id, refreshed_at, refreshed_at)
	ON CONFLICT (id) DO UPDATE SET lastrefreshed = EXCLUDED.lastrefreshed;

	DELETE FROM notifications.podstate
	WHERE lastrefreshed < refreshed_at - heartbeat_timeout;

	RETURN (SELECT count(*)::integer FROM notifications.podstate);
END;
$$;

GRANT EXECUTE ON FUNCTION notifications.get_pod_count(uuid, interval) TO platform_notifications;
