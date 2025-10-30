-- Creates or replaces a function to delete statusfeed records older than 90 days
CREATE OR REPLACE FUNCTION notifications.delete_old_status_feed_records()
RETURNS bigint -- Returns the number of rows deleted
LANGUAGE 'plpgsql'
VOLATILE
AS $$
DECLARE
    -- Variable to hold the count of deleted rows
    deleted_count bigint := 0;
    -- Variable to hold the lock acquisition status
    lock_acquired boolean;
    -- Generate lock ID from function name using hashtext
    lock_id bigint := hashtext('notifications.delete_old_status_feed_records');
BEGIN
    -- Acquire a advisory lock to prevent concurrent cleanup operations
    SELECT pg_try_advisory_lock(lock_id) INTO lock_acquired;
    
    IF NOT lock_acquired THEN
        RETURN 0; -- Another cleanup is running
    END IF;
    
    WITH deleted_rows AS (
        DELETE FROM notifications.statusfeed 
        WHERE created <= NOW() - INTERVAL '90 days'
        RETURNING _id
    )
    -- Count the rows that were captured in the CTE
    SELECT count(*) INTO deleted_count FROM deleted_rows;
    
    PERFORM pg_advisory_unlock(lock_id);
    
    RETURN deleted_count;
END;
$$;

-- Add a comment to describe the function's purpose
COMMENT ON FUNCTION notifications.delete_old_status_feed_records() IS
'Deletes records from notifications.statusfeed where the "created" timestamp is 90 days or older. Returns the count of deleted records. Uses an advisory lock to prevent concurrent executions.';
