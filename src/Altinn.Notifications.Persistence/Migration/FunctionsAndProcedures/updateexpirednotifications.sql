CREATE OR REPLACE FUNCTION notifications.updateexpirednotifications(
    _source TEXT,
    _limit INT
)
RETURNS SETOF UUID
LANGUAGE plpgsql
AS $$
BEGIN
    -- Use lower() for case-insensitive comparison of the notification type
    IF lower(_source) = 'email' THEN
        -- If the type is 'email', run the update on the emailnotifications table
        RETURN QUERY
        WITH updated_rows AS (
            UPDATE notifications.emailnotifications
            SET result = 'Failed_TTL',
                resulttime = now()
            WHERE _id IN (
                SELECT _id
                FROM notifications.emailnotifications
                WHERE result = 'Succeeded' AND expirytime < now()
                ORDER BY _id DESC
                LIMIT GREATEST(_limit, 1) -- Use the input parameter for the limit
            )
            RETURNING alternateid -- Return all alternateids from updated rows
        )
        -- Select the unique alternateids from the CTE
        SELECT DISTINCT alternateid
        FROM updated_rows;

    ELSIF lower(_source) = 'sms' THEN
        -- If the type is 'sms', run the update on the smsnotifications table
        RETURN QUERY
        WITH updated_rows AS (
            UPDATE notifications.smsnotifications
            SET result = 'Failed_TTL',
                resulttime = now()
            WHERE _id IN (
                SELECT _id
                FROM notifications.smsnotifications
                WHERE result = 'Accepted' AND expirytime < now()
                ORDER BY _id DESC
                LIMIT GREATEST(_limit, 1) -- Use the input parameter for the limit
            )
            RETURNING alternateid -- Return all alternateids from updated rows
        )
        -- Select the unique alternateids from the CTE
        SELECT DISTINCT alternateid
        FROM updated_rows;
        
    ELSE
        -- Inform the user if an invalid type was provided. The function will return an empty set.
        RAISE NOTICE 'Invalid notification type: %. Allowed values are ''email'' or ''sms''.', _source;
    END IF;

END;
$$;

-- Add a comment to the function for documentation purposes
COMMENT ON FUNCTION notifications.updateexpirednotifications(TEXT, INT) IS 
'Updates the result of expired email or sms notifications to ''Failed''. 
Parameters: notification_type (TEXT: ''email'' or ''sms''), update_limit (INT).
Returns a set of unique alternateid for the updated records.';