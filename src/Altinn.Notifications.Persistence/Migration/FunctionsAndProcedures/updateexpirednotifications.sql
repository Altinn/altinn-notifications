CREATE OR REPLACE FUNCTION notifications.updateexpirednotifications(
    _source TEXT,
    _limit INT,
    _expiry_offset_seconds INT DEFAULT 300
)
RETURNS SETOF UUID
LANGUAGE plpgsql
AS $$
BEGIN
    -- Validate inputs
        IF _limit <= 0 THEN
            RAISE EXCEPTION 'Limit must be greater than 0';
        END IF;

    -- Use lower() for case-insensitive comparison of the notification type
    IF lower(_source) = 'email' THEN
        -- If the type is 'email', run the update on the emailnotifications table
        RETURN QUERY
        WITH claimed_rows AS (
            SELECT _id
            FROM notifications.emailnotifications
            WHERE result = 'Succeeded'::emailnotificationresulttype AND expirytime < (now() - make_interval(secs => _expiry_offset_seconds))
            ORDER BY expirytime ASC, _id
            FOR UPDATE SKIP LOCKED
            LIMIT _limit
        ),
        updated_rows AS (
            UPDATE notifications.emailnotifications
            SET result = 'Failed_TTL'::emailnotificationresulttype,
                resulttime = now()
            WHERE _id IN (SELECT _id FROM claimed_rows)
            RETURNING alternateid -- Return all alternateids from updated rows
        )
        -- Select the unique alternateids from the CTE
        SELECT alternateid
        FROM updated_rows;

    ELSIF lower(_source) = 'sms' THEN
        -- If the type is 'sms', run the update on the smsnotifications table
        RETURN QUERY
        WITH claimed_rows AS (
            SELECT _id
            FROM notifications.smsnotifications
            WHERE result = 'Accepted'::smsnotificationresulttype AND expirytime < (now() - make_interval(secs => _expiry_offset_seconds))
            ORDER BY expirytime ASC, _id
            FOR UPDATE SKIP LOCKED
            LIMIT _limit
        ),
        updated_rows AS (
            UPDATE notifications.smsnotifications
            SET result = 'Failed_TTL'::smsnotificationresulttype,
                resulttime = now()
            WHERE _id IN (SELECT _id FROM claimed_rows)
            RETURNING alternateid -- Return all alternateids from updated rows
        )
        -- Select the unique alternateids from the CTE
        SELECT alternateid
        FROM updated_rows;
        
    ELSE
        -- Throw an exception if an invalid type was provided
        RAISE EXCEPTION 'Invalid notification type: %. Allowed values are ''email'' or ''sms''.', _source;
    END IF;

END;
$$;

-- Add a comment to the function for documentation purposes
COMMENT ON FUNCTION notifications.updateexpirednotifications(TEXT, INT, INT) IS 
'Use row-level locking to support concurrent calls. Updates the result of expired email or sms notifications to ''Failed_TTL''. 
Parameters: 
- _source (TEXT): notification type (''email'' or ''sms'')
- _limit (INT): maximum number of records to update
- _expiry_offset_seconds (INT): grace period in seconds before marking as expired (default: 300)
Returns a set of unique alternateid for the updated records.';
