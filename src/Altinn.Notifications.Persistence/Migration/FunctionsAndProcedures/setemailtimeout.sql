CREATE OR REPLACE FUNCTION notifications.set_email_timeout(new_timeout TIMESTAMPTZ)
RETURNS BOOLEAN 
LANGUAGE plpgsql
VOLATILE
PARALLEL UNSAFE
AS $$
DECLARE
    rows_affected INT;
BEGIN
    -- Acquire an advisory lock to serialize access
    PERFORM pg_advisory_xact_lock(hashtext('resourcelimitlog_email_timeout'));
    
    -- Try to update the row with the highest id
    UPDATE notifications.resourcelimitlog
    SET emaillimittimeout = new_timeout
    WHERE id = (SELECT MAX(id) FROM notifications.resourcelimitlog);
    
    GET DIAGNOSTICS rows_affected = ROW_COUNT;
    
    -- If no row was updated, insert a new one
    IF rows_affected = 0 THEN
        INSERT INTO notifications.resourcelimitlog (emaillimittimeout)
        VALUES (new_timeout);
        RETURN TRUE;
    END IF;
    
    RETURN rows_affected > 0;
END;
$$;

COMMENT ON FUNCTION notifications.set_email_timeout(TIMESTAMPTZ) IS
'Sets the email timeout value in the resource limit log.

Updates the email timeout for the most recent resource limit log entry, or creates a new entry if none exists.
Uses advisory locking to ensure thread-safe operation.

Parameters:
- new_timeout: The new timeout timestamp to set

Returns:
- TRUE if the operation was successful (either update or insert occurred)
- Raises an exception if the operation fails';
