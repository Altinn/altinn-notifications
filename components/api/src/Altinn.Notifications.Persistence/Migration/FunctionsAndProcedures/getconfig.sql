CREATE FUNCTION notifications.get_config()
RETURNS TABLE (key text, value text)
LANGUAGE sql STABLE
AS $$
	SELECT config.key, config.value FROM notifications.config;
$$;

GRANT EXECUTE ON FUNCTION notifications.get_config() TO platform_notifications;
